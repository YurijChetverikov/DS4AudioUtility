using System.Threading.Channels;

namespace DS4AudioUtility.Utils
{
    internal class Dumper : IAsyncDisposable
    {
        private FileStream _fileStream;
        private Channel<byte[]> _channel;
        private Task _consumerTask;

        private volatile int consumerTotalBytes = 0;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        internal Dumper(string fileName, int maxFileSize = 67108864)
        {
            _fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 8192);

            var channelOptions = new BoundedChannelOptions(capacity: 2048)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = true
            };

            _channel = Channel.CreateBounded<byte[]>(channelOptions);

            _consumerTask = consumer(_channel, _cts.Token);
        }

        internal async Task StartAsync()
        {
            await Task.WhenAll(_consumerTask);
        }

        private async Task consumer(Channel<byte[]> channel, CancellationToken ct)
        {
            while (ct.IsCancellationRequested == false)
            {
                if (consumerTotalBytes >= 8192)
                {
                    consumerTotalBytes = 0;
                    await foreach (byte[] data in channel.Reader.ReadAllAsync())
                    {
                        await _fileStream.WriteAsync(data, ct);
                    }
                }

                await Task.Delay(500);
            }
        }


        public async ValueTask DisposeAsync()
        {
            _channel.Writer.Complete();

            _cts.Cancel();

            await _consumerTask;

            _consumerTask.Dispose();

            _fileStream.Close();

            await _fileStream.DisposeAsync();
        }

        internal async Task DumpBytesAsync(byte[] array)
        {
            if (_channel.Writer.TryWrite(array))
            {
                consumerTotalBytes += array.Length;
            }
        }
    }
}

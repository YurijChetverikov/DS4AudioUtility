using DS4AudioUtility.Utils;
using HidSharp;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace DS4AudioUtility
{
    internal class Program
    {
        const int DUAL_CHANNEL_AUDIO_DATA_SIZE = 448;
        const int JOINT_STEREO_AUDIO_DATA_SIZE = 460;
        static int AUDIO_DATA_SIZE = DUAL_CHANNEL_AUDIO_DATA_SIZE;
        static byte FRAMES_IN_PAYLOAD = 4;

        /* DUAL-SHOCK 4 settings set-up report */
        static byte _protocolID = 0x15; /* Protocol ID */
        static byte _modeType = 0xc0; /* c0 Bluetooth Mode / a0 USB Mode */
        static byte _transactionType = 0xa2; /* Transaction Type is DATA (0xa0). Report Type is OUTPUT (0x02) */
        static byte _featuresSwitch = 0xf3; /* 0xf0 Disables LED and Rumble Motors. 0xf3 Enables All of Them */
        static byte _volMic = 0x4f; /* Volume Mic */
        static byte _flashON = 0x00; /* LED Flash On */
        static byte _flashOFF = 0x00; /* LED Flash Off */




        private static Config _config = Config.DefaultDual;

        private static Process _gstProcess;
        private static TcpClient _tcpClient;
        private static NetworkStream _networkStream;
        private static HidStream _stream;
        private static HidDeviceLoader _loader = new HidDeviceLoader();

        private static Stopwatch GlobalWatch = new Stopwatch();
        private static Dumper _dumper = new Dumper(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "dump.txt");

        private static double _delayBetweenPayloads = 16;
        private static volatile bool _isPlaying = false;
        private static volatile bool _isQueueFull = false;
        private static bool _isShuttingDown = false;


        static async Task Main(string[] args)
        {
            
            // Registering posix signals to shut down application properly
            using var reg = PosixSignalRegistration.Create(PosixSignal.SIGINT, async context =>
            {
                context.Cancel = true;
                Console.CursorVisible = true;
                Console.WriteLine("Shutting down...");
                _isShuttingDown = true;
                stop();
                await _dumper.DisposeAsync();
                Environment.Exit(0);
            });

            using var regTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, async context =>
            {
                context.Cancel = true;
                Console.CursorVisible = true;
                Console.WriteLine("Shutting down...");
                _isShuttingDown = true;
                stop();
                await _dumper.DisposeAsync();
                Environment.Exit(0);
            });

            Console.CursorVisible = false;

            if (ArguementsParser.TryParse<Config>(args, Config.DefaultDual, out var config))
            {
                if (_config.ChannelMode != "dual" && _config.ChannelMode != "joint")
                {
                    // Invalid channel mode
                    Console.WriteLine($"Error: Value '{_config.ChannelMode}' for arguement '--ChannelMode' has invalid value (can be 'dual' or 'joint')");
                    return;
                }

                _config = config;

                Console.WriteLine("Configuration:\n");

                Console.WriteLine($"GStreamerPath: {_config.GStreamerPath}");
                Console.WriteLine($"DS4VId: {_config.DS4VId}");
                Console.WriteLine($"SaveDump: {_config.SaveDump}");
                Console.WriteLine($"ChannelMode: {_config.ChannelMode}");
                Console.WriteLine($"Blocks: {_config.Blocks}");
                Console.WriteLine($"Subbands: {_config.Subbands}");
                Console.WriteLine($"Bitpool: {_config.Bitpool}");
                Console.WriteLine($"QueueSize: {_config.QueueSize}");
                Console.WriteLine($"SpeakerVol: {_config.SpeakerVol}");
                Console.WriteLine($"LeftEarVol: {_config.LeftEarVol}");
                Console.WriteLine($"RightEarVol: {_config.RightEarVol}");

                Console.WriteLine("\n");
            }
            else
            {
                return;
            }


            if (File.Exists(_config.GStreamerPath) == false)
            {
                Console.WriteLine($"Unable to locate GStreamer at '{_config.GStreamerPath}'");
                return;
            }


            // IF channel mode = dual:
            // Sframe = 4 + (4*subbands*channels/8) + (blocks*channels*bitpool/8)

            // IF channel mode = joint:
            // Sframe = 4 + (4*subbands*channels/8) + (blocks*channels*bitpool/8)

            // Common formulas:
            // Nframes = AUDIO_DATA_SIZE/Sframe
            // Delay = Nframes * (subbands*blocks/frequency)

            double sFrame = 0;
            double nFrames = 0;

            

            if (_config.ChannelMode == "dual")
            {
                sFrame = 4 + (4 * (double)_config.Subbands * 2 / 8) + ((double)_config.Blocks * 2 * (double)_config.Bitpool / 8);
                AUDIO_DATA_SIZE = DUAL_CHANNEL_AUDIO_DATA_SIZE;
            }
            else if (_config.ChannelMode == "joint")
            {
                sFrame = 4 + (double)_config.Subbands + ((double)_config.Subbands + (double)_config.Blocks * (double)_config.Bitpool) / 8;
                AUDIO_DATA_SIZE = JOINT_STEREO_AUDIO_DATA_SIZE;
            }


            nFrames = (double)AUDIO_DATA_SIZE / sFrame;
            _delayBetweenPayloads = nFrames * ((double)_config.Subbands * (double)_config.Blocks / 32000) * 1000;
            FRAMES_IN_PAYLOAD = (byte)nFrames;


            // Main cycle.
            // If we playing now - just wait 500ms
            // If not - wait for device to connect and then fire start() method
            // 
            // If exception occur - handle it:
            // If IOException - device got disconnected
            // If not - unexpected expection - something gone wrong. log it and break cycle 


            while (true)
            {
                try
                {
                    if (_isShuttingDown)
                        break;

                    if(_isPlaying == false)
                    {
                        var device = _loader.GetDevices().Where(d => d.VendorID == _config.DS4VId).FirstOrDefault();

                        if (device == null)
                        {
                            // If DS4 was unplugged while it was still connected - we need to close all the streams
                            stop();

                            // Warning if device is not found
                            // And fancy looking "Reconnecting" text

                            int startTop = Console.CursorTop;

                            Console.WriteLine($"Can not find DS4 controller with VID: {_config.DS4VId}!");
                            Console.SetCursorPosition(0, startTop);
                            for (int i = 1; i < 3 + 1; i++)
                            {
                                Console.SetCursorPosition(0, startTop + 1);

                                Console.WriteLine($"Reconnecting" + string.Join("", Enumerable.Repeat('.', i)));

                                await Task.Delay(250);
                            }
                            Console.SetCursorPosition(0, startTop);
                            Console.WriteLine("                                                                           ");
                            Console.WriteLine("                                                                           ");
                            Console.SetCursorPosition(0, startTop);
                        }
                        else
                        {
                            Console.WriteLine("Connected successfully");

                            _stream = device.Open();
                            _stream.Write(sendInitReport());

                            await start();
                        }
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }
                catch (IOException io)
                {
                    Console.WriteLine("Connection loss. Reconnecting in 1s...");
                    stop();
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    break;
                }
            }

            if (_isShuttingDown == false)
                stop();
        }


        /// <summary>
        /// Starts up GStreamer & producer and consumer, reads controller's buffer 
        /// </summary>
        private static async Task start()
        {
            try
            {
                int tcpPort = getFreeTcpPort();

                // Launch GStreamer with TCP server
                string gstCommand =
                    $"wasapisrc loopback=true ! " +
                    "audioconvert ! audioresample ! " +
                    "audioresample quality=10 !" +
                    $"audio/x-raw,rate=32000,channels=2 ! " +
                    "sbcenc ! " +
                    $"audio/x-sbc,channels=2,rate=32000,channel-mode={_config.ChannelMode},blocks={_config.Blocks},subbands={_config.Subbands},bitpool={_config.Bitpool} ! " +
                    $"tcpserversink host=127.0.0.1 port={tcpPort}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _config.GStreamerPath,
                    Arguments = gstCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };


                _gstProcess = new Process { StartInfo = startInfo };
                _gstProcess.Start();
                await Task.Delay(250);  // Wait a little bit

                ProcessTracker.AttachProcess(_gstProcess); // Make OS kill gst process after we quit the application

                // Connecting to GStreamer server
                var tcpClient = new TcpClient(AddressFamily.InterNetwork);
                tcpClient.Connect(IPAddress.Loopback, tcpPort);
                _networkStream = tcpClient.GetStream();
                tcpClient.NoDelay = true;

                _isPlaying = true;

                var channelOptions = new BoundedChannelOptions(capacity: _config.QueueSize)
                {
                    FullMode = BoundedChannelFullMode.DropNewest, 
                    SingleWriter = true,                
                    SingleReader = true 
                };

                Channel<byte[]> channel = Channel.CreateBounded<byte[]>(channelOptions);

                GlobalWatch = new Stopwatch();
                GlobalWatch.Restart();

                Task producerTask = produceDataAsync(channel.Writer, channel.Reader);
                Task consumerTask = consumeAndSendDataAsync(channel.Reader);

                await Task.WhenAll(producerTask, consumerTask);
            }
            catch (SocketException ex)
            {
                // Failed to connect to GStreamer server
                string msgError = "GStreamer hasn't started yet";
                if (_gstProcess != null)
                {
                    msgError = _gstProcess.StandardError.ReadToEnd();
                }

                Console.WriteLine($"Can't start GStreamer! GStreamer error message:\n{msgError}");
                _isPlaying = false;
            }
            catch (IOException ex) when (ex.InnerException is Win32Exception win32Ex)
            {
                // Device not connected error
                if (win32Ex.NativeErrorCode == 1167)
                {
                    Console.WriteLine("Controller disconnected!");
                }
                _isPlaying = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _isPlaying = false;
            }
        }

        /// <summary>
        /// Encodes PC audio to SBC and sends in to Channel
        /// </summary>
        private static async Task produceDataAsync(ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader)
        {
            try
            {
                // Counter shows DS4 what frame number it's processing now
                ulong lilEndianCounter = 0;

                var accumulator = new List<byte>();
                byte[] socketBuffer = new byte[4096];

                while (_isPlaying)
                {
                    int bytesRead = await _networkStream.ReadAsync(socketBuffer, 0, socketBuffer.Length);
                    if (bytesRead == 0) break;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        accumulator.Add(socketBuffer[i]);
                    }

                    // Slicing bytes from accumulator to complete DS4 frames  
                    while (accumulator.Count >= AUDIO_DATA_SIZE)
                    {
                        byte[] completeFrame = accumulator.GetRange(0, AUDIO_DATA_SIZE).ToArray();

                        accumulator.RemoveRange(0, AUDIO_DATA_SIZE);

                        byte[]? bufWrite = null;

                        if (_config.ChannelMode == "joint")
                        {
                            bufWrite = new byte[530];

                            bufWrite[0] = 0x18; // Report ID
                            bufWrite[1] = 0x48;
                            bufWrite[2] = 0xA1;
                            bufWrite[3] = ((byte)(lilEndianCounter & 255)); /* Audio frame counter (endian 1)*/
                            bufWrite[4] = ((byte)((lilEndianCounter / 256) & 255)); /* Audio frame counter (endian 2) */

                            bufWrite[5] = 0x22;

                            Array.Copy(completeFrame, 0, bufWrite, 6, AUDIO_DATA_SIZE);
                            lilEndianCounter += FRAMES_IN_PAYLOAD;
                        }
                        else if (_config.ChannelMode == "dual")
                        {
                            bufWrite = new byte[462];

                            bufWrite[0] = 0x17; // Report ID
                            bufWrite[1] = 0x40; 
                            bufWrite[2] = 0xA0;
                            bufWrite[3] = ((byte)(lilEndianCounter & 255)); /* Audio frame counter (endian 1)*/
                            bufWrite[4] = ((byte)((lilEndianCounter / 256) & 255));  /* Audio frame counter (endian 2) */
                            bufWrite[5] = 0x02;

                            Array.Copy(completeFrame, 0, bufWrite, 6, AUDIO_DATA_SIZE);
                            lilEndianCounter += FRAMES_IN_PAYLOAD;
                        }

                        // CRC32 is optional. I prefer to not compute it.

                        if (reader.Count == _config.QueueSize)
                        {
                            // If this happens - it's not good.
                            // It means that you try to send more data than controller can process
                            _isQueueFull = true;
                            Thread.Sleep(1);
                            Console.WriteLine($"Queue is full ({_config.QueueSize}/{_config.QueueSize}). Frame was dropped");
                        }
                        else
                        {
                            _isQueueFull = false;
                            writer.TryWrite(bufWrite);
                        }


                        if (_config.SaveDump)
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                await ms.WriteAsync(Encoding.UTF8.GetBytes($"PAYLOAD DUMP ({GlobalWatch.Elapsed.ToString("mm\\:ss\\.ffff")}):\n"));
                                if (bufWrite == null)
                                {
                                    await ms.WriteAsync(Encoding.UTF8.GetBytes("NULL NULL NULL NULL\n"));
                                }
                                else
                                {
                                    for (int i = 0; i < bufWrite.Length / 16; i++)
                                    {
                                        for (int h = 0; h < 16; h++)
                                        {
                                            if (bufWrite.Length <= i * 16 + h)
                                                break;
                                            await ms.WriteAsync(Encoding.UTF8.GetBytes("0x" + bufWrite[i * 16 + h].ToString("X2") + (' ')));
                                        }
                                        await ms.WriteAsync(Encoding.UTF8.GetBytes("\n"));
                                    }

                                }
                                await ms.WriteAsync(Encoding.UTF8.GetBytes("\n-----------------------------------------------------------------------------\n"));

                                await ms.FlushAsync();

                                await _dumper.DumpBytesAsync(ms.ToArray());
                            }
                        }
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is System.Net.Sockets.SocketException socketEx)
            {
                // GStreamer disconnected
                Console.WriteLine("GStreamer dicsonnected!");
                _isPlaying = false;
            }
            catch (IOException ex) when (ex.InnerException is Win32Exception win32Ex)
            {
                // Device not connected error
                if (win32Ex.NativeErrorCode == 1167)
                {
                    Console.WriteLine("Controller disconnected!");
                }
                else
                {
                    Console.WriteLine(ex);
                }
                _isPlaying = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                _isPlaying = false;
            }
            finally
            {
                writer.Complete();
            }
        }

        /// <summary>
        /// Reads SBC-encoded PC audio from Channel and writes it to the controller
        /// </summary>
        private static async Task consumeAndSendDataAsync(ChannelReader<byte[]> reader)
        {
            var sw = new Stopwatch();

            byte counter = 0;

            double msPerPacket = _delayBetweenPayloads;
            double fullQueueMsPerPacket = msPerPacket * 0.5;
            long previousPayloadMs = 0;
            double nextPacketTime = 0;

            await foreach(byte[] payload in reader.ReadAllAsync())
            {
                if (_isPlaying)
                {
                    try
                    {
                        if (payload != null)
                        {
                            if (sw.IsRunning == false)
                            {
                                sw.Start();
                            }
                            else
                            {
                                while (sw.Elapsed.TotalMilliseconds < previousPayloadMs + (_isQueueFull ? fullQueueMsPerPacket : msPerPacket))
                                {
                                    //await Task.Delay(1);
                                    Thread.SpinWait(50);
                                }
                            }

                            //Console.WriteLine($"{sw.ElapsedMilliseconds - previousPayloadMs}");

                            previousPayloadMs = sw.ElapsedMilliseconds;

                            

                            _stream.Write(payload);

                            // Every 10 payloads and if _config.ReadBuffer == true 
                            // read the controller's buffer
                            // it may sometimes help with disconnecting in DS4Windows
                            if (_config.ReadBuffer)
                            {
                                if (counter == 10)
                                {
                                    _stream.Read();
                                    counter = 0;
                                }
                                counter++;
                            }
                        }
                            

                    }
                    catch (System.ObjectDisposedException ex)
                    {
                        // close() fired
                        _isPlaying = false;
                    }
                    catch (IOException ex) when (ex.InnerException is Win32Exception win32Ex)
                    {
                        // Device not connected error
                        if (win32Ex.NativeErrorCode == 1167)
                        {
                            Console.WriteLine("Controller disconnected!");
                        }
                        else if (win32Ex.NativeErrorCode == 31)
                        {
                            Console.WriteLine("Controller can't process SBC encoding configuration that provided");
                        }
                        else
                        {
                            Console.WriteLine(ex);
                        }
                        _isPlaying = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        _isPlaying = false;
                    }
                }

                //sw.Reset();
            }
        }




        /// <summary>
        /// Send report ID 0x15 to controller with all the configuration
        /// </summary>
        /// <returns></returns>
        private static byte[] sendInitReport()
        {
            byte[] bufWrite = new byte[334];

            bufWrite[0] = _protocolID; /* Protocol ID */
            bufWrite[1] = _modeType; /* c0 Blueooth Mode, a0 USB Mode */
            bufWrite[2] = _transactionType; /* Transaction Type is DATA (0xa0), Report Type is OUTPUT (0x02) */
            bufWrite[3] = _featuresSwitch; /* 0xf0 disables the LEDs and rumble motors, 0xf3 enables them */
            bufWrite[4] = 0x44; /* Unknown */
            bufWrite[5] = 0x00; /* Unknown */
            bufWrite[6] = 0;/* Rumble Power Right */
            bufWrite[7] = 0; /* Rumble Power Left */
            bufWrite[8] = 0; /* Red */
            bufWrite[9] = 0; /* Green*/
            bufWrite[10] = 0; /* Blue */
            bufWrite[11] = _flashON; /* LED Flash On */
            bufWrite[12] = _flashOFF; /* LED Flash Off */
            /* ... */
            bufWrite[20] = _config.LeftEarVol; /* Vol Left */
            bufWrite[21] = _config.RightEarVol; /* Vol Right */
            bufWrite[22] = 0x00; /* Unknown */
            bufWrite[23] = _volMic; /* Vol Mic */
            bufWrite[24] = _config.SpeakerVol; /* Vol Built-in Speaker */
            bufWrite[25] = 0x40; /* Unknown */
            /* ... */
            bufWrite[78] = ((byte)(0 & 255)); /* Audio frame counter (endian 1)*/
            bufWrite[79] = ((byte)((0 / 256) & 255)); /* Audio frame counter (endian 2) */
            bufWrite[80] = 0x24; /* 0x02 Speaker Mode On / 0x24 Headset Mode On*/

            //bufWrite[330] = 0x00; bufWrite[331] = 0x00; bufWrite[332] = 0x00; bufWrite[333] = 0x00; /* CRC-32 */
            return bufWrite;
        }

        private static int getFreeTcpPort()
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                s.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)s.LocalEndPoint!).Port;
            }
        }

        private static void stop()
        {
            _isPlaying = false;
            _networkStream?.Close();
            _tcpClient?.Close();
            _stream?.Close();
        }
    }
}

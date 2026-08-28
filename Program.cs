using CSCore.Codecs;
using CSCore.Codecs.WAV;
using DS4AudioUtil.Utils;
using HidSharp;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DS4AudioUtil
{
    internal class Program
    {
        const int AUDIO_DATA_SIZE = 448;

        /* DUAL-SHOCK 4 settings */
        static byte _protocolID = 0x15; /* Protocol ID */
        static byte _modeType = 0xc0; /* c0 Bluetooth Mode / a0 USB Mode */
        static byte _transactionType = 0xa2; /* Transaction Type is DATA (0xa0). Report Type is OUTPUT (0x02) */
        static byte _featuresSwitch = 0xf3; /* 0xf0 Disables LED and Rumble Motors. 0xf3 Enables All of Them */
        static byte _volMic = 0x4f; /* Volume Mic */
        static byte _flashON = 0x00; /* LED Flash On */
        static byte _flashOFF = 0x00; /* LED Flash Off */


        private static Config _config = Config.HighQuality;

        private static Process _gstProcess;
        private static TcpClient _tcpClient;
        private static NetworkStream _networkStream;
        private static HidStream _stream;
        private static HidDeviceLoader _loader = new HidDeviceLoader();

        private static double _delayBetweenPayloads = 16;
        private static ConcurrentQueue<byte[]> _audioQueue = new ConcurrentQueue<byte[]>();
        private static bool _isPlaying = false;


        static async Task Main(string[] args)
        {
            // Registering posix signals to shut down application properly
            using var reg = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                Console.CursorVisible = true;
                Console.WriteLine("Shutting down...");
                stop();
                Environment.Exit(0);
            });

            using var regTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                Console.CursorVisible = true;
                Console.WriteLine("Shutting down...");
                stop();
                Environment.Exit(0);
            });

            Console.CursorVisible = false;

            if (ArguementsParser.TryParse<Config>(args, Config.HighQuality, out var config))
            {
                _config = config;

                Console.WriteLine("Configuration:\n");

                Console.WriteLine($"GStreamerPath: {_config.GStreamerPath}");
                Console.WriteLine($"DS4VId: {_config.DS4VId}");
                Console.WriteLine($"BytesToReadFromControllerBuffer: {_config.BytesToReadFromControllerBuffer}");
                Console.WriteLine($"Frequency: {_config.Frequency}");
                Console.WriteLine($"Blocks: {_config.Blocks}");
                Console.WriteLine($"Subbands: {_config.Subbands}");
                Console.WriteLine($"Bitpool: {_config.Bitpool}");
                Console.WriteLine($"AudioFramesQueueSize: {_config.AudioFramesQueueSize}");
                Console.WriteLine($"BuiltinSpeakerVolume: {_config.BuiltinSpeakerVolume}");
                Console.WriteLine($"LeftEarVolume: {_config.LeftEarVolume}");
                Console.WriteLine($"RightEarVolume: {_config.RightEarVolume}");

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


            // Sframe = 4 + (4*subbands*channels/8) + (blocks*channels*bitpool/8)
            // Nframes = AUDIO_DATA_SIZE/Sframe
            // Delay = Nframes * (subbands*blocks/frequency)

            _delayBetweenPayloads = ((double)AUDIO_DATA_SIZE / (4 + (4 * (double)_config.Subbands * 2 / 8) + ((double)_config.Blocks * 2 * (double)_config.Bitpool / 8))) * ((double)_config.Subbands * (double)_config.Blocks / (double)_config.Frequency) * 1000;




            while (true)
            {
                try
                {
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

                            start();
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

            stop();
        }


        private static void start()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            try
            {
                int tcpPort = getFreeTcpPort();

                // Launch GStreamer with TCP server
                string gstCommand =
                    $"wasapisrc loopback=true ! " +
                    "audioconvert ! audioresample ! " +
                    "audioresample quality=10 !" +
                    $"audio/x-raw,rate={_config.Frequency},channels=2 ! " +
                    "sbcenc ! " +
                    $"audio/x-sbc,channels=2,rate={_config.Frequency},channel-mode=dual,blocks={_config.Blocks},subbands={_config.Subbands},bitpool={_config.Bitpool} ! " +
                    $"tcpserversink host=127.0.0.1 port={tcpPort}";

                var startInfo = new ProcessStartInfo
                {
                    FileName = _config.GStreamerPath,
                    Arguments = gstCommand,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _gstProcess = new Process { StartInfo = startInfo };
                _gstProcess.Start();
                Thread.Sleep(250);  // Wait a little bit

                ProcessTracker.AttachProcess(_gstProcess); // Make OS kill gst process after we quit the application

                // Connecting to GStreamer server
                var tcpClient = new TcpClient(AddressFamily.InterNetwork);
                tcpClient.Connect(IPAddress.Loopback, tcpPort);
                _networkStream = tcpClient.GetStream();
                tcpClient.NoDelay = true;

                _audioQueue = new ConcurrentQueue<byte[]>();
                _isPlaying = true;


                // Task that reads controller data
                // This piece of code is important because without it DS4Windows won't be working

                byte[] discardBuffer = new byte[_config.BytesToReadFromControllerBuffer];
                _stream.Read(discardBuffer, 0, discardBuffer.Length);
  

                // Consumer audio thread / audio sender thread
                Thread senderThread = new Thread(() =>
                {
                    double msPerPacket = _delayBetweenPayloads;
                    var sw = new Stopwatch();
                    sw.Start();
                    double nextPacketTime = 0;

                    while (_isPlaying)
                    {
                        try
                        {
                            if (_audioQueue.TryDequeue(out byte[] bufWrite))
                            {
                                if (bufWrite != null)
                                    _stream.Write(bufWrite);

                                //byte[] buff_buffer = new byte[64];
                                //int bytesRead = _stream.Read(buff_buffer, 0, buff_buffer.Length);

                                nextPacketTime += msPerPacket;
                                while (sw.Elapsed.TotalMilliseconds < nextPacketTime)
                                {
                                    Thread.SpinWait(50);
                                }
                            }
                            else
                            {
                                // Queue is empty. Wait for data
                                Thread.Sleep(2);
                            }
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
                    }
                });


                senderThread.Priority = ThreadPriority.Highest;
                senderThread.Start();

                // Producer thread / gstreamer reader
                Thread producerThread = new Thread(() =>
                {
                    try
                    {
                        // Counter shows DS4 what frame number it's processing now
                        ulong lilEndianCounter = 0;

                        var accumulator = new List<byte>();
                        byte[] socketBuffer = new byte[4096];

                        while (_isPlaying)
                        {
                            int bytesRead = _networkStream.Read(socketBuffer, 0, socketBuffer.Length);
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

                                if (_audioQueue.Count < _config.AudioFramesQueueSize)
                                {
                                    byte[] bufWrite = new byte[462];


                                    bufWrite[0] = 0x17; // Report ID
                                    bufWrite[1] = 0x40; // 
                                    bufWrite[2] = 0xA0;
                                    bufWrite[3] = (byte)(lilEndianCounter & 0xFF);
                                    bufWrite[4] = (byte)((lilEndianCounter >> 8) & 0xFF);
                                    bufWrite[5] = 0x02;

                                    Array.Copy(completeFrame, 0, bufWrite, 6, 448);
                                    lilEndianCounter += 2;

                                    // CRC32 is optional
                                    //uint asdg = Crc32Algorithm.Compute(testBytes);
                                    //byte[] df = BitConverter.GetBytes(asdg);

                                    _audioQueue.Enqueue(bufWrite);
                                }
                                else
                                {
                                    // If this happens - it not good.
                                    // It means that you try to send more data than controller can process
                                    Thread.Sleep(1);
                                    Console.WriteLine("Queue is full. Frame was dropped");
                                }
                            }
                        }
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
                });
                producerThread.Start();
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
        /// Send report ID 0x15 to controller
        /// Send 
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
            bufWrite[20] = _config.LeftEarVolume; /* Vol Left */
            bufWrite[21] = _config.RightEarVolume; /* Vol Right */
            bufWrite[22] = 0x00; /* Unknown */
            bufWrite[23] = _volMic; /* Vol Mic */
            bufWrite[24] = _config.BuiltinSpeakerVolume; /* Vol Built-in Speaker */
            bufWrite[25] = 0x40; /* Unknown */
            /* ... */
            bufWrite[78] = ((byte)(0 & 255)); /* Audio frame counter (endian 1)*/
            bufWrite[79] = ((byte)((0 / 256) & 255)); /* Audio frame counter (endian 2) */
            bufWrite[80] = 0x02; /* 0x02 Speaker Mode On / 0x24 Headset Mode On*/

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

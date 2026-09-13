namespace DS4AudioUtil.Utils
{
    internal struct Config
    {
        internal static readonly Config Default = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            BufferReadSize = 512,

            Frequency = 32000,
            Blocks = 16,
            Subbands = 8,
            Bitpool = 25,
            QueueSize = 10,

            SpeakerVol = 70,
            LeftEarVol = 0x73,
            RightEarVol = 0x73,
        };

        internal static readonly Config HighQuality = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            BufferReadSize = 512,

            Frequency = 32000,
            Blocks = 16,
            Subbands = 8,
            Bitpool = 53,
            QueueSize = 10,

            SpeakerVol = 70,
            LeftEarVol = 0x73,
            RightEarVol = 0x73,
        };

        internal static readonly Config MediumQuality = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            BufferReadSize = 512,

            Frequency = 32000,
            Blocks = 8,
            Subbands = 8,
            Bitpool = 26,
            QueueSize = 10,

            SpeakerVol = 70,
            LeftEarVol = 0x73,
            RightEarVol = 0x73,
        };

        public required string GStreamerPath;
        public required int DS4VId;
        public required ushort BufferReadSize;

        // Audio Settings
        public required byte Subbands;
        public required byte Bitpool;
        public required byte Blocks;
        public required ushort Frequency;
        public required byte QueueSize;

        // Controller Settings

        public required byte SpeakerVol;
        public required byte LeftEarVol;
        public required byte RightEarVol;
    }
}

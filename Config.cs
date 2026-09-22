namespace DS4AudioUtility.Utils
{
    internal struct Config
    {
        internal static readonly Config DefaultDual = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            SaveDump = false,
            ReadBuffer = false,

            ChannelMode = "dual",
            Blocks = 16,
            Subbands = 8,
            Bitpool = 53,
            QueueSize = 10,

            SpeakerVol = 70,
            LeftEarVol = 0x73,
            RightEarVol = 0x73,
        };

        internal static readonly Config DefaultJoint = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            SaveDump = false,
            ReadBuffer = false,

            ChannelMode = "joint",
            Blocks = 16,
            Subbands = 8,
            Bitpool = 51,
            QueueSize = 10,

            SpeakerVol = 70,
            LeftEarVol = 0x73,
            RightEarVol = 0x73,
        };

        public required string GStreamerPath;
        public required int DS4VId;
        public required bool SaveDump;
        public required bool ReadBuffer;

        // Audio Settings
        public required byte Subbands;
        public required byte Bitpool;
        public required byte Blocks;
        public required string ChannelMode;
        public required byte QueueSize;

        // Controller Settings

        public required byte SpeakerVol;
        public required byte LeftEarVol;
        public required byte RightEarVol;
    }
}

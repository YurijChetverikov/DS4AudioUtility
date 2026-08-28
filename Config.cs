namespace DS4AudioUtil.Utils
{
    internal struct Config
    {
        internal static readonly Config Default = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            BytesToReadFromControllerBuffer = 512,

            Frequency = 32000,
            Blocks = 16,
            Subbands = 8,
            Bitpool = 25,
            AudioFramesQueueSize = 10,

            BuiltinSpeakerVolume = 70,
            LeftEarVolume = 0x73,
            RightEarVolume = 0x73,
        };

        internal static readonly Config HighQuality = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            BytesToReadFromControllerBuffer = 512,

            Frequency = 32000,
            Blocks = 16,
            Subbands = 8,
            Bitpool = 53,
            AudioFramesQueueSize = 10,

            BuiltinSpeakerVolume = 70,
            LeftEarVolume = 0x73,
            RightEarVolume = 0x73,
        };

        internal static readonly Config MediumQuality = new Config()
        {
            GStreamerPath = @"C:\Program Files\gstreamer\1.0\msvc_x86_64\bin\gst-launch-1.0.exe",
            DS4VId = 1356,
            BytesToReadFromControllerBuffer = 512,

            Frequency = 32000,
            Blocks = 8,
            Subbands = 8,
            Bitpool = 26,
            AudioFramesQueueSize = 10,

            BuiltinSpeakerVolume = 70,
            LeftEarVolume = 0x73,
            RightEarVolume = 0x73,
        };

        public required string GStreamerPath;
        public required int DS4VId;
        public required ushort BytesToReadFromControllerBuffer;

        // Audio Settings
        public required byte Subbands;
        public required byte Bitpool;
        public required byte Blocks;
        public required ushort Frequency;
        public required byte AudioFramesQueueSize;

        // Controller Settings

        public required byte BuiltinSpeakerVolume;
        public required byte LeftEarVolume;
        public required byte RightEarVolume;
    }
}

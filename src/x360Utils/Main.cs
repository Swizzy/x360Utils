namespace x360Utils {
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using x360Utils.Common;

    public static class Main {
        public enum VerbosityLevels {
            Minimum = 0,
            Low = 1,
            Medium = 2,
            High = 3,
            Debug = 2147483646,
            FullDebug = int.MaxValue
        }

        public const string FirstBlKey = "DD88AD0C9ED669E7B56794FB68563EFA";
        public const string MfgBlKey = "00000000000000000000000000000000";
        public static readonly byte[] FirstBlKeyBytes;
        public static readonly byte[] MfgBlKeyBytes = new byte[16];

        private static readonly Version AppVersion = Assembly.GetAssembly(typeof(Main)).GetName().Version;

        public static int VerbosityLevel;

        static Main() {
            VerbosityLevel = 0;
            FirstBlKeyBytes = StringUtils.HexToArray(FirstBlKey);
        }

        public static string Version {
            get {
#if DEBUG
#if NO_DEBUG_PRINT
                return string.Format("x360Utils v{0}.{1} (Build: {2}) [ DEBUG No Print ]", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#else
                return string.Format("x360Utils v{0}.{1} (Build: {2}) [ DEBUG ]", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#endif
#elif PRINTDEBUG
                return string.Format("x360Utils v{0}.{1} (Build: {2}) [ Print Debug Messages ]", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#else
                return string.Format("x360Utils v{0}.{1} (Build: {2})", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#endif
            }
        }

        [DllImport("shell32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsUserAnAdmin();

        public static event EventHandler<EventArg<string>> InfoOutput;

        public static event EventHandler<EventArg<int>> BlockInReader;

        public static event EventHandler<EventArg<int>> MaxBlocksChanged;

        internal static bool VerifyVerbosityLevel(int level) { return level <= VerbosityLevel; }

        internal static void SendInfo(string message, params object[] args) {
            var info = InfoOutput;
            if(info == null || message == null)
                return;
            message = args.Length == 0 ? message : string.Format(message, args);
            info(null, new EventArg<string>(message));
        }

        internal static void SendReaderBlock(long offset) {
            var bir = BlockInReader;
            if(bir == null)
                return;
            offset = offset - offset % 4000;
            bir(null, new EventArg<int>((int)(offset / 0x4000)));
        }

        internal static void SendMaxBlocksChanged(int blocks) {
            var mbc = MaxBlocksChanged;
            if(mbc == null)
                return;
            mbc(null, new EventArg<int>(blocks));
            SendReaderBlock(0);
        }

        public static bool VerifyVerbosityLevel(VerbosityLevels level) { return VerifyVerbosityLevel((int)level); }
    }
}
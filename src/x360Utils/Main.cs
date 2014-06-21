namespace x360Utils {
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public static class Main {
        private static readonly Version AppVersion = Assembly.GetAssembly(typeof(Main)).GetName().Version;

        public static int VerbosityLevel;

        static Main() { VerbosityLevel = 0; }

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
    }
}
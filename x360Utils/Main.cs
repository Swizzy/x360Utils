namespace x360Utils {
    using System;
    using System.IO;
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

        public static event EventHandler<EventArg<int, int>> BlockInReader;

        internal static void SendInfo(VerbosityLevels verbosity,string message, params object[] args) {
            if(verbosity > (VerbosityLevels)VerbosityLevel)
                return;
            if(verbosity == VerbosityLevels.Debug || verbosity == VerbosityLevels.FullDebug) {
                Debug.SendDebug(message, args);
                return;
            }
            var info = InfoOutput;
            if(info == null || message == null)
                return;
            message = args.Length == 0 ? message : string.Format(message, args);
            info(null, new EventArg<string>(message));
        }

        internal static void SendReaderBlock(long offset, int blocks) {
            var bir = BlockInReader;
            if(bir == null)
                return;
            offset = offset - offset % 4000;
            bir(null, new EventArg<int, int>((int)(offset / 0x4000), blocks));
        }

        public static byte[] GetEmbeddedResource(string name, bool addNameSpace = true) {
            if(addNameSpace)
                name = string.Format("{0}.{1}", typeof(Main).Namespace, name);
            using(var stream = Assembly.GetAssembly(typeof(Main)).GetManifestResourceStream(name)) {
                if (stream == null)
                    throw new FileNotFoundException(name);
                using(var br = new BinaryReader(stream))
                    return br.ReadBytes((int)br.BaseStream.Length);
            }
        }
    }
}
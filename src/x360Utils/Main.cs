namespace x360Utils
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public static class Main {
        static Main() {
            VerbosityLevel = 0;
        }

        private static readonly Version AppVersion = Assembly.GetAssembly(typeof(Main)).GetName().Version;

        public static int VerbosityLevel;

        public static string Version {
            get { 
#if DEBUG
                return string.Format("x360Utils v{0}.{1} (Build: {2}) [ DEBUG ]", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#elif PRINTDEBUG
                return string.Format("x360Utils v{0}.{1} (Build: {2}) [ Print Debug Messages ]", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#else
                    return string.Format("x360Utils v{0}.{1} (Build: {2})", AppVersion.Major, AppVersion.Minor, AppVersion.Build);
#endif
            }
        }

        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsUserAnAdmin();

        public static event EventHandler<EventArg<string>> InfoOutput;

        internal static bool VerifyVerbosityLevel(int level) {
            return level <= VerbosityLevel;
        }

        internal static void SendInfo(string message, params object[] args)
        {
            var info = InfoOutput;
            if(info == null || message == null)
                return;
            message = args.Length == 0 ? message : string.Format(message, args);
            info(null, new EventArg<string>(message));
        }
    }
}
namespace x360Utils
{
    using System;
    using System.Reflection;

    public static class Main {
        private static readonly Version AppVersion = Assembly.GetAssembly(typeof(Main)).GetName().Version;

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
    }
}
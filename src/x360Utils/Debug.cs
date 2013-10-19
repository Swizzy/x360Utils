#if DEBUG
#define PRINTDEBUG // Make sure we print debug messages on debug builds
#endif

namespace x360Utils {
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    public static class Debug {
        [DllImport("shell32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]public static extern bool IsUserAnAdmin();

        public static event EventHandler<EventArg<string>> DebugOutput;

        [Conditional("PRINTDEBUG")]
        internal static void SendDebug(string message, params object[] args)
        {
            message = args.Length == 0 ? message : string.Format(message, args);
            var dbg = DebugOutput;
            if(dbg != null && message != null)
                dbg(null, new EventArg<string>(message));
        }
    }
}
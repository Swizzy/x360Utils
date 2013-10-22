#region

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#endregion

namespace x360Utils {
    public static class Debug {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsUserAnAdmin();

        public static event EventHandler<EventArg<string>> DebugOutput;

        [Conditional("PRINTDEBUG")] [Conditional("DEBUG")] 
        internal static void SendDebug(string message, params object[] args) {
            message = args.Length == 0 ? message : string.Format(message, args);
            var dbg = DebugOutput;
            if (dbg != null && message != null)
                dbg(null, new EventArg<string>(message));
        }
    }
}
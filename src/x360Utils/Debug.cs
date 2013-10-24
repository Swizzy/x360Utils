#region

using System;
using System.Diagnostics;

#endregion

namespace x360Utils {
    public static class Debug {
        public static event EventHandler<EventArg<string>> DebugOutput;

        [Conditional("PRINTDEBUG")] [Conditional("DEBUG")] 
        internal static void SendDebug(string message, params object[] args) {
            var dbg = DebugOutput;
            if(dbg == null || message == null)
                return;
            message = args.Length == 0 ? message : string.Format(message, args);
            dbg(null, new EventArg<string>(message));
        }
    }
}
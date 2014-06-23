namespace x360Utils {
    using System;
    using System.Diagnostics;

    public static class Debug {
        // ReSharper disable once EventNeverInvoked
        public static event EventHandler<EventArg<string>> DebugOutput;

        [Conditional("PRINTDEBUG")] [Conditional("DEBUG")] internal static void SendDebug(string message, params object[] args) {
#if NO_DEBUG_PRINT // Ignore it!
#else
            var dbg = DebugOutput;
            if(dbg == null || message == null)
                return;
            message = args.Length == 0 ? message : string.Format(message, args);
            dbg(null, new EventArg<string>(message));
#endif
        }
    }
}
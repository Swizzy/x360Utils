namespace x360Utils {
    using System;

    public static class Debug {
        public static event EventHandler<EventArg<string>> DebugOutput;

        internal static void SendDebug(string message, params object[] args) {
            var dbg = DebugOutput;
            if(dbg == null || message == null)
                return;
            message = args.Length == 0 ? message : string.Format(message, args);
            dbg(null, new EventArg<string>(message));
        }
    }
}
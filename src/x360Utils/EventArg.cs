namespace x360Utils {
    using System;

    public sealed class EventArg <T>: EventArgs {
        private readonly T _data;

        internal EventArg(T data) { _data = data; }

        public T Data { get { return _data; } }
    }

    public sealed class EventArg <T1, T2>: EventArgs {
        private readonly T1 _data1;
        private readonly T2 _data2;

        public EventArg(T1 data1, T2 data2) {
            _data1 = data1;
            _data2 = data2;
        }

        public T1 Data1 { get { return _data1; } }

        public T2 Data2 { get { return _data2; } }
    }
}
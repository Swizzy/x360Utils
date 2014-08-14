namespace x360Utils.Network {
    using System;

    public sealed class XeLLNetworkException: Exception {
        internal XeLLNetworkException(string message): base(message) { }

        internal XeLLNetworkException() { }
    }
}
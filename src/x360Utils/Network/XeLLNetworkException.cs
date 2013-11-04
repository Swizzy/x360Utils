namespace x360Utils.Network {
    using System;

    internal sealed class XeLLNetworkException : Exception {
        internal XeLLNetworkException(string message) : base(message) {
        }

        internal XeLLNetworkException() {
        }
    }
}
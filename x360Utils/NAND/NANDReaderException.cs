namespace x360Utils.NAND {
    using System;

    public class NANDReaderException: Exception {
        public enum ErrorTypes {
            NotEnoughData,
            BadMagic
        }

        public ErrorTypes ErrorType;

        public NANDReaderException(ErrorTypes errorType, string msg = "") {
            ErrorType = errorType;
            Message = msg;
        }

        public new string Message { get; private set; }
    }
}
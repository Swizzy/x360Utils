namespace x360Utils {
    using System;

    public sealed class X360UtilsException : Exception {
        #region X360UtilsErrors enum

        public enum X360UtilsErrors {
            KeyTooShort,
            KeyTooLong,
            KeyInvalidHamming,
            KeyInvalidECD,
            KeyFileNoKeyFound,
            DataTooSmall,
            DataTooBig
        }

        #endregion X360UtilsErrors enum

        public readonly X360UtilsErrors ErrorCode;

        public X360UtilsException(X360UtilsErrors errorCode) {
            ErrorCode = errorCode;
        }
    }
}
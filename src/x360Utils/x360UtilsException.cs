#region

using System;

#endregion

namespace x360Utils {
    public sealed class X360UtilsException : Exception {
        #region X360UtilsErrors enum

        public enum X360UtilsErrors {
            KeyTooShort,
            KeyTooLong,
            KeyInvalidHamming,
            KeyInvalidECD,
            KeyFileNoKeyFound,
            DataTooSmall,
            DataTooBig,
            DataNotFound,
            DataNotDecrypted,
            BadChecksum,
            DataInvalid,
            DataDecryptionFailed,
            DataEncryptionFailed
        }

        #endregion X360UtilsErrors enum

        public readonly X360UtilsErrors ErrorCode;

        public new readonly string Message;

        public X360UtilsException(X360UtilsErrors errorCode, string message = "") {
            ErrorCode = errorCode;
            Message = message;
        }
    }
}
#region



#endregion

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
            DataTooBig,
            DataNotFound,
            DataNotDecrypted,
            BadChecksum,
            DataInvalid,
            DataDecryptionFailed,
            UnkownMetaType,
            BadBlockDetected,
            UnkownPatchset
        }

        #endregion

        public readonly X360UtilsErrors ErrorCode;

        public new readonly string Message;

        public X360UtilsException(X360UtilsErrors errorCode, string message = "") {
            ErrorCode = errorCode;
            Message = message;
        }

        public override string ToString() { return string.Format("x360UtilsException!{0}ErrorCode: {1}{0}Message: {2}{0}StackTrace: {0}{3}", Environment.NewLine, ErrorCode, Message, StackTrace); }
    }
}
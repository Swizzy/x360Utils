namespace x360Utils.CPUKey {
    using System;

    public class CpuKeyException: Exception {
        public readonly ExceptionTypes ExceptionType;

        public CpuKeyException(ExceptionTypes exceptionType) { ExceptionType = exceptionType; }

        public enum ExceptionTypes {
            Hamming,
            Ecd,
            NoValidKeyFound,
            InvalidLength
        }
    }
}
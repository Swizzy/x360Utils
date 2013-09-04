namespace x360Utils.Common {
    using System;

    public sealed class BitOperations {
        public UInt64 Swap(UInt64 x) {
            return x << 56 | x << 40 & 0xff000000000000 | x << 24 & 0xff0000000000 | x << 8 & 0xff00000000 | x >> 8 & 0xff000000 | x >> 24 & 0xff0000 | x >> 40 & 0xff00 | x >> 56;
        }

        public UInt32 Swap(UInt32 x) {
            return (x & 0x000000FF) << 24 | (x & 0x0000FF00) << 8 | (x & 0x00FF0000) >> 8 | (x & 0xFF000000) >> 24;
        }

        public UInt16 Swap(UInt16 x) {
            return (UInt16) ((0xFF00 & x) >> 8 | (0x00FF & x) << 8);
        }

        public uint CountSetBits(UInt64 n) {
            uint c;
            for(c = 0; n > 0; c++)
                n &= n - 1;
            return c;
        }

        public bool CompareByteArrays(byte[] a1, byte[] a2) {
            if(a1 == a2)
                return true;
            if(a1 == null || a2 == null || a1.Length != a2.Length)
                return false;
            for(var index = 0; index < a1.Length; index++) {
                if(a1[index] != a2[index])
                    return false;
            }
            return true;
        }

        public static bool DataIsZero(ref byte[] data, int offset, int length)
        {
            for (var i = 0; i < length; i++)
                if (data[offset + i] != 0x00)
                    return false;
            return true;
        }
    }
}
namespace x360Utils.Common {
    public static class BitOperations {
        public static ulong Swap(ulong x) {
            return x << 56 | x << 40 & 0xff000000000000 | x << 24 & 0xff0000000000 | x << 8 & 0xff00000000 |
                   x >> 8 & 0xff000000 | x >> 24 & 0xff0000 | x >> 40 & 0xff00 | x >> 56;
        }

        public static uint Swap(uint x) {
            return (x & 0x000000FF) << 24 | (x & 0x0000FF00) << 8 | (x & 0x00FF0000) >> 8 | (x & 0xFF000000) >> 24;
        }

        public static ushort Swap(ushort x) {
            return (ushort) ((0xFF00 & x) >> 8 | (0x00FF & x) << 8);
        }

        public static uint CountSetBits(ulong n) {
            uint c;
            for (c = 0; n > 0; c++)
                n &= n - 1;
            return c;
        }

        public static bool DataIsZero(ref byte[] data, int offset, int length) {
            for (var i = 0; i < length; i++)
                if (data[offset + i] != 0x00)
                    return false;
            return true;
        }

        public static int CountByteInstances(ref byte[] data, byte instance) {
            var count = 0;
            foreach (var b in data)
                if (b == instance)
                    count++;
            return count;
        }

        public static int CountByteInstances(ref byte[] data, byte instance, int offset, int length = 0) {
            var count = 0;
            length = length <= 0 ? data.Length - offset : offset + length;
            for (; offset < length; offset++)
                if (data[offset] == instance)
                    count++;
            return count;
        }

        public static bool CompareByteArrays(ref byte[] a1, ref byte[] a2, bool checkmax = true) {
            if (a1 == a2)
                return true;
            if (a1 == null || a2 == null || (checkmax && a1.Length != a2.Length))
                return false;
            if(!checkmax) {
                if(a1.Length < a2.Length) {
                    for(var index = 0; index < a1.Length; index++)
                        if(a1[index] != a2[index])
                            return false;
                }
                else {
                    for(var index = 0; index < a2.Length; index++)
                        if(a1[index] != a2[index])
                            return false;
                }
            }
            else {
                for(var index = 0; index < a1.Length; index++)
                    if(a1[index] != a2[index])
                        return false;
            }
            return true;
        }
    }
}
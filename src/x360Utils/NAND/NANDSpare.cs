#region

using System;

#endregion

namespace x360Utils.NAND {
    internal class NANDSpare {
        public static byte[] CalculateECD(ref byte[] data, int offset) {
            UInt32 i, val = 0, v = 0;
            var count = 0;
            for (i = 0; i < 0x1066; i++) {
                if ((i & 31) == 0) {
                    v = ~BitConverter.ToUInt32(data, (count + offset));
                    count += 4;
                }
                val ^= v & 1;
                v >>= 1;
                if ((val & 1) != 0)
                    val ^= 0x6954559;
                val >>= 1;
            }
            val = ~val;
            return new[]
                       {
                           (byte) (val << 6), (byte) ((val >> 2) & 0xFF), (byte) ((val >> 10) & 0xFF),
                           (byte) ((val >> 18) & 0xFF)
                       };
        }

        internal static bool CheckPageECD(ref byte[] data, int offset) {
            var actual = new byte[4];
            var calculated = CalculateECD(ref data, offset);
            Buffer.BlockCopy(data, offset + 524, actual, 0, 4);
            return (calculated[0] == actual[0] && calculated[1] == actual[1] && calculated[2] == actual[2] &&
                    calculated[3] == actual[3]);
        }
    }
}
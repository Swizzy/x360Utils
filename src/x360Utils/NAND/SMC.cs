namespace x360Utils.NAND {
    using System;

    internal class SMC {
        public string GetVersion(ref byte[] data) {
            if(data == null)
                throw new ArgumentNullException("data");
            if(data.Length < 0x102)
                throw new ArgumentOutOfRangeException("data");
            return string.Format("{0}.{1} ({2}.{3:D2})", (data[0x100] & 0xF0) >> 0x4, data[0x100] & 0x0F, data[0x101], data[0x102]);
        }
    }
}
namespace x360Utils.NAND {
    using System;
    using System.IO;

    public class MobileEntry {
        public readonly byte MobileType;
        public readonly long Offset;
        public readonly int Size;
        public readonly long Version;
        private readonly long _rawOffset;

        internal MobileEntry(long offset, ref NANDSpare.MetaData meta) {
            Offset = offset;
            _rawOffset = (offset / 0x200) * 0x210;
            Version = NANDSpare.GetFsSequence(ref meta);
            MobileType = NANDSpare.GetBlockType(ref meta);
            Size = NANDSpare.GetFsSize(ref meta);
        }

        internal MobileEntry(long offset, long version, int size, byte mobileType) {
            Offset = offset;
            _rawOffset = offset;
            Version = version;
            MobileType = mobileType;
            Size = size;
        }

        public override string ToString() {
            return _rawOffset != Offset
                       ? string.Format("MobileEntry @ 0x{0:X} (0x{1:X} [0x{2:X}]) Version: {3} Type: 0x{4:X} (Mobile{5}.dat) Size: 0x{6:X}", Offset, _rawOffset, _rawOffset + 0x200, Version, MobileType,
                                       Convert.ToChar(MobileType + 0x11), Size)
                       : string.Format("MobileEntry @ 0x{0:X} Version: {1} Type: 0x{2:X} (Mobile{3}.dat) Size: 0x{4:X}", Offset, Version, MobileType, Convert.ToChar(MobileType + 0x11), Size);
        }

        public byte[] GetData(ref NANDReader reader) {
            reader.Seek(Offset, SeekOrigin.Begin);
            return reader.ReadBytes(Size);
        }
    }
}
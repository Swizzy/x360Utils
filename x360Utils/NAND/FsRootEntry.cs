namespace x360Utils.NAND.x360Utils.NAND {
    using System.IO;

    public class FsRootEntry {
        public readonly long Offset;
        public readonly long Version;
        private readonly long _rawOffset;

        public FsRootEntry(long offset, long version, bool isMmc = false) {
            Offset = offset;
            _rawOffset = !isMmc ? (offset / 0x200) * 0x210 : offset;
            Version = version;
        }

        public override string ToString() {
            return _rawOffset != Offset ? string.Format("FSRootEntry @ 0x{0:X} (0x{1:X}) Version: {2}", Offset, _rawOffset, Version) : string.Format("FSRootEntry @ 0x{0:X}", Offset);
        }

        public byte[] GetBlock(ref NANDReader reader) {
            reader.Seek(Offset, SeekOrigin.Begin);
            return reader.ReadBytes(0x4000);
        }
    }
}
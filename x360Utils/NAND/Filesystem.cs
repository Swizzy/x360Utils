namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using x360Utils.Common;

    public class Filesystem {
        public readonly FsRootEntry FsRoot;
        public readonly MobileEntry[] MobileEntries;
        private readonly List<FsRootEntry> _fsRootEntries = new List<FsRootEntry>();
        private readonly List<MobileEntry> _mobileEntries = new List<MobileEntry>();
        private readonly NANDReader _reader;

        public Filesystem(ref NANDReader reader) {
            _reader = reader;
            ScanForFsRootAndMobile();
            FsRoot = FindLatestFsRoot();
            MobileEntries = FindLatestMobiles();
        }

        private void ScanForFsRootAndMobile() {
            var mobiles = new List<MobileEntry>();
            var fsroots = new List<FsRootEntry>();
            if(!_reader.HasSpare) {
                #region MMC

                if(_reader.Length < 0x2FF0000)
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
                _reader.Seek(0x2FE8018, SeekOrigin.Begin); // Seek to MMC Anchor number offset
                var ver1 = BitOperations.Swap(BitConverter.ToUInt32(_reader.ReadBytes(4), 0));
                _reader.Seek(0x2FEC018, SeekOrigin.Begin); // Seek to MMC Anchor number offset
                var ver2 = BitOperations.Swap(BitConverter.ToUInt32(_reader.ReadBytes(4), 0));
                if(ver1 == 0 || ver2 == 0)
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
                _reader.Seek(ver1 > ver2 ? 0x2FE8000 : 0x2FEC000, SeekOrigin.Begin); // Seek to MMC Anchor Block Offset
                var buf = _reader.ReadBytes(0x4000); // We want the first anchor buffer
                fsroots.Add(new FsRootEntry(GetMmcMobileBlock(ref buf, 0) * 0x4000, 0, true));
                for(byte i = 0x31; i < 0x3F; i++) {
                    var size = GetMmcMobileSize(ref buf, i);
                    mobiles.Add(new MobileEntry(GetMmcMobileBlock(ref buf, i) * 0x4000, 0, size > 0 ? size : 0x4000, i));
                }

                #endregion
            }
            else {
                #region NAND

                var maximumOffset = BitOperations.GetSmallest(_reader.BaseStream.Length, 0x4200000); // Only read the filesystem area of BB NANDs (for faster processing)
                _reader.BaseStream.Seek(0x8600, SeekOrigin.Begin); // Seek to Page 0 on Block 3 (SB) Nothing before this will be valid FSRoot...
                for(; _reader.BaseStream.Position < maximumOffset - 0x10;) {
                    var meta = new Meta.MetaData(_reader);
                    if(!Meta.IsFsRootPage(meta))
                        continue;
                    Main.SendInfo(Main.VerbosityLevels.Debug, "FSRoot found @ 0x{0:X} Version: {1}{2}", _reader.Position - 0x200, Meta.GetFsSequence(meta));
                    fsroots.Add(new FsRootEntry(_reader.Position - 0x200, Meta.GetFsSequence(meta)));
                }

                #region Mobile

                _reader.BaseStream.Seek(0x8600, SeekOrigin.Begin); //Seek to block 3 page 0 on small block
                for(; _reader.BaseStream.Position < maximumOffset - 0x10;) {
                    var meta = new Meta.MetaData(_reader);
                    if(Meta.IsFsRootPage(meta)) {
                        _reader.BaseStream.Seek(0x41f0, SeekOrigin.Current); // Seek to the next small block
                        continue; // Skip this one
                    }

                    if(Meta.IsMobilePage(meta)) {
                        Main.SendInfo(Main.VerbosityLevels.Debug, "Mobile found @ 0x{0:X} Version: {1}{2}", _reader.Position - 0x200, Meta.GetFsSequence(meta));
                        mobiles.Add(new MobileEntry(_reader.Position - 0x200, ref meta));
                        var size = Meta.GetFsSize(meta);
                        _reader.BaseStream.Seek(size / 0x200 * 0x210 - 0x10, SeekOrigin.Current);
                        if(size % 0x200 > 0) // There's data still to be saved...
                            _reader.BaseStream.Seek(0x210, SeekOrigin.Current); // Seek 1 page
                        while(_reader.Position % 0x800 > 0) // We want to have an even 4 pages!
                            _reader.BaseStream.Seek(0x210, SeekOrigin.Current); // Seek 1 page
                    }
                    else
                        _reader.BaseStream.Seek(0x830, SeekOrigin.Current); // Skip 4 pages
                }

                #endregion

                #endregion
            }
        }

        public MobileEntry[] GetAllMobileEntries() { return _mobileEntries.ToArray(); }

        public FsRootEntry[] GetAllFsRootEntries() { return _fsRootEntries.ToArray(); }

        private MobileEntry[] FindLatestMobiles() {
            var list = new List<MobileEntry>();
            foreach(var mobileEntry in _mobileEntries) {
                if(list.Count > 0) {
                    for(var i = 0; i < list.Count; i++) {
                        if(mobileEntry.MobileType != list[i].MobileType || mobileEntry.Version < list[i].Version)
                            continue;
                        list.RemoveAt(i);
                        list.Add(mobileEntry);
                    }
                }
                if(list.Count > 0) {
                    var addit = true;
                    for(var i = 0; i < list.Count; i++) {
                        if(mobileEntry.MobileType != list[i].MobileType)
                            continue;
                        addit = false;
                        break;
                    }
                    if(addit)
                        list.Add(mobileEntry);
                }
                else
                    list.Add(mobileEntry);
            }
            var tmp = list.ToArray();
            list.Clear();
            foreach(var mobileEntry in tmp) {
                if(mobileEntry.Offset != 0)
                    list.Add(mobileEntry);
            }
            return list.ToArray();
        }

        private FsRootEntry FindLatestFsRoot() {
            var ret = new FsRootEntry(0, 0, true);
            foreach(var fsRootEntry in _fsRootEntries) {
                if(fsRootEntry.Version >= ret.Version)
                    ret = fsRootEntry;
            }
            return ret;
        }

        private static ushort GetMmcMobileBlock(ref byte[] data, byte mobileType) { return BitOperations.Swap(BitConverter.ToUInt16(data, 0x1C + (mobileType * 0x4))); }

        private static ushort GetMmcMobileSize(ref byte[] data, byte mobileType) { return BitOperations.Swap(BitConverter.ToUInt16(data, 0x1E + (mobileType * 0x4))); }

        private static long GetBaseBlockForMeta2(ref NANDReader reader, FsRootEntry fsRoot) {
            reader.BaseStream.Seek(fsRoot.Offset / 0x200 * 0x210 + 0x200, SeekOrigin.Begin);
            var meta = new Meta.MetaData(reader);
            var reserved = 0x1E0;
            reserved -= meta.FsPageCount;
            reserved -= meta.FsSize0 << 2;
            return reserved * 8;
        }

        public FileSystemEntry[] ParseFileSystem(FsRootEntry fsRoot) {
            var ret = new List<FileSystemEntry>();
            _reader.Seek(fsRoot.Offset, SeekOrigin.Begin);
            var bitmap = new byte[0x2000];
            var fsinfo = new byte[0x2000];
            for(var i = 0; i < bitmap.Length / 0x200; i++) {
                var buf = _reader.ReadBytes(0x200);
                Buffer.BlockCopy(buf, 0, bitmap, i * 0x200, buf.Length);
                buf = _reader.ReadBytes(0x200);
                Buffer.BlockCopy(buf, 0, fsinfo, i * 0x200, buf.Length);
            }

            if(_reader.MetaType == Meta.MetaTypes.MetaType0 || _reader.MetaType == Meta.MetaTypes.MetaType1 || _reader.MetaType == Meta.MetaTypes.MetaType2 ||
               _reader.MetaType == Meta.MetaTypes.MetaTypeNone) {
                for(var i = 0; i < fsinfo.Length / 0x20; i++) {
                    var blocks = new List<long>();
                    var name = Encoding.ASCII.GetString(fsinfo, i * 0x20, 0x16);
                    var start = BitOperations.Swap(BitConverter.ToUInt16(fsinfo, i * 0x20 + 0x16));
                    var size = BitOperations.Swap(BitConverter.ToUInt32(fsinfo, i * 0x20 + 0x18));
                    var timestamp = BitOperations.Swap(BitConverter.ToUInt32(fsinfo, i * 0x20 + 0x1C));
                    blocks.Add(start); // Always add the first block!
                    while(true) {
                        start = BitOperations.Swap(BitConverter.ToUInt16(bitmap, start * 2));
                        //if(start == 0x1FFF || start == 0x1FFE)
                        if(start * 2 > bitmap.Length + 2)
                            break;
                        blocks.Add(start);
                    }
                    if(name.StartsWith("\0") || name.StartsWith("\x5"))
                        continue; // Ignore empty and/or deleted entries
                    if(blocks.Count > 0) {
                        if(blocks[0] == 0 || size == 0) // ignore entries with no offset / size
                            continue;
                    }
                    ret.Add(new FileSystemEntry(name.Substring(0, name.IndexOf('\0')), size, timestamp, blocks.ToArray(), fsRoot));
                }
            }
            else
                throw new NotSupportedException();
            return ret.ToArray();
        }

        public class FileSystemEntry {
            public readonly long[] Blocks;
            public readonly string Filename;
            public readonly uint Size;
            public readonly uint TimeStamp;
            private readonly FsRootEntry _fsRoot;

            public FileSystemEntry(string filename, uint size, uint timeStamp, long[] blocks, FsRootEntry fsRoot) {
                Blocks = blocks;
                Filename = filename;
                Size = size;
                TimeStamp = timeStamp;
                _fsRoot = fsRoot;
            }

            public DateTime DateTime { get { return DateTimeUtils.DosTimeStampToDateTime(TimeStamp); } }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.AppendFormat("FSEntry: {0} Size: 0x{1:X} Timestamp: 0x{2:X} ({3}) Blocks: 0x{4:X}", Filename, Size, TimeStamp, DateTime, Blocks[0]);
                if(Blocks.Length <= 1)
                    return sb.ToString();
                for(var i = 1; i < Blocks.Length; i++)
                    sb.AppendFormat(" -> 0x{0:X}", Blocks[i]);
                return sb.ToString();
            }

            public byte[] GetData(ref NANDReader reader) {
                var ret = new List<byte>();
                var left = (int)Size;
                var baseBlock = reader.MetaType != Meta.MetaTypes.MetaType2 ? 0 : GetBaseBlockForMeta2(ref reader, _fsRoot);
                foreach(var offset in Blocks) {
                    reader.Lba = (ushort)(baseBlock + offset);
                    ret.AddRange(reader.ReadBytes(BitOperations.GetSmallest(left, 0x4000)));
                    left -= BitOperations.GetSmallest(left, 0x4000);
                }
                return ret.ToArray();
            }

            public void ExtractToFile(ref NANDReader reader, string filename) {
                using(var writer = new BinaryWriter(File.OpenWrite(filename))) {
                    var left = (int)Size;
                    var baseBlock = reader.MetaType != Meta.MetaTypes.MetaType2 ? 0 : GetBaseBlockForMeta2(ref reader, _fsRoot);
                    foreach(var offset in Blocks) {
                        reader.Lba = (ushort)(baseBlock + offset);
                        writer.Write(reader.ReadBytes(BitOperations.GetSmallest(left, 0x4000)));
                        left -= BitOperations.GetSmallest(left, 0x4000);
                    }
                }
                File.SetCreationTime(filename, DateTime);
                File.SetLastAccessTime(filename, DateTime);
                File.SetLastWriteTime(filename, DateTime);
            }
        }

        public struct FsRootEntry {
            public readonly long Offset;
            public readonly long Version;
            private readonly long _rawOffset;

            public FsRootEntry(long offset, long version, bool isMmc = false): this() {
                Offset = offset;
                Version = version;
                _rawOffset = !isMmc ? (offset / 0x200) * 0x210 : offset;
            }

            public override string ToString() {
                return _rawOffset != Offset ? string.Format("FSRootEntry @ 0x{0:X} (0x{1:X}) Version: {2}", Offset, _rawOffset, Version) : string.Format("FSRootEntry @ 0x{0:X}", Offset);
            }

            public byte[] GetBlock(ref NANDReader reader) {
                reader.BaseStream.Seek(_rawOffset, SeekOrigin.Begin);
                return reader.ReadBytes(0x4000);
            }
        }

        public class MobileEntry {
            public readonly byte MobileType;
            public readonly long Offset;
            public readonly int Size;
            public readonly long Version;
            private readonly long _rawOffset;

            internal MobileEntry(long offset, ref Meta.MetaData meta) {
                Offset = offset;
                _rawOffset = (offset / 0x200) * 0x210;
                Version = Meta.GetFsSequence(meta);
                MobileType = meta.FsBlockType;
                Size = Meta.GetFsSize(meta);
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
                           ? string.Format("MobileEntry @ 0x{0:X} (0x{1:X} [0x{2:X}]) Version: {3} Type: 0x{4:X} (Mobile{5}.dat) Size: 0x{6:X}", Offset, _rawOffset, _rawOffset + 0x200, Version,
                                           MobileType, Convert.ToChar(MobileType + 0x11), Size)
                           : string.Format("MobileEntry @ 0x{0:X} Version: {1} Type: 0x{2:X} (Mobile{3}.dat) Size: 0x{4:X}", Offset, Version, MobileType, Convert.ToChar(MobileType + 0x11), Size);
            }

            public byte[] GetData(ref NANDReader reader) {
                reader.Seek(Offset, SeekOrigin.Begin);
                return reader.ReadBytes(Size);
            }
        }
    }
}
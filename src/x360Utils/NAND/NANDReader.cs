namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using x360Utils.Common;

    public sealed class NANDReader: Stream {
        public readonly List<FsRootEntry> FsRootEntries = new List<FsRootEntry>();
        public readonly bool HasSpare;
        public readonly NANDSpare.MetaType MetaType;
        public readonly List<MobileEntry> MobileEntries = new List<MobileEntry>();
        private readonly List<long> _badBlocks = new List<long>();
        private readonly BinaryReader _binaryReader;
        private readonly bool _doSendPosition;
        private bool _forcedSb;

        public NANDReader(string file) {
            Debug.SendDebug("Creating NANDReader for: {0}", file);
            _binaryReader = new BinaryReader(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            if(!VerifyMagic())
                throw new Exception("Bad Magic");
            if(Main.VerifyVerbosityLevel(1))
                Main.SendInfo("\r\nChecking for spare... ");
            HasSpare = CheckForSpare();
            if(HasSpare) {
                if (Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("Image has Spare...");
                Main.SendMaxBlocksChanged((int)(_binaryReader.BaseStream.Length / 0x4200));
                _doSendPosition = true;
                if(Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("\r\nChecking for MetaType...");
                MetaType = NANDSpare.DetectSpareType(this);
                if(Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("\r\nMetaType: {0}\r\n", MetaType);
                if (Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("Checking for bad blocks...");
                try {
                    FindBadBlocks();
                }
                catch(X360UtilsException ex) {
                    if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataNotFound)
                        throw;
                }
            }
            else {
                if (Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("Image does NOT have Spare...");
                if(Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("\r\n");
                Main.SendMaxBlocksChanged((int)(_binaryReader.BaseStream.Length / 0x4000));
                _doSendPosition = true;
                MetaType = NANDSpare.MetaType.MetaTypeNone;
            }
        }

        #region Overrides of Stream

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return _binaryReader.BaseStream.CanSeek; } }

        public override bool CanWrite { get { return false; } }

        public override long Length {
            get {
                if(!HasSpare)
                    return _binaryReader.BaseStream.Length;
                return (_binaryReader.BaseStream.Length / 0x210) * 0x200;
            }
        }

        public override long Position { get { return !HasSpare ? _binaryReader.BaseStream.Position : (_binaryReader.BaseStream.Position / 0x210) * 0x200; } set { Seek(value, SeekOrigin.Begin); } }

        public override void Flush() { throw new NotSupportedException(); }

        public override long Seek(long offset, SeekOrigin origin) {
            offset = HasSpare ? ((offset / 0x200) * 0x210) + offset % 0x200 : offset;
            Debug.SendDebug("Old position: 0x{0:X}", _binaryReader.BaseStream.Position);
            Debug.SendDebug("Seeking to offset: 0x{0:X} origin: {1}", offset, origin);
            var ret = _binaryReader.BaseStream.Seek(offset, origin);
            Debug.SendDebug("New position: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(_doSendPosition)
                Main.SendReaderBlock(Position);
            return ret;
        }

        public override void SetLength(long value) { throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int index, int count) {
            Debug.SendDebug("Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(!HasSpare) {
                if(_doSendPosition)
                    Main.SendReaderBlock(Position + count);
                return _binaryReader.Read(buffer, index, count);
            }
            if(_doSendPosition)
                Main.SendReaderBlock(Position + count);
            var pos = (int)_binaryReader.BaseStream.Position % 0x210;
            int size;
            if(pos != 0) {
                size = (0x200 - pos);
                if(size > count)
                    size = count;
                pos = _binaryReader.Read(buffer, index, size);
                if(size == count)
                    return pos;
            }
            while(pos < count) {
                size = count - pos < 0x200 ? count - pos : 0x200;
                pos += _binaryReader.Read(buffer, pos + index, size);
                Seek(0x10, SeekOrigin.Current);
            }
            return pos;
        }

        public new byte ReadByte() {
            if(HasSpare && _binaryReader.BaseStream.Position % 0x210 != 0)
                RawSeek(0x10, SeekOrigin.Current);
            if(_doSendPosition)
                Main.SendReaderBlock(Position + 1);
            return _binaryReader.ReadByte();
        }

        public byte[] ReadBytes(int count) {
            Debug.SendDebug("Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(!HasSpare) {
                if(_doSendPosition)
                    Main.SendReaderBlock(Position + count);
                return _binaryReader.ReadBytes(count);
            }
            if(_doSendPosition)
                Main.SendReaderBlock(Position + count);
            var buffer = new byte[count];
            var pos = (int)_binaryReader.BaseStream.Position % 0x210;
            int size, index = 0;
            if(pos != 0) {
                size = (0x200 - pos);
                if(size > count)
                    size = count;
                index += Read(buffer, index, size);
                if(size == count)
                    return buffer;
            }
            while(index < count) {
                size = count - index < 0x200 ? count - index : 0x200;
                index += Read(buffer, index, size);
            }
            return buffer;
        }

        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        public override void WriteByte(byte value) { throw new NotSupportedException(); }

        public new void Close() { _binaryReader.Close(); }

        #endregion Overrides of Stream

        public FsRootEntry FsRoot { get; private set; }

        public long RawLength { get { return _binaryReader.BaseStream.Length; } }

        public long RawPosition { get { return _binaryReader.BaseStream.Position; } set { RawSeek(value, SeekOrigin.Begin); } }

        public MobileEntry[] MobileArray { get; private set; }

        public void SeekToLbaEx(uint lba) {
            if(_badBlocks.Contains(lba)) {
                var block = MetaType == NANDSpare.MetaType.MetaType2 ? 0xFFF : 0x3FF;
                while(true) {
                    NANDSpare.MetaData meta;
                    switch(MetaType) {
                        case NANDSpare.MetaType.MetaType0:
                        case NANDSpare.MetaType.MetaType1:
                            Seek(block * 0x4000, SeekOrigin.Begin);
                            RawSeek(0x200, SeekOrigin.Current);
                            meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                            if(NANDSpare.GetLba(ref meta) == lba) {
                                Seek(block * 0x4000, SeekOrigin.Begin);
                                return;
                            }
                            break;
                        case NANDSpare.MetaType.MetaType2:
                            Seek(block * 0x4000, SeekOrigin.Begin);
                            RawSeek(0x200, SeekOrigin.Current);
                            meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                            if(NANDSpare.GetLba(ref meta) == lba / 8) {
                                Seek(block * 0x4000 + ((lba % 8) * 0x4000), SeekOrigin.Begin);
                                return;
                            }
                            break;
                        default:
                            Seek(lba * 0x4000, SeekOrigin.Begin);
                            return;
                    }
                    block--;
                }
            }
            Seek(MetaType == NANDSpare.MetaType.MetaType2 ? ((lba / 8) * 0x20000) + ((lba % 8) * 0x4000) : lba * 0x4000, SeekOrigin.Begin);
        }

        public void SeekToLba(uint lba) {
            if(_badBlocks.Contains(lba)) {
                var block = 0;
                while(true) {
                    NANDSpare.MetaData meta;
                    switch(MetaType) {
                        case NANDSpare.MetaType.MetaType0:
                        case NANDSpare.MetaType.MetaType1:
                            Seek((lba * 0x4000) - block * 0x4000, SeekOrigin.Begin);
                            RawSeek(0x200, SeekOrigin.Current);
                            meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                            if(NANDSpare.GetLba(ref meta) == lba) {
                                Seek((lba * 0x4000) - block * 0x4000, SeekOrigin.Begin);
                                return;
                            }
                            break;
                        case NANDSpare.MetaType.MetaType2:
                            Seek((lba * 0x20000) - block * 0x20000, SeekOrigin.Begin);
                            RawSeek(0x200, SeekOrigin.Current);
                            meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                            if(NANDSpare.GetLba(ref meta) == lba) {
                                Seek((lba * 0x20000) - block * 0x20000, SeekOrigin.Begin);
                                return;
                            }
                            break;
                        default:
                            Seek(lba * 0x4000, SeekOrigin.Begin);
                            return;
                    }
                    block++;
                }
            }
            Seek(lba * (MetaType != NANDSpare.MetaType.MetaType2 ? 0x4000 : 0x20000), SeekOrigin.Begin);
        }

        private bool CheckForSpare() {
            RawSeek(0, SeekOrigin.Begin);
            var tmp = _binaryReader.ReadBytes(0x630);
            RawSeek(0, SeekOrigin.Begin);
            var ret = true;
            for(var i = 0; i < tmp.Length; i += 0x210) {
                if(!NANDSpare.CheckPageEcd(ref tmp, i))
                    ret = false;
            }
            return ret;
        }

        private bool VerifyMagic() {
            if(Main.VerifyVerbosityLevel(1))
                Main.SendInfo("\r\nChecking Magic bytes... ");
            RawSeek(0, SeekOrigin.Begin);
            var tmp = _binaryReader.ReadBytes(2);
            Debug.SendDebug("Restoring position...");
            RawSeek(0, SeekOrigin.Begin);
            var ret = (tmp[0] == 0xFF && tmp[1] == 0x4F);
            if(Main.VerifyVerbosityLevel(1)) {
                if(ret)
                    Main.SendInfo("OK!");
                else
                    Main.SendInfo("Failed! (Expected: 0xFF4F but got: 0x{0:X2}{1:X2}", tmp[0], tmp[1]);
            }
            return ret;
        }

        public void ScanForFsRootAndMobile() {
            if(FsRootEntries.Count > 0)
                return;
            if(!HasSpare) {
                #region MMC (No Spare)

                if(Length < 0x2FF0000)
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
                Seek(0x2FE8018, SeekOrigin.Begin); // Seek to MMC Anchor number offset
                var ver1 = BitOperations.Swap(BitConverter.ToUInt32(ReadBytes(4), 0));
                Seek(0x2FEC018, SeekOrigin.Begin); // Seek to MMC Anchor number offset
                var ver2 = BitOperations.Swap(BitConverter.ToUInt32(ReadBytes(4), 0));
                if(ver1 == 0 || ver2 == 0)
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
                Seek(ver1 > ver2 ? 0x2FE8000 : 0x2FEC000, SeekOrigin.Begin); // Seek to MMC Anchor Block Offset
                var buf = ReadBytes(0x4000); // We want the first anchor buffer
                FsRootEntries.Add(new FsRootEntry(NANDSpare.GetMmcMobileBlock(ref buf, 0) * 0x4000, 0, true));
                for(byte i = 0x31; i < 0x3F; i++) {
                    var size = NANDSpare.GetMmcMobileSize(ref buf, i);
                    MobileEntries.Add(new MobileEntry(NANDSpare.GetMmcMobileBlock(ref buf, i) * 0x4000, 0, size > 0 ? size : 0x4000, i));
                }

                #endregion
            }
            else {
                #region NAND (With Spare)

                var maximumOffset = BitOperations.GetSmallest(_binaryReader.BaseStream.Length, 0x4200000); // Only read the filesystem area of BB NANDs (for faster processing)

                #region FSRoot

                RawSeek(0x8600, SeekOrigin.Begin); //Seek to block 3 page 0 on small block
                for(; _binaryReader.BaseStream.Position < maximumOffset - 0x10;) {
                    var meta = NANDSpare.GetMetaData(_binaryReader.ReadBytes(0x10), MetaType);
                    if(NANDSpare.PageIsFsRoot(ref meta)) {
                        Debug.SendDebug("FSRoot found @ 0x{0:X} version: {1}", Position - 0x200, NANDSpare.GetFsSequence(ref meta));
                        FsRootEntries.Add(new FsRootEntry(Position - 0x200, NANDSpare.GetFsSequence(ref meta)));
                        RawSeek(0x41f0, SeekOrigin.Current); // Seek to the next small block
                    }
                    else {
                        if(NANDSpare.IsMobilePage(ref meta)) {
                            Debug.SendDebug("Mobile found @ 0x{0:X} version: {1}", Position - 0x200, NANDSpare.GetFsSequence(ref meta));
                            MobileEntries.Add(new MobileEntry(Position - 0x200, ref meta));
                        }
                        for(var i = 0; i < 31; i++) {
                            RawSeek(0x200, SeekOrigin.Current);
                            meta = NANDSpare.GetMetaData(_binaryReader.ReadBytes(0x10), MetaType);
                            if(NANDSpare.IsMobilePage(ref meta)) {
                                Debug.SendDebug("Mobile found @ 0x{0:X} version: {1}", Position - 0x200, NANDSpare.GetFsFreePages(ref meta));
                                MobileEntries.Add(new MobileEntry(Position - 0x200, ref meta));
                            }
                        }
                        RawSeek(0x200, SeekOrigin.Current);
                    }
                }

                #endregion

                #region Mobile*.dat

                RawSeek(0x8600, SeekOrigin.Begin); //Seek to block 3 page 0 on small block
                for(; _binaryReader.BaseStream.Position < maximumOffset - 0x10;) {
                    var meta = NANDSpare.GetMetaData(_binaryReader.ReadBytes(0x10), MetaType);
                    if(NANDSpare.PageIsFsRoot(ref meta)) {
                        RawSeek(0x41f0, SeekOrigin.Current); // Seek to the next small block
                        continue; // Skip this one
                    }

                    if(NANDSpare.IsMobilePage(ref meta)) {
                        Debug.SendDebug("Mobile found @ 0x{0:X} version: {1}", Position - 0x200, NANDSpare.GetFsSequence(ref meta));
                        MobileEntries.Add(new MobileEntry(Position - 0x200, ref meta));
                        var size = NANDSpare.GetFsSize(ref meta);
                        RawSeek(size / 0x200 * 0x210 - 0x10, SeekOrigin.Current);
                        if(size % 0x200 > 0) // There's data still to be saved...
                            RawSeek(0x210, SeekOrigin.Current); // Seek 1 page
                        while(Position % 0x800 > 0) // We want to have an even 4 pages!
                            RawSeek(0x210, SeekOrigin.Current); // Seek 1 page
                    }
                    else
                        RawSeek(0x830, SeekOrigin.Current); // Skip 4 pages
                }

                #endregion

                #endregion
            }
            RawSeek(0, SeekOrigin.Begin); //Reset the stream

            if(FsRootEntries.Count <= 0)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
            FindLatestFsRoot();
            FillMobileArray();
        }

        private void FindLatestFsRoot() {
            foreach(var fsRootEntry in FsRootEntries) {
                if(FsRoot == null)
                    FsRoot = fsRootEntry;
                else if(fsRootEntry.Version >= FsRoot.Version)
                    FsRoot = fsRootEntry;
            }
        }

        private void FillMobileArray() {
            var list = new List<MobileEntry>();
            foreach(var mobileEntry in MobileEntries) {
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
            MobileArray = list.ToArray();
            list.Clear();
            foreach(var mobileEntry in MobileArray) {
                if(mobileEntry.Offset != 0)
                    list.Add(mobileEntry);
            }
            MobileArray = list.ToArray();
        }

        public long[] FindBadBlocks(bool forceSb = false) {
            if(!HasSpare || MetaType == NANDSpare.MetaType.MetaTypeUnInitialized)
                throw new NotSupportedException();
            if(_forcedSb && !forceSb || !_forcedSb && forceSb)
                _badBlocks.Clear();
            if(_badBlocks.Count > 0)
                return _badBlocks.ToArray();
            _forcedSb = forceSb;
            _badBlocks.Clear();
            RawSeek(0x200, SeekOrigin.Begin); // Seek to first page spare data...
            var totalBlocks = Length / (MetaType == NANDSpare.MetaType.MetaType2 ? (!forceSb ? 0x20000 : 0x4000) : 0x4000);
            for(var block = 0; block < totalBlocks; block++) {
                var spare = RawReadBytes(0x10);
                if(NANDSpare.CheckIsBadBlockSpare(ref spare, MetaType)) {
                    if(Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("{1}BadBlock Marker detected @ block 0x{0:X}", block, Environment.NewLine);
                    _badBlocks.Add(block);
                }
                RawSeek(MetaType == NANDSpare.MetaType.MetaType2 ? (!forceSb ? 0x20FF0 : 0x41F0) : 0x41F0, SeekOrigin.Current);
            }
            if (Main.VerifyVerbosityLevel(1))
                Main.SendInfo(Environment.NewLine);
            if(_badBlocks.Count > 0)
                return _badBlocks.ToArray();
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
        }

        public long RawSeek(long offset, SeekOrigin origin) {
            Debug.SendDebug("[RAW]Old position: 0x{0:X}", _binaryReader.BaseStream.Position);
            Debug.SendDebug("[RAW]Seeking to offset: 0x{0:X} origin: {1}", offset, origin);
            var ret = _binaryReader.BaseStream.Seek(offset, origin);
            Debug.SendDebug("[RAW]New position: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(_doSendPosition)
                Main.SendReaderBlock(Position);
            return ret;
        }

        public byte[] RawReadBytes(int count) {
            Debug.SendDebug("[RAW]Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(_doSendPosition)
                Main.SendReaderBlock(Position + ((count / 0x210) * 0x200));
            return _binaryReader.ReadBytes(count);
        }

        public int RawRead(byte[] buffer, int index, int count) {
            Debug.SendDebug("[RAW]Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(_doSendPosition)
                Main.SendReaderBlock(Position + ((count / 0x210) * 0x200));
            return _binaryReader.Read(buffer, index, count);
        }
    }

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
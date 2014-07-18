namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using x360Utils.Common;

    public sealed class SmartNANDReader: Stream {
        private readonly List<uint> _badBlocks = new List<uint>();
        private readonly BinaryReader _binaryReader;
        private readonly bool _doSendPosition;
        private uint[] _badBlocks2;
        private bool _badBlocksScanned, _badBlocksScanned2;

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

        public override long Position {
            get {
                var offset = _binaryReader.BaseStream.Position;
                if(HasSpare)
                    offset = CalculateLba(offset) * 0x4200 + CalculatePage(CalculateLbaOffset(offset)) + CalculatePageOffset(CalculateLbaOffset(offset));
                return offset;
            }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override void Flush() { throw new NotSupportedException(); }

        public override long Seek(long offset, SeekOrigin origin) {
            Debug.SendDebug("Old position: 0x{0:X}", _binaryReader.BaseStream.Position);
            Debug.SendDebug("Seeking to offset: 0x{0:X} (LBA: {1}) origin: {2}", offset, CalculateLba(offset), origin);
            Lba = CalculateLba(offset);
            if(MetaType == NANDSpare.MetaType.MetaTypeNone)
                RawSeek(offset, origin);
            else if(origin == SeekOrigin.Current) {
                offset += CalculateLbaOffset(Position);
                SeekToSmallBlock(CalculateLba(offset) + Lba);
                if(CalculateLbaOffset(offset) > 0)
                    SeekToLbaOffset(CalculateLbaOffset(offset));
            }
            else if(origin == SeekOrigin.Begin) {
                SeekToSmallBlock(CalculateLba(offset));
                if(CalculateLbaOffset(offset) > 0)
                    SeekToLbaOffset(CalculateLbaOffset(offset));
            }
            else if(origin == SeekOrigin.End) {
                SeekToSmallBlock(LastBlock() - CalculateLba(offset));
                if(CalculateLbaOffset(offset) > 0)
                    SeekToLbaOffset(CalculateLbaOffset(offset) * -1);
            }
            Debug.SendDebug("New position: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(_doSendPosition)
                Main.SendReaderBlock(Position);
            return Position;
        }

        public override void SetLength(long value) { throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int index, int count) {
            Debug.SendDebug("Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            throw new NotImplementedException();
        }

        public override int ReadByte() {
            if (MetaType == NANDSpare.MetaType.MetaTypeNone)
                return _binaryReader.ReadByte();
            var ret = new byte[1];
            Read(ret, 0, 1);
            return ret[0];
        }

        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        public override void WriteByte(byte value) { throw new NotSupportedException(); }

        public override void Close() { _binaryReader.Close(); }

        #endregion Overrides of Stream

        public SmartNANDReader(string file, bool fastScan = false) {
            if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                Main.SendInfo("Creating SmartNANDReader for {0}{1}", file, Environment.NewLine);
            _binaryReader = new BinaryReader(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                Main.SendInfo("Verifying Magic Bytes... ");
            if(!VerifyMagic()) {
                if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                    Main.SendInfo("Failed!{0}", Environment.NewLine);
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.BadMagic);
            }
            if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                Main.SendInfo("OK!{0}", Environment.NewLine);
            if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                Main.SendInfo("Checking for spare... ");
            HasSpare = CheckForSpare();
            if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                Main.SendInfo(HasSpare ? "Spare detected!{0}" : "No Spare detected!{0}", Environment.NewLine);
            if(HasSpare) {
                Main.SendMaxBlocksChanged((int)(_binaryReader.BaseStream.Length / 0x4200));
                _doSendPosition = true;
                if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                    Main.SendInfo("Detecting spare type... ");
                MetaType = NANDSpare.DetectSpareType(this);
                if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                    Main.SendInfo("{0}{1}", MetaType, Environment.NewLine);
                if(fastScan)
                    return;
                if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                    Main.SendInfo("Scanning for FSRoot/Mobile entries");
                ScanForFsRootAndMobiles();
                return;
            }
            Main.SendMaxBlocksChanged((int)(_binaryReader.BaseStream.Length / 0x4000));
            _doSendPosition = true;
            MetaType = NANDSpare.MetaType.MetaTypeNone;
        }

        #region Properties

        public MobileEntry[] MobileEntries { get; private set; }

        public FsRootEntry[] FileSystemEntries { get; private set; }

        public bool HasSpare { get; private set; }

        public NANDSpare.MetaType MetaType { get; private set; }

        public uint Lba { get; private set; }

        public FsRootEntry FsRoot { get; private set; }

        public long RawLength { get { return _binaryReader.BaseStream.Length; } }

        public long RawPosition { get { return _binaryReader.BaseStream.Position; } set { RawSeek(value, SeekOrigin.Begin); } }

        #endregion

        #region Private functions

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

        private void ScanForBadBlocks(bool throwException = true, uint blocksize = 0x4000, uint nextBlock = 0x41F0) {
            if(!HasSpare || MetaType == NANDSpare.MetaType.MetaTypeUnInitialized) {
                if(throwException)
                    throw new NotSupportedException();
                return;
            }
            _badBlocks.Clear();
            RawSeek(0x200, SeekOrigin.Begin); // Seek to first page spare data...
            var tBlocks = Length / blocksize;
            for(uint block = 0; block < tBlocks; block++) {
                var meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                if(NANDSpare.CheckIsBadBlock(meta)) {
                    _badBlocks.Add(block);
                    if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.High))
                        Main.SendInfo("{1}BadBlock Marker detected @ block 0x{0:X}", block, Environment.NewLine);
                }
                RawSeek(nextBlock, SeekOrigin.Current);
            }
            _badBlocksScanned = true;
        }

        private void ScanForFsRootAndMobiles() {
            if(FsRoot != null)
                return;
            var mobiles = new List<MobileEntry>();
            var fsroots = new List<FsRootEntry>();
            if(!HasSpare) {
                #region MMC

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
                fsroots.Add(new FsRootEntry(NANDSpare.GetMmcMobileBlock(ref buf, 0) * 0x4000, 0, true));
                for(byte i = 0x31; i < 0x3F; i++) {
                    var size = NANDSpare.GetMmcMobileSize(ref buf, i);
                    mobiles.Add(new MobileEntry(NANDSpare.GetMmcMobileBlock(ref buf, i) * 0x4000, 0, size > 0 ? size : 0x4000, i));
                }

                #endregion
            }
            else {
                #region NAND

                var maximumOffset = BitOperations.GetSmallest(_binaryReader.BaseStream.Length, 0x4200000); // Only read the filesystem area of BB NANDs (for faster processing)
                RawSeek(0x8600, SeekOrigin.Begin); // Seek to Page 0 on Block 3 (SB) Nothing before this will be valid FSRoot...
                for(; RawPosition < maximumOffset - 0x10;) {
                    var meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                    if(!NANDSpare.PageIsFsRoot(ref meta))
                        continue;
                    if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                        Main.SendInfo("FSRoot found @ 0x{0:X} Version: {1}{2}", Position - 0x200, NANDSpare.GetFsSequence(ref meta));
                    fsroots.Add(new FsRootEntry(Position - 0x200, NANDSpare.GetFsSequence(ref meta)));
                }

                #region Mobile

                RawSeek(0x8600, SeekOrigin.Begin); //Seek to block 3 page 0 on small block
                for(; _binaryReader.BaseStream.Position < maximumOffset - 0x10;) {
                    var meta = NANDSpare.GetMetaData(_binaryReader.ReadBytes(0x10), MetaType);
                    if(NANDSpare.PageIsFsRoot(ref meta)) {
                        RawSeek(0x41f0, SeekOrigin.Current); // Seek to the next small block
                        continue; // Skip this one
                    }

                    if(NANDSpare.IsMobilePage(ref meta)) {
                        if(Main.VerifyVerbosityLevel(Main.VerbosityLevels.Debug))
                            Main.SendInfo("Mobile found @ 0x{0:X} Version: {1}{2}", Position - 0x200, NANDSpare.GetFsSequence(ref meta));
                        mobiles.Add(new MobileEntry(Position - 0x200, ref meta));
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
            FileSystemEntries = fsroots.ToArray();
            FindLatestFsRoot();
            FindLatestMobiles(mobiles);
        }

        private void FindLatestFsRoot() {
            foreach(var fsRootEntry in FileSystemEntries) {
                if(FsRoot == null || fsRootEntry.Version >= FsRoot.Version)
                    FsRoot = fsRootEntry;
            }
        }

        private void FindLatestMobiles(IEnumerable<MobileEntry> mobiles) {
            var list = new List<MobileEntry>();
            foreach(var mobileEntry in mobiles) {
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
            MobileEntries = list.ToArray();
            list.Clear();
            foreach(var mobileEntry in MobileEntries) {
                if(mobileEntry.Offset != 0)
                    list.Add(mobileEntry);
            }
            MobileEntries = list.ToArray();
        }

        private uint CalculateLba(long offset) { return (uint)(offset / 0x4000); }

        private uint CalculateLbaOffset(long offset) { return (uint)(offset % 0x4000); }

        private long CalculatePage(long pageOffset) { return (pageOffset / 0x200) * 0x210; }

        private long CalculatePageOffset(long pageOffset) { return pageOffset % 0x200; }

        private uint LastBlock() { return (uint)(MetaType == NANDSpare.MetaType.MetaType2 ? 0xFFF : 0x3FF); }

        private void SeekToSmallBlock(uint blockLba) {
            if(MetaType == NANDSpare.MetaType.MetaTypeNone) {
                RawSeek(blockLba * 0x4000, SeekOrigin.Begin);
                return;
            }
            if(MetaType == NANDSpare.MetaType.MetaTypeUnInitialized) {
                RawSeek(blockLba * 0x4200, SeekOrigin.Begin);
                return;
            }
            RawSeek(0x200 + (blockLba * 0x4200), SeekOrigin.Begin); // Seek to the first page spare of the block we're looking for
            var meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
            switch(MetaType) {
                case NANDSpare.MetaType.MetaType2:
                    if(NANDSpare.GetLba(ref meta) == blockLba / 8) {
                        RawSeek(blockLba * 0x4200, SeekOrigin.Begin);
                        return;
                    }
                    break;
                default:
                    if(NANDSpare.GetLba(ref meta) == blockLba) {
                        RawSeek(blockLba * 0x4200, SeekOrigin.Begin);
                        return;
                    }
                    break;
            }

            #region Find the block starting from the end...

            var block = LastBlock();
            while(true) {
                RawSeek(0x200 + (block * 0x4200), SeekOrigin.Begin);
                meta = NANDSpare.GetMetaData(RawReadBytes(0x10), MetaType);
                switch(MetaType) {
                    case NANDSpare.MetaType.MetaType2:
                        if(NANDSpare.GetLba(ref meta) == blockLba / 8) {
                            RawSeek(block * 0x4200, SeekOrigin.Begin);
                            return;
                        }
                        break;
                    default:
                        if(NANDSpare.GetLba(ref meta) == blockLba) {
                            RawSeek(block * 0x4200, SeekOrigin.Begin);
                            return;
                        }
                        break;
                }
                block--;
            }

            #endregion
        }

        private void SeekToLbaOffset(long lbaOffset) {
            if(MetaType == NANDSpare.MetaType.MetaTypeNone)
                RawSeek(lbaOffset, SeekOrigin.Current);
            else
                RawSeek(CalculatePage(lbaOffset) + CalculatePageOffset(lbaOffset), SeekOrigin.Current);
        }

        #endregion

        public byte[] ReadBytes(int count) {
            if(MetaType == NANDSpare.MetaType.MetaTypeNone)
                return _binaryReader.ReadBytes(count);
            var buffer = new byte[count];
            Read(buffer, 0, count);
            return buffer;
        }

        public uint[] GetBadBlocks(bool smallBlocks = true) {
            if(smallBlocks && _badBlocksScanned)
                return _badBlocks.ToArray();
            if(smallBlocks || MetaType != NANDSpare.MetaType.MetaType2) {
                ScanForBadBlocks();
                return _badBlocks.ToArray();
            }
            if(_badBlocks2.Length > 0 && _badBlocksScanned2)
                return _badBlocks2;
            if(!_badBlocksScanned)
                ScanForBadBlocks();
            var tmp = _badBlocks.ToArray();
            ScanForBadBlocks(true, 0x20000, 0x20ff0);
            _badBlocksScanned2 = true;
            _badBlocks2 = _badBlocks.ToArray();
            _badBlocks.Clear();
            _badBlocks.AddRange(tmp);
            return _badBlocks2;
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
}
namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;

    public sealed class NANDReader : Stream {
        public readonly bool HasSpare;
        public readonly NANDSpare.MetaType MetaType;
        private readonly List<long> _badBlocks = new List<long>();
        private readonly BinaryReader _binaryReader;
        private readonly List<long> _fsBlocks = new List<long>();
        private bool _forcedSB;

        public NANDReader(string file) {
            Debug.SendDebug("Creating NANDReader for: {0}", file);
            _binaryReader = new BinaryReader(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            if(!VerifyMagic())
                throw new Exception("Bad Magic");
            if (Main.VerifyVerbosityLevel(1))
                Main.SendInfo("\r\nChecking for spare...");
            HasSpare = CheckForSpare();
            if(HasSpare) {
                if (Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("\r\nChecking for MetaType...");
                MetaType = NANDSpare.DetectSpareType(this);
                if (Main.VerifyVerbosityLevel(1))
                    Main.SendInfo("\r\nMetaType: {0}", MetaType);
                //Main.SendInfo("Checking for bad blocks...");
                //try {
                //    FindBadBlocks();
                //}
                //catch (X360UtilsException ex) {
                //    if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataNotFound)
                //        throw;
                //}
            }
            else
                MetaType = NANDSpare.MetaType.MetaTypeNone;
        }

        #region Overrides of Stream

        public override bool CanRead {
            get { return true; }
        }

        public override bool CanSeek {
            get { return _binaryReader.BaseStream.CanSeek; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override long Length {
            get {
                if(!HasSpare)
                    return _binaryReader.BaseStream.Length;
                return (_binaryReader.BaseStream.Length / 0x210) * 0x200;
            }
        }

        public override long Position {
            get { return !HasSpare ? _binaryReader.BaseStream.Position : (_binaryReader.BaseStream.Position / 0x210) * 0x200; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override void Flush() {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            offset = HasSpare ? ((offset / 0x200) * 0x210) + offset % 0x200 : offset;
            Debug.SendDebug("Old position: 0x{0:X}", _binaryReader.BaseStream.Position);
            Debug.SendDebug("Seeking to offset: 0x{0:X} origin: {1}", offset, origin);
            var ret = _binaryReader.BaseStream.Seek(offset, origin);
            Debug.SendDebug("New position: 0x{0:X}", _binaryReader.BaseStream.Position);
            return ret;
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int index, int count) {
            Debug.SendDebug("Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(!HasSpare)
                return _binaryReader.Read(buffer, index, count);
            var pos = (int) _binaryReader.BaseStream.Position % 0x210;
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
            return _binaryReader.ReadByte();
        }

        public byte[] ReadBytes(int count) {
            Debug.SendDebug("Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            if(!HasSpare)
                return _binaryReader.ReadBytes(count);
            var buffer = new byte[count];
            var pos = (int) _binaryReader.BaseStream.Position % 0x210;
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

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }

        public new void Close() {
            _binaryReader.Close();
        }

        #endregion Overrides of Stream

        public long RawLength {
            get { return _binaryReader.BaseStream.Length; }
        }

        public long RawPosition {
            get { return _binaryReader.BaseStream.Position; }
            set { RawSeek(value, SeekOrigin.Begin); }
        }

        private bool CheckForSpare() {
            RawSeek(0, SeekOrigin.Begin);
            var tmp = _binaryReader.ReadBytes(0x630);
            RawSeek(0, SeekOrigin.Begin);
            var ret = true;
            for(var i = 0; i < tmp.Length; i += 0x210) {
                if(!NANDSpare.CheckPageECD(ref tmp, i))
                    ret = false;
            }
            return ret;
        }

        private bool VerifyMagic() {
            if (Main.VerifyVerbosityLevel(1))
                Main.SendInfo("\r\nChecking Magic bytes...");
            RawSeek(0, SeekOrigin.Begin);
            var tmp = _binaryReader.ReadBytes(2);
            Debug.SendDebug("Restoring position...");
            RawSeek(0, SeekOrigin.Begin);
            return (tmp[0] == 0xFF && tmp[1] == 0x4F);
        }

        public long[] FindFSBlocks() {
            if(!HasSpare)
                throw new NotSupportedException();
            if(_fsBlocks.Count > 0)
                return _fsBlocks.ToArray();
            _fsBlocks.Clear();
            RawSeek(0x8600, SeekOrigin.Begin); //Seek to block 3 page 0 on small block
            for(; _binaryReader.BaseStream.Position < _binaryReader.BaseStream.Length - 0x10;) {
                var tmp = _binaryReader.ReadBytes(0x10);
                if(NANDSpare.PageIsFS(ref tmp))
                    _fsBlocks.Add(Position);
                RawSeek(0x41f0, SeekOrigin.Current); // Seek to the next small block
            }
            if(_fsBlocks.Count > 0)
                return _fsBlocks.ToArray();
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
        }

        public long[] FindBadBlocks(bool forceSB = false) {
            if(!HasSpare || MetaType == NANDSpare.MetaType.MetaTypeUnInitialized)
                throw new NotSupportedException();
            if(_forcedSB && !forceSB || !_forcedSB && forceSB)
                _badBlocks.Clear();
            if(_badBlocks.Count > 0)
                return _badBlocks.ToArray();
            _forcedSB = forceSB;
            _badBlocks.Clear();
            RawSeek(0x200, SeekOrigin.Begin); // Seek to first page spare data...
            var totalBlocks = Length / (MetaType == NANDSpare.MetaType.MetaType2 ? (!forceSB ? 0x20000 : 0x4000) : 0x4000);
            for(var block = 0; block < totalBlocks; block++) {
                var spare = RawReadBytes(0x10);
                if(NANDSpare.CheckIsBadBlockSpare(ref spare, MetaType)) {
                    if (Main.VerifyVerbosityLevel(1))
                        Main.SendInfo("\r\nBadBlock Marker detected @ block 0x{0:X}", block);
                    _badBlocks.Add(block);
                }
                RawSeek(MetaType == NANDSpare.MetaType.MetaType2 ? (!forceSB ? 0x20FF0 : 0x41F0) : 0x41F0, SeekOrigin.Current);
            }
            if(_badBlocks.Count > 0)
                return _badBlocks.ToArray();
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
        }

        public long RawSeek(long offset, SeekOrigin origin) {
            Debug.SendDebug("Old position: 0x{0:X}", _binaryReader.BaseStream.Position);
            Debug.SendDebug("[RAW]Seeking to offset: 0x{0:X} origin: {1}", offset, origin);
            var ret = _binaryReader.BaseStream.Seek(offset, origin);
            Debug.SendDebug("New position: 0x{0:X}", _binaryReader.BaseStream.Position);
            return ret;
        }

        public byte[] RawReadBytes(int count) {
            Debug.SendDebug("[RAW]Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            return _binaryReader.ReadBytes(count);
        }

        public int RawRead(byte[] buffer, int index, int count) {
            Debug.SendDebug("[RAW]Reading @ offset: 0x{0:X}", _binaryReader.BaseStream.Position);
            return _binaryReader.Read(buffer, index, count);
        }
    }
}
#region

using System;
using System.IO;

#endregion

namespace x360Utils.NAND {
    internal class NANDReader : Stream {
        public readonly bool HasSpare;
        private readonly BinaryReader _binaryReader;

        private NANDReader(string file) {
            _binaryReader = new BinaryReader(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read));
            if (!VerifyMagic())
                throw new Exception("Bad Magic");
            HasSpare = CheckForSpare();
        }

        #region Overrides of Stream

        public override bool CanRead {
            get { return true; }
        }

        public override bool CanSeek {
            get { return true; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override long Length {
            get {
                if (!HasSpare)
                    return _binaryReader.BaseStream.Length;
                return (_binaryReader.BaseStream.Length / 0x210) * 0x200;
            }
        }

        public override long Position {
            get {
                return !HasSpare
                           ? _binaryReader.BaseStream.Position
                           : (_binaryReader.BaseStream.Position / 0x210) * 0x200;
            }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override void Flush() {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return _binaryReader.BaseStream.Seek(HasSpare ? (offset / 0x200) * 0x210 : offset, origin);
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (!HasSpare)
                return _binaryReader.Read(buffer, offset, count);
            var pos = (int) _binaryReader.BaseStream.Position % 0x210;
            int size;
            if (pos != 0) {
                size = (0x200 - pos);
                if (size > count)
                    size = count;
                pos = Read(buffer, offset, size);
                if (size == count)
                    return pos;
            }
            while (pos < count) {
                _binaryReader.BaseStream.Seek(0x10, SeekOrigin.Current);
                size = count - pos < 0x200 ? count - pos : 0x200;
                pos += Read(buffer, pos + offset, size);
            }
            return pos;
        }

        public byte[] ReadBytes(int count) {
            if (!HasSpare)
                return _binaryReader.ReadBytes(count);
            var buffer = new byte[count];
            var pos = (int) _binaryReader.BaseStream.Position % 0x210;
            int size;
            if (pos != 0) {
                size = (0x200 - pos);
                if (size > count)
                    size = count;
                pos = Read(buffer, 0, size);
                if (size == count)
                    return buffer;
            }
            while (pos < count) {
                _binaryReader.BaseStream.Seek(0x10, SeekOrigin.Current);
                size = count - pos < 0x200 ? count - pos : 0x200;
                pos += Read(buffer, pos, size);
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

        private bool CheckForSpare() {
            _binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
            var tmp = _binaryReader.ReadBytes(0x630);
            _binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
            var ret = true;
            for (var i = 0; i < tmp.Length; i += 0x210) {
                if (!NANDSpare.CheckPageECD(ref tmp, i))
                    ret = false;
            }
            return ret;
        }

        private bool VerifyMagic() {
            _binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
            var tmp = _binaryReader.ReadBytes(2);
            _binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
            return (tmp[0] == 0xFF && tmp[1] == 0x4F);
        }
    }
}
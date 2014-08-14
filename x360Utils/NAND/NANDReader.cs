namespace x360Utils.NAND {
    using System;
    using System.IO;
    using x360Utils.Common;

    public abstract class NANDReader: IDisposable {
        public NANDReader(string file): this(File.OpenRead(file)) { }

        public NANDReader(Stream input) {
            BaseStream = input;
            Main.SendInfo(Main.VerbosityLevels.Medium, "Checking magic bytes...{0}", Environment.NewLine);
            CheckMagic();
            Main.SendInfo(Main.VerbosityLevels.Medium, "Checking for spare...{0}", Environment.NewLine);
            CheckForMeta();
            if(!HasSpare)
                return;
            Main.SendInfo(Main.VerbosityLevels.Medium, "Checking meta type...{0}", Environment.NewLine);
            MetaType = Meta.DetectSpareType(this);
        }

        public Meta.MetaTypes MetaType { get; internal set; }

        public bool HasSpare { get; internal set; }

        public long Length {
            get {
                if(!HasSpare)
                    return BaseStream.Length;
                return (BaseStream.Length / 0x210) * 0x200;
            }
        }

        public long Position {
            get {
                if(!HasSpare)
                    return BaseStream.Position;
                return (BaseStream.Position / 0x210) * 0x200 + BaseStream.Position % 0x210;
            }
            set { SetPosition(value); }
        }

        public Stream BaseStream { get; private set; }

        public virtual ushort Lba { get { return (ushort)(Position / 0x4000); } set { SetPosition(value * 0x4000); } }

        public void Dispose() { BaseStream.Dispose(); }

        internal void SendBlockChanged(long offset) { Main.SendReaderBlock(offset, (int)(Length / 0x4000)); }

        internal virtual void SetPosition(long offset) { Seek(offset, SeekOrigin.Begin); }

        internal void CheckMagic() {
            BaseStream.Seek(0, SeekOrigin.Begin);
            var buf = new byte[2];
            BaseStream.Read(buf, 0, 2);
            if(buf[0] != 0xFF || buf[1] != 0x4F)
                throw new NANDReaderException(NANDReaderException.ErrorTypes.BadMagic, string.Format("Expected: 0xFF4F Got: 0x{0:X2}{1:X2}", buf[0], buf[1]));
        }

        internal void CheckForMeta() {
            BaseStream.Seek(0, SeekOrigin.Begin);
            var buf = new byte[0x630]; // 3 page buffer
            if(BaseStream.Read(buf, 0, buf.Length) != buf.Length)
                throw new NANDReaderException(NANDReaderException.ErrorTypes.NotEnoughData);
            HasSpare = true; // Let's assume it has spare before we begin...
            for(var i = 0; i < buf.Length; i += 0x210) {
                if(!Meta.CheckPageEcd(ref buf, i))
                    HasSpare = false; // We don't have spare...
            }
        }

        public void Close() { BaseStream.Close(); }

        public virtual int Read(byte[] buffer, int offset, int count) {
            if(!HasSpare)
                return BaseStream.Read(buffer, offset, count);
            var read = 0;
            if(BaseStream.Position % 0x210 > 0) {
                var pageOffset = 0x200 - BaseStream.Position % 0x210;
                var size = (int)BitOperations.GetSmallest(pageOffset, count);
                read = BaseStream.Read(buffer, offset, size);
                offset += size;
                if(size == pageOffset)
                    BaseStream.Seek(0x10, SeekOrigin.Current);
            }
            while(read < count) {
                var size = BitOperations.GetSmallest(0x200, count - read);
                read += BaseStream.Read(buffer, offset, size);
                offset += size;
                if(size == 0x200)
                    BaseStream.Seek(0x10, SeekOrigin.Current);
            }
            SendBlockChanged(Position);
            return read;
        }

        public byte ReadByte() {
            var buf = new byte[1];
            if(Read(buf, 0, 1) == 1)
                return buf[0];
            throw new NANDReaderException(NANDReaderException.ErrorTypes.NotEnoughData);
        }

        public byte[] ReadBytes(int count) {
            var buf = new byte[count];
            if(Read(buf, 0, count) == count)
                return buf;
            throw new NANDReaderException(NANDReaderException.ErrorTypes.NotEnoughData);
        }

        public short ReadInt16() { return (short)BitOperations.Swap(BitConverter.ToUInt16(ReadBytes(2), 0)); }

        public int ReadInt32() { return (int)BitOperations.Swap(BitConverter.ToUInt32(ReadBytes(4), 0)); }

        public long ReadInt64() { return (long)BitOperations.Swap(BitConverter.ToUInt64(ReadBytes(8), 0)); }

        public ushort ReadUInt16() { return BitOperations.Swap(BitConverter.ToUInt16(ReadBytes(2), 0)); }

        public uint ReadUInt32() { return BitOperations.Swap(BitConverter.ToUInt32(ReadBytes(4), 0)); }

        public ulong ReadUInt64() { return BitOperations.Swap(BitConverter.ToUInt64(ReadBytes(8), 0)); }

        public virtual void Seek(long offset, SeekOrigin origin) {
            if(HasSpare)
                offset = ((offset / 0x200) * 0x210) + offset % 0x210;
            SendBlockChanged(offset);
            BaseStream.Seek(offset, origin);
        }
    }
}
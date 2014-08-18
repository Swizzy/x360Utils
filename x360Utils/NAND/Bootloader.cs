namespace x360Utils.NAND {
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using x360Utils.Common;

    public class Bootloader {
        public enum BlTypes {
            Cb,
            Cd,
            Ce,
            Cf,
            Cg
        }

        public readonly long Offset;

        private readonly Bootloader _parent;
        private readonly NANDReader _reader;
        private byte[] _data;

        public Bootloader(NANDReader reader, int slot = 0) {
            _reader = reader;
            Offset = reader.Position;
            Header = reader.ReadBytes(0x10);
            reader.Seek(Offset + Size, SeekOrigin.Begin);
            Slot = slot;
        }

        public Bootloader(Bootloader parent, NANDReader reader, int slot = 0): this(reader, slot) { _parent = parent; }

        public int Slot { get; private set; }

        public bool Encrypted { get; private set; }

        public byte[] Data {
            get {
                if(_data != null)
                    return _data;
                GetData();
                return _data;
            }
        }

        public byte[] Header { get; private set; }

        public int Build { get { return BitOperations.Swap(BitConverter.ToUInt16(Header, 2)); } }

        public int CryptoFlag { get { return BitOperations.Swap(BitConverter.ToUInt16(Header, 0x6)); } }

        public int Size { get { return BitOperations.Swap(BitConverter.ToUInt16(Header, 0xC)); } }

        public byte[] CryptoKey { get; private set; }

        public BlTypes Type {
            get {
                switch(Header[1]) {
                    case (byte)'B':
                        return BlTypes.Cb;
                    case (byte)'D':
                        return BlTypes.Cd;
                    case (byte)'E':
                        return BlTypes.Ce;
                    case (byte)'F':
                        return BlTypes.Cf;
                    case (byte)'G':
                        return BlTypes.Cg;
                    default:
                        throw new NotSupportedException(Encoding.ASCII.GetString(Header, 0, 2));
                }
            }
        }

        public bool IsZeroPaired {
            get {
                if(_data == null)
                    throw new InvalidOperationException("You must dump and decrypt first!");
                if(Encrypted)
                    throw new InvalidOperationException("You must decrypt first!");
                return BitOperations.DataIsZero(ref _data, 0x20, 0x20);
            }
        }

        private void GetData() {
            _reader.Seek(Offset, SeekOrigin.Begin);
            _data = _reader.ReadBytes(Size);
            Encrypted = VerifyDecrypted();
        }

        public bool VerifyDecrypted() {
            if(_data == null)
                throw new NullReferenceException("_data can't be null");
            switch(Type) {
                case BlTypes.Cb:
                    return BitOperations.DataIsZero(ref _data, 0x270, 0x120);
                case BlTypes.Cd:
                    return BitOperations.DataIsZero(ref _data, 0x30, 0x200);
                case BlTypes.Ce:
                    var retval = BitOperations.DataIsZero(ref _data, 0x56027, 0x3F);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x55FF1, 0x3F);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x55F9B, 0x3F);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x55F55, 0x3F);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x55F0F, 0x3F);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x5416D, 0x3F);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x54132, 0x34);
                    if(retval)
                        retval = BitOperations.DataIsZero(ref _data, 0x55EEC, 0x1E);
                    return retval;
                case BlTypes.Cf:
                    return BitOperations.DataIsZero(ref _data, 0x1F0, 0x20);
                    //case BlTypes.Cg:
                    //    return BitOperations.DataIsZero(ref _data, 0, 0);
                default:
                    throw new NotSupportedException();
            }
        }

        public void Decrypt() {
            if(Type != BlTypes.Cb || _parent == null)
                Decrypt(Main.FirstBlKeyBytes);
        }

        public void Decrypt(byte[] key) {
            DoCrypto(key);
            if(!VerifyDecrypted())
                throw new Exception("Decryption failed!");
        }

        public void Encrypt() {
            if(Type != BlTypes.Cb || _parent == null)
                Encrypt(Main.FirstBlKeyBytes);
        }

        public void Encrypt(byte[] key) { DoCrypto(key, false); }

        private void DoCrypto(byte[] key, bool decrypt = true) {
            if(_data == null)
                GetData();
            if((!decrypt && Encrypted) || (decrypt && Encrypted))
                return;
            if(_parent != null && _parent.CryptoKey == null)
                throw new Exception("You must decrypt the bootloader chain in order starting from CB/CB_A");
            switch(Type) {
                case BlTypes.Cb:
                case BlTypes.Cd:
                case BlTypes.Ce:
                    DoCryptoChain(key);
                    break;
                case BlTypes.Cf:
                    DoCryptoCf(key);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private void DoCryptoCf(byte[] key) {
            var buf = new byte[0x10];
            Array.Copy(Data, 0x20, buf, 0, buf.Length);
            key = new HMACSHA1(key).ComputeHash(buf);
            Array.Resize(ref key, 0x10);
            CryptoKey = key;
            buf = new byte[Data.Length - 0x30];
            Buffer.BlockCopy(Data, 0x30, buf, 0, buf.Length);
            Rc4.Compute(ref buf, key);
            Buffer.BlockCopy(buf, 0, Data, 0x30, buf.Length);
        }

        private void DoCryptoChain(byte[] key) {
            byte[] buf;
            switch(CryptoFlag) {
                case 0:
                    key = new HMACSHA1(key).ComputeHash(Header);
                    break;
                case 0x801:
                case 0x800:
                    if(_parent == null)
                        throw new NullReferenceException("The crypto flag says there should be a parent to this bootloader, but there is none defined!");
                    buf = new byte[Header.Length + key.Length];
                    Array.Copy(Header, 0, buf, 0, Header.Length);
                    Array.Copy(key, 0, buf, Header.Length, key.Length);
                    key = new HMACSHA1(_parent.CryptoKey).ComputeHash(buf);
                    break;
                case 0x1800:
                    if(_parent == null)
                        throw new NullReferenceException("The crypto flag says there should be a parent to this bootloader, but there is none defined!");
                    buf = new byte[Header.Length + key.Length + _parent.CryptoKey.Length];
                    Array.Copy(Header, 0, buf, 0, Header.Length);
                    Array.Copy(key, 0, buf, Header.Length, key.Length);
                    Array.Copy(_parent.CryptoKey, 0, buf, Header.Length + key.Length, 0x6);
                    Array.Copy(_parent.CryptoKey, 0x8, buf, Header.Length + key.Length + 0x8, 0x8);
                    key = new HMACSHA1(_parent.CryptoKey).ComputeHash(buf);
                    break;
            }
            Array.Resize(ref key, 0x10);
            CryptoKey = key;
            buf = new byte[Data.Length - 0x20];
            Buffer.BlockCopy(Data, 0x20, buf, 0, buf.Length);
            Rc4.Compute(ref buf, CryptoKey);
            Buffer.BlockCopy(buf, 0, Data, 0x20, buf.Length);
        }

        public void ZeroPair() {
            if(_data == null)
                throw new InvalidOperationException("You must dump and decrypt first!");
            if(Encrypted)
                throw new InvalidOperationException("You must decrypt first!");
            if(_data.Length <= 0x40)
                throw new InvalidOperationException("The bootloader should be bigger then 0x40 bytes!");
            var tmp = new byte[0x20];
            Buffer.BlockCopy(tmp, 0, _data, 0x20, tmp.Length);
        }
    }
}
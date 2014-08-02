namespace x360Utils.NAND {
    using System;
    using System.IO;
    using x360Utils.Common;

    public class Bootloader {
        public readonly uint Build;
        public readonly uint Offset;
        public readonly uint Size;
        public readonly uint StartLba;
        public readonly BootLoaderTypes Type;
        private readonly Cryptography _crypto = new Cryptography();
        public byte[] Data;
        public bool Decrypted;
        public byte[] Key;
        public byte[] OutKey;

        #region BootLoaderTypes enum

        public enum BootLoaderTypes {
            CB,
            CBA,
            CBB,
            CD,
            CE,
            CF0,
            CF1,
            CG0,
            CG1
        }

        #endregion

        public Bootloader(byte[] data, BootLoaderTypes type) {
            Type = type;
            Data = data;
            Size = (uint)data.Length;
            Offset = 0;
            Build = GetBootloaderVersion(ref data);
        }

        public Bootloader(NANDReader reader, int count = 0, bool readitin = false) {
            Offset = (uint)(reader.Lba * 0x4000 + reader.Position % 0x4000);
            StartLba = reader.Lba;
            var header = reader.ReadBytes(0x10);
            Type = GetTypeFromHeader(ref header, count);
            Size = GetBootloaderSize(ref header);
            Build = GetBootloaderVersion(ref header);
            if(readitin) {
                reader.SeekToLbaEx(Offset / 0x4000);
                if(Offset % 0x4000 > 0)
                    reader.Seek(Offset % 0x4000, SeekOrigin.Current);
                Data = new byte[Size];
                var left = Size;
                var dOffset = 0;
                for(var i = 0; left > 0; i++) {
                    var toread = (int)BitOperations.GetSmallest(0x4000, left);
                    if(left == Size && reader.Position % 0x4000 > 0)
                        toread = (int)BitOperations.GetSmallest((0x4000 - (reader.Position % 0x4000)), left);
                    var tmp = reader.ReadBytes(toread);
                    Buffer.BlockCopy(tmp, 0, Data, dOffset, toread);
                    left -= (uint)toread;
                    dOffset += toread;
                    if(left > 0) // We want to seek to the next block!
                        reader.SeekToLbaEx((uint)((Offset / 0x4000) + 1 + i));
                }
            }
            else {
                reader.SeekToLbaEx((Offset + Size) / 0x4000);
                if((Offset + Size) % 0x4000 > 0)
                    reader.Seek((Offset + Size) % 0x4000, SeekOrigin.Current);
            }
            //reader.Seek(Offset + Size, SeekOrigin.Begin);
        }

        public Cryptography.BlEncryptionTypes CryptoType {
            get {
                try {
                    if(Data != null && Data.Length > 0x20)
                        return _crypto.GetBootloaderCryptoType(ref Data);
                }
                catch(NotSupportedException) {
                    return Cryptography.BlEncryptionTypes.NotSupported;
                }
                return Cryptography.BlEncryptionTypes.Unknown;
            }
        }

        private uint GetBootloaderSize(ref byte[] header) { return BitOperations.Swap(BitConverter.ToUInt32(header, 0xC)); }

        public BootLoaderTypes GetTypeFromHeader(ref byte[] header, int count = 0) {
            if(header[0] == 0x43) {
                switch(header[1]) {
                    case 0x42:
                        if(count == 0)
                            return (BitOperations.Swap(BitConverter.ToUInt16(header, 6)) & 0x800) == 0x800 ? BootLoaderTypes.CBA : BootLoaderTypes.CB;
                        return BootLoaderTypes.CBB;
                    case 0x44:
                        return BootLoaderTypes.CD;
                    case 0x45:
                        return BootLoaderTypes.CE;
                    case 0x46:
                        if(count == 0 || count == 5 || count == 6)
                            return BootLoaderTypes.CF0;
                        return BootLoaderTypes.CF1;
                    case 0x47:
                        if(count == 0 || count == 8 || count == 9)
                            return BootLoaderTypes.CG0;
                        return BootLoaderTypes.CG1;
                }
            }
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
        }

        private uint GetBootloaderVersion(ref byte[] header) {
            if(header.Length <= 4)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            return BitOperations.Swap(BitConverter.ToUInt16(header, 2));
        }

        public void Decrypt(byte[] decryptionkey) {
            if(Decrypted)
                return;
            if(decryptionkey == null)
                return;
            switch(Type) {
                case BootLoaderTypes.CB:
                case BootLoaderTypes.CBA:
                    _crypto.DecryptBootloaderCB(ref Data, decryptionkey, null, Cryptography.BlEncryptionTypes.Default, out OutKey);
                    Decrypted = _crypto.VerifyCBDecrypted(ref Data);
                    break;
                case BootLoaderTypes.CBB:
                    var cryptotype = _crypto.GetBootloaderCryptoType(ref Data);
                    _crypto.DecryptBootloaderCB(ref Data, decryptionkey, Key, cryptotype, out OutKey);
                    Decrypted = _crypto.VerifyCBDecrypted(ref Data);
                    break;
                case BootLoaderTypes.CD:
                case BootLoaderTypes.CE:
                    _crypto.DecryptBootloaderCB(ref Data, decryptionkey, Key, Cryptography.BlEncryptionTypes.Default, out OutKey);
                    Decrypted = _crypto.VerifyCBDecrypted(ref Data);
                    break;
                case BootLoaderTypes.CF0:
                case BootLoaderTypes.CF1:
                    _crypto.DecryptBootloaderCF(ref Data, ref decryptionkey, out OutKey);
                    Decrypted = _crypto.VerifyCFDecrypted(ref Data);
                    break;
                case BootLoaderTypes.CG0:
                case BootLoaderTypes.CG1:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Encrypt(byte[] encryptionkey = null) {
            if(!Decrypted)
                return;
            switch(Type) {
                case BootLoaderTypes.CB:
                case BootLoaderTypes.CBA:
                    _crypto.EncryptBootloaderCB(ref Data, encryptionkey, null, Cryptography.BlEncryptionTypes.Default, out Key);
                    break;
                case BootLoaderTypes.CBB:
                    var cryptotype = _crypto.GetBootloaderCryptoType(ref Data);
                    _crypto.EncryptBootloaderCB(ref Data, encryptionkey, Key, cryptotype, out OutKey);
                    break;
                case BootLoaderTypes.CD:
                case BootLoaderTypes.CE:
                    _crypto.EncryptBootloaderCB(ref Data, encryptionkey, Key, Cryptography.BlEncryptionTypes.Default, out OutKey);
                    break;
                case BootLoaderTypes.CF0:
                case BootLoaderTypes.CF1:
                case BootLoaderTypes.CG0:
                case BootLoaderTypes.CG1:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
            Decrypted = false;
        }

        public void Zeropair(ref byte[] data) {
            if(data == null)
                throw new ArgumentNullException("data");
            if(data.Length <= 0x40)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            var tmp = new byte[0x20];
            Buffer.BlockCopy(tmp, 0x00, data, 0x20, 0x20);
        }
    }
}
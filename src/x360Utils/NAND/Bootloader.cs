namespace x360Utils.NAND {
    using System;
    using System.IO;
    using x360Utils.Common;

    public class Bootloader {
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

        public readonly uint Build;
        public readonly uint Offset;
        public readonly uint Size;
        public readonly BootLoaderTypes Type;
        public byte[] Data;
        public bool Decrypted;
        public byte[] Key;
        public byte[] OutKey;

        public Bootloader(byte[] data, BootLoaderTypes type) {
            Type = type;
            Data = data;
            Size = (uint)data.Length;
            Offset = 0;
            Build = GetBootloaderVersion(ref data);
        }

        public Bootloader(NANDReader reader, int count = 0, bool readitin = false) {
            Offset = (uint)reader.Position;
            var header = reader.ReadBytes(0x10);
            Type = GetTypeFromHeader(ref header, count);
            Size = GetBootloaderSize(ref header);
            Build = GetBootloaderVersion(ref header);
            if(readitin) {
                reader.Seek(Offset, SeekOrigin.Begin);
                Data = reader.ReadBytes((int)Size);
            }
            else {
                reader.SeekToLbaEx(Offset + Size / 0x4000);
                if (Offset + Size % 0x4000 > 0)
                    reader.Seek(Offset + Size % 0x4000, SeekOrigin.Current);
                //reader.Seek(Offset + Size, SeekOrigin.Begin);
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

        public void Decrypt(byte[] decryptionkey = null) {
            if(Decrypted)
                return;
            var crypto = new Cryptography();
            switch(Type) {
                case BootLoaderTypes.CB:
                case BootLoaderTypes.CBA:
                    crypto.DecryptBootloaderCB(ref Data, decryptionkey, null, Cryptography.BlEncryptionTypes.Default, out OutKey);
                    Decrypted = crypto.VerifyCBDecrypted(ref Data);
                    break;
                case BootLoaderTypes.CBB:
                    var cryptotype = crypto.GetBootloaderCryptoType(ref Data);
                    crypto.DecryptBootloaderCB(ref Data, decryptionkey, Key, cryptotype, out OutKey);
                    Decrypted = crypto.VerifyCBDecrypted(ref Data);
                    break;
                case BootLoaderTypes.CD:
                case BootLoaderTypes.CE:
                    throw new NotImplementedException();
                case BootLoaderTypes.CF0:
                case BootLoaderTypes.CF1:
                    crypto.DecryptBootloaderCF(ref Data, ref decryptionkey, out OutKey);
                    Decrypted = crypto.VerifyCFDecrypted(ref Data);
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
            var crypto = new Cryptography();
            switch(Type) {
                case BootLoaderTypes.CB:
                case BootLoaderTypes.CBA:
                    crypto.EncryptBootloaderCB(ref Data, encryptionkey, null, Cryptography.BlEncryptionTypes.Default, out Key);
                    break;
                case BootLoaderTypes.CBB:
                    var cryptotype = crypto.GetBootloaderCryptoType(ref Data);
                    crypto.DecryptBootloaderCB(ref Data, encryptionkey, Key, cryptotype, out OutKey);
                    break;
                case BootLoaderTypes.CD:
                    crypto.DecryptBootloaderCB(ref Data, encryptionkey, Key, Cryptography.BlEncryptionTypes.Default, out OutKey);
                    break;
                case BootLoaderTypes.CE:
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
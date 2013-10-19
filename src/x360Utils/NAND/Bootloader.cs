namespace x360Utils.NAND {
    using System;
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
        public readonly BootLoaderTypes Type;
        public byte[] Data;
        public bool Decrypted;
        public byte[] Key;
        public byte[] OutKey;

        public Bootloader(byte[] data, BootLoaderTypes type) {
            Type = type;
            Data = data;
            Build = GetBootloaderVersion();
        }

        private uint GetBootloaderVersion() {
            if(Data.Length <= 4)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            return BitOperations.Swap(BitConverter.ToUInt16(Data, 2));
        }

        public void Decrypt(byte[] decryptionkey = null) {
            if(Decrypted)
                return;
            var crypto = new Cryptography();
            switch(Type) {
                case BootLoaderTypes.CB:
                case BootLoaderTypes.CBA:
                    crypto.DecryptBootloaderCB(ref Data, decryptionkey, null, Cryptography.BLEncryptionTypes.Default, out OutKey);
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
                    crypto.EncryptBootloaderCB(ref Data, encryptionkey, null, Cryptography.BLEncryptionTypes.Default, out Key);
                    break;
                case BootLoaderTypes.CBB:
                    var cryptotype = crypto.GetBootloaderCryptoType(ref Data);
                    crypto.DecryptBootloaderCB(ref Data, encryptionkey, Key, cryptotype, out OutKey);
                    break;
                case BootLoaderTypes.CD:
                    crypto.DecryptBootloaderCB(ref Data, encryptionkey, Key, Cryptography.BLEncryptionTypes.Default, out OutKey);
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
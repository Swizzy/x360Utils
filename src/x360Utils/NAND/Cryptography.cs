#region

using System;
using System.Security.Cryptography;
using x360Utils.Common;

#endregion

namespace x360Utils.NAND {
    public sealed class Cryptography {
        #region BLEncryptionTypes enum

        public enum BLEncryptionTypes : ushort {
            Default = 0,
            CBB = 0x800,
            MFGCBB = 0x801,
            CPUKey = 0x1800
        }

        #endregion

        private static readonly byte[] BLKey = new byte[]
                                                   {
                                                       0xDD, 0x88, 0xAD, 0x0C, 0x9E, 0xD6, 0x69, 0xE7, 0xB5, 0x67, 0x94,
                                                       0xFB, 0x68, 0x56, 0x3E, 0xFA
                                                   };

        public static void Rc4(ref byte[] bytes, byte[] key) {
            var s = new byte[256];
            var k = new byte[256];
            byte temp;
            int i;
            for (i = 0; i < 256; i++) {
                s[i] = (byte) i;
                k[i] = key[i % key.GetLength(0)];
            }
            var j = 0;
            for (i = 0; i < 256; i++) {
                j = (j + s[i] + k[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
            }
            i = j = 0;
            for (var x = 0; x < bytes.GetLength(0); x++) {
                i = (i + 1) % 256;
                j = (j + s[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
                var t = (s[i] + s[j]) % 256;
                bytes[x] ^= s[t];
            }
        }

        #region SMC

        public static bool VerifySMCDecrypted(ref byte[] data) {
            return BitOperations.DataIsZero(ref data, data.Length - 4, 4);
        }

        public void DecryptSMC(ref byte[] data) {
            var key = new byte[] {0x42, 0x75, 0x4E, 0x79};
            for (var i = 0; i < data.Length; i++) {
                var num1 = data[i];
                var num2 = num1 * 0xFB;
                data[i] = Convert.ToByte(num1 ^ (key[i & 3] & 0xFF));
                key[(i + 1) & 3] += (byte) num2;
                key[(i + 2) & 3] += Convert.ToByte(num2 >> 8);
            }
        }

        public void EncryptSMC(ref byte[] data) {
            var key = new byte[] {0x42, 0x75, 0x4e, 0x79};
            for (var i = 0; i < data.Length; i++) {
                var num2 = data[i] ^ (key[i & 3] & 0xff);
                var num3 = num2 * 0xFB;
                data[i] = Convert.ToByte(num2);
                key[(i + 1) & 3] = (byte) (key[(i + 1) & 3] + (byte) num3);
                key[(i + 2) & 3] = (byte) (key[(i + 2) & 3] + Convert.ToByte(num3 >> 8));
            }
        }

        #endregion SMC

        #region Bootloaders

        public BLEncryptionTypes GetBootloaderCryptoType(ref byte[] data) {
            var type = BitOperations.Swap(BitConverter.ToUInt16(data, 6));
            if (Enum.IsDefined(typeof (BLEncryptionTypes), type))
                return (BLEncryptionTypes) type;
            throw new NotSupportedException(string.Format("This encryption type is not supported yet... Value: {0:X4}", type));
        }

        public void DecryptBootloaderCB(ref byte[] data, byte[] inkey, byte[] oldkey, BLEncryptionTypes type, out byte[] outkey) {
            #region Error Handling

            if (inkey == null) {
                switch (type) {
                    case BLEncryptionTypes.Default:
                    case BLEncryptionTypes.CBB:
                        inkey = BLKey;
                        break;
                    case BLEncryptionTypes.MFGCBB:
                        inkey = new byte[0x10]; // 00's for key (MFG bootloader)
                        break;
                    default:
                        throw new ArgumentNullException("inkey");
                }
            }
            if (inkey.Length != 0x10)
                throw new ArgumentOutOfRangeException("inkey");

            #endregion

            var header = new byte[0x10];
            Array.Copy(data, 0x10, header, 0x0, 0x10);
            switch (type) {
                case BLEncryptionTypes.Default:
                    outkey = new HMACSHA1(inkey).ComputeHash(header);
                    break;
                case BLEncryptionTypes.CBB:
                case BLEncryptionTypes.MFGCBB:
                    Array.Resize(ref header, 0x20);
                    Array.Copy(inkey, 0x0, header, 0x10, 0x10);
                    outkey = new HMACSHA1(oldkey).ComputeHash(header);
                    break;
                case BLEncryptionTypes.CPUKey:
                    header = new byte[0x30];
                    Array.Copy(data, 0x10, header, 0x0, 0x10);
                    Array.Copy(inkey, 0x0, header, 0x10, 0x10);
                    Array.Copy(oldkey, 0x0, header, 0x20, 0x6);
                    Array.Copy(oldkey, 0x8, header, 0x20 + 0x8, 0x8);
                    var cbakey = new byte[0x10];
                    Array.Copy(oldkey, 0x10, cbakey, 0x0, 0x10);
                    outkey = new HMACSHA1(cbakey).ComputeHash(header);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
            Array.Resize(ref outkey, 0x10);
            var decrypted = new byte[data.Length - 0x20];
            Buffer.BlockCopy(data, 0x20, decrypted, 0x0, decrypted.Length);
            Rc4(ref decrypted, outkey);
            Buffer.BlockCopy(decrypted, 0x0, data, 0x20, decrypted.Length);
        }

        public void EncryptBootloaderCB(ref byte[] data, byte[] inkey, byte[] oldkey, BLEncryptionTypes type, out byte[] outkey) {
            #region Error Handling

            if (inkey == null) {
                switch (type) {
                    case BLEncryptionTypes.Default:
                    case BLEncryptionTypes.CBB:
                        inkey = BLKey;
                        break;
                    case BLEncryptionTypes.MFGCBB:
                        inkey = new byte[0x10]; // 00's for key (MFG bootloader)
                        break;
                    default:
                        throw new ArgumentNullException("inkey");
                }
            }
            if (inkey.Length != 0x10)
                throw new ArgumentOutOfRangeException("inkey");

            #endregion

            var header = new byte[0x10];
            Array.Copy(data, 0x10, header, 0x0, 0x10);
            switch (type) {
                case BLEncryptionTypes.Default:
                    break;
                case BLEncryptionTypes.CBB:
                case BLEncryptionTypes.MFGCBB:
                    Buffer.BlockCopy(oldkey, 0x0, header, 0x0, 0x10);
                    Buffer.BlockCopy(header, 0x0, data, 0x10, 0x10);
                    break;
                case BLEncryptionTypes.CPUKey:
                    throw new NotImplementedException("This type is not yet supported...");
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
            Buffer.BlockCopy(header, 0, data, 0x10, 0x10);
            outkey = new HMACSHA1(inkey).ComputeHash(header);
            Array.Resize(ref outkey, 0x10);
            var tmp = new byte[data.Length - 0x20];
            Buffer.BlockCopy(data, 0x20, tmp, 0x0, tmp.Length);
            Rc4(ref tmp, outkey);
            Buffer.BlockCopy(tmp, 0x0, data, 0x20, tmp.Length);
        }

        public void DecryptBootloaderCF(ref byte[] data, ref byte[] inkey, out byte[] outkey) {
            #region Error Handling

            if (inkey == null)
                inkey = BLKey;
            if (inkey.Length != 0x10)
                throw new ArgumentOutOfRangeException("inkey");

            #endregion

            var header = new byte[0x10];
            Array.Copy(data, 0x20, header, 0x0, 0x10);
            outkey = new HMACSHA1(inkey).ComputeHash(header);
            Array.Resize(ref outkey, 0x10);
            Buffer.BlockCopy(data, 0x0, data, 0x0, 0x20);
            Array.Copy(outkey, 0x0, data, 0x20, outkey.Length);
            var decrypted = new byte[data.Length - 0x30];
            Buffer.BlockCopy(data, 0x30, decrypted, 0x0, decrypted.Length);
            Rc4(ref decrypted, outkey);
            Buffer.BlockCopy(decrypted, 0x0, data, 0x30, decrypted.Length);
        }

        public bool VerifyCBDecrypted(ref byte[] data) {
            return BitOperations.DataIsZero(ref data, 0x270, 0x120);
        }

        public bool VerifyCFDecrypted(ref byte[] data) {
            return BitOperations.DataIsZero(ref data, 0x1F0, 0x20);
        }

        #endregion Bootloaders

        #region Keyvault

        public void DecryptKV(ref byte[] data, string cpukey) {
            DecryptKV(ref data, StringUtils.HexToArray(cpukey));
        }

        public void DecryptKV(ref byte[] data, byte[] cpukey) {
            if (data.Length < 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            if (data.Length > 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooBig);
            if (cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyTooShort);
            if (cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyTooLong);
            var tmp = new byte[data.Length - 0x10];
            var header = new byte[0x10];
            Array.Copy(data, 0x0, header, 0x0, 0x10);
            Buffer.BlockCopy(data, 0x10, tmp, 0x0, tmp.Length);
            cpukey = new HMACSHA1(cpukey).ComputeHash(header);
            Array.Resize(ref cpukey, 0x10);
            Rc4(ref tmp, cpukey);
            Array.Copy(header, data, header.Length);
            Buffer.BlockCopy(tmp, 0x0, data, header.Length, tmp.Length);
        }

        public bool VerifyKVDecrypted(ref byte[] data, string cpukey) {
            return VerifyKVDecrypted(ref data, StringUtils.HexToArray(cpukey));
        }

        public bool VerifyKVDecrypted(ref byte[] data, byte[] cpukey) {
            if (data.Length < 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            if (data.Length > 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooBig);
            if (cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyTooShort);
            if (cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyTooLong);
            byte[] header = new byte[0x10], tmp = new byte[0x4002 - 0x10];
            Array.Copy(data, 0x0, header, 0x0, 0x10);
            Buffer.BlockCopy(data, 0x10, tmp, 0x0, 0x4000 - 0x10);
            tmp[0x3FF0] = 0x7;
            tmp[0x3FF1] = 0x12;
            var checkdata = new HMACSHA1(cpukey).ComputeHash(tmp);
            Array.Resize(ref checkdata, 0x10);
            for (var i = 0; i < 0x10; i++) {
                if (checkdata[i] != header[i])
                    return false;
            }
            return true;
        }

        #endregion Keyvault
    }
}
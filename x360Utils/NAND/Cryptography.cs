namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using global::x360Utils.Common;

    public sealed class Cryptography {
        #region BLEncryptionTypes enum

        public enum BlEncryptionTypes: ushort {
            Default = 0,
            Cbb = 0x800,
            MfgCbb = 0x801,
            CpuKey = 0x1800,
            Unknown = ushort.MaxValue,
            NotSupported = ushort.MaxValue
        }

        #endregion

        private static readonly byte[] BlKey = {
                                                   0xDD, 0x88, 0xAD, 0x0C, 0x9E, 0xD6, 0x69, 0xE7, 0xB5, 0x67, 0x94, 0xFB, 0x68, 0x56, 0x3E, 0xFA
                                               };

        public static void Rc4(ref byte[] data, byte[] key) {
            var s = new byte[256];
            var k = new byte[256];
            byte temp;
            int i;
            for(i = 0; i < 256; i++) {
                s[i] = (byte)i;
                k[i] = key[i % key.GetLength(0)];
            }
            var j = 0;
            for(i = 0; i < 256; i++) {
                j = (j + s[i] + k[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
            }
            i = j = 0;
            for(var x = 0; x < data.GetLength(0); x++) {
                i = (i + 1) % 256;
                j = (j + s[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
                var t = (s[i] + s[j]) % 256;
                data[x] ^= s[t];
            }
        }

        #region SMC

        public static bool VerifySmcDecrypted(ref byte[] data) { return BitOperations.DataIsZero(ref data, data.Length - 4, 4); }

        public void DecryptSmc(ref byte[] data) {
            var key = new byte[] {
                                     0x42, 0x75, 0x4E, 0x79
                                 };
            for(var i = 0; i < data.Length; i++) {
                var num1 = data[i];
                var num2 = num1 * 0xFB;
                data[i] = Convert.ToByte(num1 ^ (key[i & 3] & 0xFF));
                key[(i + 1) & 3] += (byte)num2;
                key[(i + 2) & 3] += Convert.ToByte(num2 >> 8);
            }
        }

        public void EncryptSmc(ref byte[] data) {
            var key = new byte[] {
                                     0x42, 0x75, 0x4E, 0x79
                                 };
            for(var i = 0; i < data.Length; i++) {
                var num2 = data[i] ^ (key[i & 3] & 0xff);
                var num3 = num2 * 0xFB;
                data[i] = Convert.ToByte(num2);
                key[(i + 1) & 3] = (byte)(key[(i + 1) & 3] + (byte)num3);
                key[(i + 2) & 3] = (byte)(key[(i + 2) & 3] + Convert.ToByte(num3 >> 8));
            }
        }

        #endregion SMC

        #region Bootloaders

        public BlEncryptionTypes GetBootloaderCryptoType(ref byte[] data) {
            var type = BitOperations.Swap(BitConverter.ToUInt16(data, 6));
            if(Enum.IsDefined(typeof(BlEncryptionTypes), type))
                return (BlEncryptionTypes)type;
            throw new NotSupportedException(string.Format("This encryption type is not supported yet... Value: {0:X4}", type));
        }

        public void DecryptBootloaderCB(ref byte[] data, byte[] inkey, byte[] oldkey, BlEncryptionTypes type, out byte[] outkey) {
            #region Error Handling

            if(inkey == null) {
                switch(type) {
                    case BlEncryptionTypes.Default:
                    case BlEncryptionTypes.Cbb:
                        inkey = BlKey;
                        break;
                    case BlEncryptionTypes.MfgCbb:
                        inkey = new byte[0x10]; // 00's for key (MFG bootloader)
                        break;
                    default:
                        throw new ArgumentNullException("inkey");
                }
            }
            if(inkey.Length != 0x10)
                throw new ArgumentOutOfRangeException("inkey");

            #endregion

            var header = new byte[0x10];
            Array.Copy(data, 0x10, header, 0x0, 0x10);
            switch(type) {
                case BlEncryptionTypes.Default:
                    outkey = new HMACSHA1(inkey).ComputeHash(header);
                    break;
                case BlEncryptionTypes.Cbb:
                case BlEncryptionTypes.MfgCbb:
                    Array.Resize(ref header, 0x20);
                    Array.Copy(inkey, 0x0, header, 0x10, 0x10);
                    outkey = new HMACSHA1(oldkey).ComputeHash(header);
                    break;
                case BlEncryptionTypes.CpuKey:
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

        public void EncryptBootloaderCB(ref byte[] data, byte[] inkey, byte[] oldkey, BlEncryptionTypes type, out byte[] outkey) {
            #region Error Handling

            if(inkey == null) {
                switch(type) {
                    case BlEncryptionTypes.Default:
                    case BlEncryptionTypes.Cbb:
                        inkey = BlKey;
                        break;
                    case BlEncryptionTypes.MfgCbb:
                        inkey = new byte[0x10]; // 00's for key (MFG bootloader)
                        break;
                    default:
                        throw new ArgumentNullException("inkey");
                }
            }
            if(inkey.Length != 0x10)
                throw new ArgumentOutOfRangeException("inkey");

            #endregion

            var header = new byte[0x10];
            Array.Copy(data, 0x10, header, 0x0, 0x10);
            switch(type) {
                case BlEncryptionTypes.Default:
                    break;
                case BlEncryptionTypes.Cbb:
                case BlEncryptionTypes.MfgCbb:
                    Buffer.BlockCopy(oldkey, 0x0, header, 0x0, 0x10);
                    Buffer.BlockCopy(header, 0x0, data, 0x10, 0x10);
                    break;
                case BlEncryptionTypes.CpuKey:
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

            if(inkey == null)
                inkey = BlKey;
            if(inkey.Length != 0x10)
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

        public bool VerifyCBDecrypted(ref byte[] data) { return BitOperations.DataIsZero(ref data, 0x270, 0x120); }

        public bool VerifyCFDecrypted(ref byte[] data) { return BitOperations.DataIsZero(ref data, 0x1F0, 0x20); }

        #endregion Bootloaders

        #region Keyvault

        public void DecryptKv(ref byte[] data, string cpukey) { DecryptKv(ref data, StringUtils.HexToArray(cpukey)); }

        public void DecryptKv(ref byte[] data, byte[] cpukey) {
            if(data.Length < 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            if(data.Length > 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooBig);
            if(cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooShortKey);
            if(cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooLongKey);
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

        public bool VerifyKvDecrypted(ref byte[] data, string cpukey) { return VerifyKvDecrypted(ref data, StringUtils.HexToArray(cpukey)); }

        public bool VerifyKvDecrypted(ref byte[] data, byte[] cpukey) {
            if(data.Length < 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            if(data.Length > 0x4000)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooBig);
            if(cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooShortKey);
            if(cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooLongKey);
            byte[] header = new byte[0x10], tmp = new byte[0x4002 - 0x10];
            Array.Copy(data, 0x0, header, 0x0, 0x10);
            Buffer.BlockCopy(data, 0x10, tmp, 0x0, 0x4000 - 0x10);
            tmp[0x3FF0] = 0x7;
            tmp[0x3FF1] = 0x12;
            var checkdata = new HMACSHA1(cpukey).ComputeHash(tmp);
            Array.Resize(ref checkdata, 0x10);
            for(var i = 0; i < 0x10; i++) {
                if(checkdata[i] != header[i])
                    return false;
            }
            return true;
        }

        #endregion Keyvault

        #region FCRT

        public bool VerifyFcrtDecrypted(ref byte[] data) {
            if(data.Length < 0x140)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            using(var sha1 = new SHA1Managed()) {
                var hash = new byte[20];
                Buffer.BlockCopy(data, 0x12C, hash, 0, hash.Length);
                var hash2 = sha1.ComputeHash(data, (int)BitOperations.Swap(BitConverter.ToUInt32(data, 0x11C)), (int)BitOperations.Swap(BitConverter.ToUInt32(data, 0x118)));
                return BitOperations.CompareByteArrays(ref hash, ref hash2);
            }
        }

        public void DecryptFcrt(ref byte[] data, byte[] cpukey) {
            if(data.Length < 0x120)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataTooSmall);
            var offset = BitOperations.Swap(BitConverter.ToUInt32(data, 0x11C));
            var length = BitOperations.Swap(BitConverter.ToUInt32(data, 0x118));
            var iv = new byte[0x10];
            Buffer.BlockCopy(data, 0x100, iv, 0, iv.Length);
            if(data.Length < offset + length)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
            var buf = new byte[length];
            var ret = new List<byte>();
            Buffer.BlockCopy(data, (int)offset, buf, 0, buf.Length);
            using(var aes = new RijndaelManaged()) {
                aes.Mode = CipherMode.CBC;
                aes.KeySize = 128;
                aes.Key = cpukey;
                aes.IV = iv;
                aes.Padding = PaddingMode.None;
                using(var ms = new MemoryStream(buf)) {
                    using(var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read)) {
                        var b = 0;
                        while(b != -1) {
                            b = cs.ReadByte();
                            if(b != -1)
                                ret.Add((byte)b);
                        }
                    }
                }
                //return new BinaryReader(cs).ReadBytes((int)length);
            }
            buf = ret.ToArray();
            Buffer.BlockCopy(buf, 0, data, (int)offset, buf.Length);
        }

        #endregion
    }
}
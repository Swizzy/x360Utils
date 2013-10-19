#region

using System;
using System.Collections.Generic;
using System.IO;
using x360Utils.Common;

#endregion

namespace x360Utils.NAND {
    internal class X360NAND {
        public byte[] GetFCRT(ref NANDReader reader) {
            reader.Seek(0x8000, SeekOrigin.Begin);
            for (var i = 0; reader.Position < reader.Length; i = 0) {
                var tmp = reader.ReadBytes(0x4000);
                while (i < tmp.Length) {
                    if (tmp[i] == 0x66) {
                        if (tmp.Length - i < 28) {
                            var tmp2 = reader.ReadBytes(0x23);
                            reader.Seek(-0x10, SeekOrigin.Current);
                            Array.Resize(ref tmp, tmp.Length + tmp2.Length);
                            Buffer.BlockCopy(tmp2, 0, tmp, tmp.Length - tmp2.Length, tmp2.Length);
                        }
                        if (tmp[i + 1] == 0x63 && tmp[i + 2] == 0x72 && tmp[i + 3] == 0x74 && tmp[i + 4] == 0x2E &&
                            tmp[i + 5] == 0x62 && tmp[i + 6] == 0x69 && tmp[i + 7] == 0x6E) {
                            reader.Seek(BitOperations.Swap(BitConverter.ToUInt16(tmp, i + 0x16)), SeekOrigin.Begin);
                            return reader.ReadBytes((int) BitOperations.Swap(BitConverter.ToUInt32(tmp, i + 0x18)));
                        }
                    }
                }
            }
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound, "FCRT");
        }

        public byte[] GetKeyVault(ref NANDReader reader) {
            reader.Seek(0x4000, SeekOrigin.Begin);
            return reader.ReadBytes(0x4000);
        }

        public byte[] GetKeyVault(ref NANDReader reader, string cpukey) {
            var kv = GetKeyVault(ref reader);
            var crypto = new Cryptography();
            crypto.DecryptKV(ref kv, cpukey);
            return kv;
        }

        public byte[] GetKeyVault(ref NANDReader reader, byte[] cpukey) {
            var kv = GetKeyVault(ref reader);
            var crypto = new Cryptography();
            crypto.DecryptKV(ref kv, cpukey);
            return kv;
        }

        public byte[] GetSMC(ref NANDReader reader, bool decrypted = false) {
            reader.Seek(0x78, SeekOrigin.Begin);
            var tmp = reader.ReadBytes(4);
            var size = BitOperations.Swap(BitConverter.ToUInt32(tmp, 0));
            reader.Seek(0x7C, SeekOrigin.Begin);
            tmp = reader.ReadBytes(4);
            reader.Seek(BitOperations.Swap(BitConverter.ToUInt32(tmp, 0)), SeekOrigin.Begin);
            if (!decrypted)
                return reader.ReadBytes((int) size);
            tmp = reader.ReadBytes((int) size);
            var crypto = new Cryptography();
            crypto.DecryptSMC(ref tmp);
            if (!Cryptography.VerifySMCDecrypted(ref tmp))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataDecryptionFailed);
            return tmp;
        }

        public Bootloader[] GetBootLoaders(ref NANDReader reader) {
            var bls = new List<Bootloader>();
            reader.Seek(0x8000, SeekOrigin.Begin);
            bls.Add(new Bootloader(reader));
            try
            {
                for (var i = 1; i < 4; i++)
                    bls.Add(new Bootloader(reader, i));
            }
            catch (X360UtilsException ex) {
                if (ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataInvalid)
                    throw;
            }
            return bls.ToArray();
        }
    }
}
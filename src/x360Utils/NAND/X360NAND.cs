namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using x360Utils.CPUKey;
    using x360Utils.Common;

    public class X360NAND {
        public byte[] GetFCRT(ref NANDReader reader) {
            reader.Seek(0x8000, SeekOrigin.Begin);
            for(var i = 0; reader.Position < reader.Length; i = 0) {
                var tmp = reader.ReadBytes(0x4000);
                while(i < tmp.Length) {
                    if(tmp[i] == 0x66) {
                        if(tmp.Length - i < 28) {
                            var tmp2 = reader.ReadBytes(0x23);
                            reader.Seek(-0x10, SeekOrigin.Current);
                            Array.Resize(ref tmp, tmp.Length + tmp2.Length);
                            Buffer.BlockCopy(tmp2, 0, tmp, tmp.Length - tmp2.Length, tmp2.Length);
                        }
                        if(tmp[i + 1] == 0x63 && tmp[i + 2] == 0x72 && tmp[i + 3] == 0x74 && tmp[i + 4] == 0x2E && tmp[i + 5] == 0x62 && tmp[i + 6] == 0x69 && tmp[i + 7] == 0x6E) {
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
            if(!decrypted)
                return reader.ReadBytes((int) size);
            tmp = reader.ReadBytes((int) size);
            var crypto = new Cryptography();
            crypto.DecryptSMC(ref tmp);
            if(!Cryptography.VerifySMCDecrypted(ref tmp))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataDecryptionFailed);
            return tmp;
        }

        private static bool GetSMCConfig(ref NANDReader reader, int offset, out byte[] data) {
            data = null;
            try {
                var cfg = new SMCConfig();
                reader.Seek(offset, SeekOrigin.Begin);
                data = reader.ReadBytes(0x400);
                cfg.VerifySMCConfigChecksum(ref data);
            }
            catch(X360UtilsException ex) {
                if(ex.ErrorCode == X360UtilsException.X360UtilsErrors.BadChecksum)
                    return false;
                throw;
            }
            return true;
        }

        public byte[] GetSMCConfig(ref NANDReader reader) {
            byte[] data;
            if(!GetSMCConfig(ref reader, 0xF7C000, out data)) // 16MB NAND
            {
                if(!GetSMCConfig(ref reader, 0x2FFC000, out data)) // MMC NAND
                {
                    if(!GetSMCConfig(ref reader, 0x3BE0000, out data)) // BigBlock NAND
                        throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
                }
            }
            return data;
        }

        public Bootloader[] GetBootLoaders(ref NANDReader reader, bool readToMemory = false) {
            var bls = new List<Bootloader>();
            reader.Seek(0x8000, SeekOrigin.Begin);
            bls.Add(new Bootloader(reader, readitin : readToMemory));
            try {
                for(var i = 1; i < 4; i++)
                    bls.Add(new Bootloader(reader, i, readToMemory));
            }
            catch(X360UtilsException ex) {
                if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataInvalid)
                    throw;
            }
            try {
                reader.Seek(0x70, SeekOrigin.Begin);
                var tmp = reader.ReadBytes(4);
                var size = BitOperations.Swap(BitConverter.ToUInt32(tmp, 0));
                reader.Seek(0x64, SeekOrigin.Begin);
                tmp = reader.ReadBytes(4);
                var offset = BitOperations.Swap(BitConverter.ToUInt32(tmp, 0));
                reader.Seek(offset, SeekOrigin.Begin);
                bls.Add(new Bootloader(reader, readitin : readToMemory));
                bls.Add(new Bootloader(reader, readitin : readToMemory));
                try {
                    if(size == 0) {
                        reader.Seek(offset + 0x10000, SeekOrigin.Begin);
                        bls.Add(new Bootloader(reader, readitin : readToMemory));
                        bls.Add(new Bootloader(reader, readitin : readToMemory));
                    }
                }
                catch(X360UtilsException ex) {
                    if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataInvalid)
                        throw;
                    if(size == 0) {
                        reader.Seek(offset + 0x20000, SeekOrigin.Begin);
                        bls.Add(new Bootloader(reader, readitin : readToMemory));
                        bls.Add(new Bootloader(reader, readitin : readToMemory));
                    }
                }
            }
            catch(X360UtilsException ex) {
                if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataInvalid)
                    throw;
            }
            return bls.ToArray();
        }

        public string GetVirtualFuses(ref NANDReader reader) {
            reader.Seek(0x95000, SeekOrigin.Begin);
            var data = reader.ReadBytes(0x60);
            var tmp = new byte[] { 0xC0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0xF0 };
            if(!BitOperations.CompareByteArrays(ref data, ref tmp, false))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
            var ret = new StringBuilder();
            for(var index = 0; index < data.Length; index++) {
                if(index % 0x8 == 0 || index == 0)
                    ret.AppendFormat("\nfuseset {0:##}: ", index / 0x5);
                ret.Append(data[index].ToString("X2"));
            }
            return ret.ToString().Trim();
        }

        private static bool GetByteKey(ref NANDReader reader, int offset, out byte[] key) {
            var keyutils = new CpukeyUtils();
            reader.Seek(offset, SeekOrigin.Begin);
            key = reader.ReadBytes(0x10);
            try {
                keyutils.VerifyCpuKey(ref key);
                return true;
            }
            catch {
                return false;
            }
        }

        private static bool GetASCIIKey(ref NANDReader reader, int offset, out string key) {
            key = null;
            var keyutils = new CpukeyUtils();
            reader.Seek(offset, SeekOrigin.Begin);
            var tmp = reader.ReadBytes(0x10);
            try {
                key = Encoding.ASCII.GetString(tmp);
                return keyutils.VerifyCpuKey(key);
            }
            catch {
                return false;
            }
        }

        public string GetNANDCPUKey(ref NANDReader reader) {
            byte[] key;
            if(!GetByteKey(ref reader, 0x100, out key)) // Blakcat XeLL
            {
                if(!GetByteKey(ref reader, 0x6d0, out key)) // Blakcat Freeboot storage (Spare type offset)
                {
                    if(!GetByteKey(ref reader, 0x700, out key)) // Blakcat Freeboot storage (MMC type offset)
                    {
                        if(!GetByteKey(ref reader, 0x95020, out key)) // Virtual Fuses
                        {
                            string keys;
                            if(!GetASCIIKey(ref reader, 0x600, out keys)) // XeBuild GUI method
                                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
                            return keys;
                        }
                    }
                }
            }
            return StringUtils.ArrayToHex(key);
        }
    }
}
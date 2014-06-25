namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using x360Utils.Common;
    using x360Utils.CPUKey;

    public sealed class X360NAND {
        private readonly Cryptography _crypto = new Cryptography();

        public byte[] GetFcrt(NANDReader reader) {
            if(reader.FsRoot == null)
                reader.ScanForFsRootAndMobile();
            var nandfs = new NANDFileSystem();
            var fs = nandfs.ParseFileSystem(ref reader);
            foreach(var fileSystemEntry in fs)
                if(fileSystemEntry.Filename.Equals("fcrt.bin", StringComparison.CurrentCultureIgnoreCase))
                    return fileSystemEntry.GetData(ref reader);
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound, "FCRT");
        }

        public byte[] GetKeyVault(NANDReader reader, bool decrypted = false) {
            reader.Seek(0x4000, SeekOrigin.Begin);
            if(!decrypted)
                return reader.ReadBytes(0x4000);
            var kv = GetKeyVault(reader);
            var cpukey = GetNandCpuKey(reader);
            _crypto.DecryptKv(ref kv, cpukey);
            if(_crypto.VerifyKvDecrypted(ref kv, cpukey))
                return kv;
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataDecryptionFailed);
        }

        public byte[] GetKeyVault(NANDReader reader, string cpukey) {
            var kv = GetKeyVault(reader);
            _crypto.DecryptKv(ref kv, cpukey);
            if(_crypto.VerifyKvDecrypted(ref kv, cpukey))
                return kv;
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataDecryptionFailed);
        }

        public byte[] GetKeyVault(NANDReader reader, byte[] cpukey) {
            var kv = GetKeyVault(reader);
            _crypto.DecryptKv(ref kv, cpukey);
            if(_crypto.VerifyKvDecrypted(ref kv, cpukey))
                return kv;
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataDecryptionFailed);
        }

        public byte[] GetSmc(NANDReader reader, bool decrypted = false) {
            reader.Seek(0x78, SeekOrigin.Begin);
            var tmp = reader.ReadBytes(4);
            var size = BitOperations.Swap(BitConverter.ToUInt32(tmp, 0));
            reader.Seek(0x7C, SeekOrigin.Begin);
            tmp = reader.ReadBytes(4);
            reader.Seek(BitOperations.Swap(BitConverter.ToUInt32(tmp, 0)), SeekOrigin.Begin);
            if(!decrypted)
                return reader.ReadBytes((int)size);
            tmp = reader.ReadBytes((int)size);
            _crypto.DecryptSmc(ref tmp);
            if(!Cryptography.VerifySmcDecrypted(ref tmp))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataDecryptionFailed);
            return tmp;
        }

        public byte[] GetSmcConfig(NANDReader reader) {
            if(reader.RawLength == 0x1080000) // 16MB NAND
                reader.Seek(0xF7C000, SeekOrigin.Begin);
            else if(!reader.HasSpare) // MMC NAND
                reader.Seek(0x2FFC000, SeekOrigin.Begin);
            else // BigBlock NAND
                reader.Seek(0x3BE0000, SeekOrigin.Begin);
            var data = reader.ReadBytes(0x400);
            try {
                var cfg = new SmcConfig();
                cfg.VerifySMCConfigChecksum(data);
            }
            catch(X360UtilsException ex) {
                if(ex.ErrorCode == X360UtilsException.X360UtilsErrors.BadChecksum)
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
                throw;
            }
            return data;
        }

        public Bootloader[] GetBootLoaders(NANDReader reader, bool readToMemory = false) {
            var bls = new List<Bootloader>();
            reader.Seek(0x8000, SeekOrigin.Begin);
            bls.Add(new Bootloader(reader, readitin: readToMemory));
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
                bls.Add(new Bootloader(reader, readitin: readToMemory));
                bls.Add(new Bootloader(reader, readitin: readToMemory));
                try {
                    if(size == 0) {
                        reader.Seek(offset + 0x10000, SeekOrigin.Begin);
                        bls.Add(new Bootloader(reader, readitin: readToMemory));
                        bls.Add(new Bootloader(reader, readitin: readToMemory));
                    }
                }
                catch(X360UtilsException ex) {
                    if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataInvalid)
                        throw;
                    if(size == 0) {
                        reader.Seek(offset + 0x20000, SeekOrigin.Begin);
                        bls.Add(new Bootloader(reader, readitin: readToMemory));
                        bls.Add(new Bootloader(reader, readitin: readToMemory));
                    }
                }
            }
            catch(X360UtilsException ex) {
                if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataInvalid)
                    throw;
            }
            return bls.ToArray();
        }

        public string GetVirtualFuses(NANDReader reader) {
            reader.Seek(0x95000, SeekOrigin.Begin);
            var data = reader.ReadBytes(0x60);
            var tmp = new byte[] {
                                     0xC0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0x0F, 0xF0
                                 };
            if(!BitOperations.CompareByteArrays(ref data, ref tmp, false))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
            var ret = new StringBuilder();
            for(var index = 0; index < data.Length; index++) {
                if(index % 0x8 == 0 || index == 0)
                    ret.AppendFormat("\nfuseset {0:D2}: ", index / 0x5);
                ret.Append(data[index].ToString("X2"));
            }
            return ret.ToString().Trim();
        }

        private static bool GetByteKey(NANDReader reader, int offset, out byte[] key) {
            Debug.SendDebug("Grabbing Byte Key @ 0x{0:X}", offset);
            reader.Seek(offset, SeekOrigin.Begin);
            key = reader.ReadBytes(0x10);
            try {
                CpukeyUtils.VerifyCpuKey(ref key);
                return true;
            }
            catch(X360UtilsException ex) {
                Debug.SendDebug(ex.ToString());
                return false;
            }
        }

        private static bool GetAsciiKey(NANDReader reader, int offset, out string key) {
            Debug.SendDebug("Grabbing ASCII Key @ 0x{0:X}", offset);
            key = null;
            reader.Seek(offset, SeekOrigin.Begin);
            var tmp = reader.ReadBytes(0x10);
            try {
                key = Encoding.ASCII.GetString(tmp);
                try {
                    CpukeyUtils.VerifyCpuKey(key);
                    return true;
                }
                catch(X360UtilsException ex) {
                    Debug.SendDebug(ex.ToString());
                    return false;
                }
            }
            catch {
                return false;
            }
        }

        public string GetNandCpuKey(NANDReader reader) {
            byte[] key;
            if(!GetByteKey(reader, 0x100, out key)) // Blakcat XeLL
            {
                if(!GetByteKey(reader, 0x6d0, out key)) // Blakcat Freeboot storage (Spare type offset)
                {
                    if(!GetByteKey(reader, 0x700, out key)) // Blakcat Freeboot storage (MMC type offset)
                    {
                        if(!GetByteKey(reader, 0x600, out key)) // xeBuild GUI Offset
                        {
                            if(!GetByteKey(reader, 0x95020, out key)) // Virtual Fuses
                            {
                                string keys;
                                if(!GetAsciiKey(reader, 0x600, out keys)) // xeBuild GUI ASCII Method
                                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound);
                                return keys;
                            }
                        }
                    }
                }
            }
            return StringUtils.ArrayToHex(key);
        }

        public string GetLaunchIni(NANDReader reader) {
            if (reader.FsRoot == null)
                reader.ScanForFsRootAndMobile();
            var nandfs = new NANDFileSystem();
            var fs = nandfs.ParseFileSystem(ref reader);
            foreach (var fileSystemEntry in fs)
                if (fileSystemEntry.Filename.Equals("launch.ini", StringComparison.CurrentCultureIgnoreCase))
                    return Encoding.ASCII.GetString(fileSystemEntry.GetData(ref reader));
            throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataNotFound, "Launch.ini");
        }
    }
}
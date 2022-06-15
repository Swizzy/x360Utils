namespace x360Utils.CPUKey {
    using System;
    using System.IO;
    using x360Utils.Common;

    public sealed class CpukeyUtils {
        private static Random _random = new Random((int)(DateTime.Now.Ticks & 0xFFFF));

        public static void UpdateRandom(int seed) { _random = new Random(seed); }

        public byte[] GenerateRandomCPUKey() {
            var key = new byte[0x10];
            do {
                _random.NextBytes(key);
                if(BitOperations.DataIsZero(ref key, 0, key.Length))
                    UpdateRandom((int)(DateTime.Now.Ticks & 0xFFFF));
                try {
                    VerifyCPUKeyHammingWeight(ref key);
                    CalculateCPUKeyECD(ref key);
                    return key;
                }
                catch(X360UtilsException) {}
            }
            while(true);
        }

        private static void CalculateCPUKeyECD(ref byte[] key) {
            uint acc1 = 0, acc2 = 0;
            for(var cnt = 0; cnt < 0x80; cnt++, acc1 >>= 1) {
                var bTmp = key[cnt >> 3];
                var dwTmp = (uint)((bTmp >> (cnt & 7)) & 1);
                if(cnt < 0x6A) {
                    acc1 = dwTmp ^ acc1;
                    if((acc1 & 1) > 0)
                        acc1 = acc1 ^ 0x360325;
                    acc2 = dwTmp ^ acc2;
                }
                else if(cnt < 0x7F) {
                    if(dwTmp != (acc1 & 1))
                        key[(cnt >> 3)] = (byte)((1 << (cnt & 7)) ^ (bTmp & 0xFF));
                    acc2 = (acc1 & 1) ^ acc2;
                }
                else if(dwTmp != acc2)
                    key[0xF] = (byte)((0x80 ^ bTmp) & 0xFF);
            }
        }

        private static void VerifyCPUKeyECD(ref byte[] key) { 
            var scratch = new byte[0x10];
            Buffer.BlockCopy(key, 0, scratch, 0, key.Length);
            CalculateCPUKeyECD(ref scratch);
            if(!BitOperations.CompareByteArrays(ref key, ref scratch))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.InvalidKeyECD);
        }

        private static void VerifyCPUKeyHammingWeight(ref byte[] cpukey) {
            UInt64 part0 = BitOperations.Swap(BitConverter.ToUInt64(cpukey, 0));
            UInt64 part1 = BitOperations.Swap(BitConverter.ToUInt64(cpukey, 8));
            if(BitOperations.CountSetBits(part0) + BitOperations.CountSetBits(part1 & 0xFFFFFFFFFF030000) != 53)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.InvalidKeyHamming);
        }

        public static void VerifyCpuKey(string cpukey) {
            cpukey = cpukey.Trim();
            var tmp = StringUtils.HexToArray(cpukey);
            VerifyCpuKey(ref tmp);
        }

        public static void VerifyCpuKey(ref byte[] cpukey) {
            if(cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooShortKey);
            if(cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooLongKey);
            VerifyCPUKeyHammingWeight(ref cpukey);
            VerifyCPUKeyECD(ref cpukey);
        }

        public static void VerifyCpuKey(UInt64 part0, UInt64 part1) {
            var key = new byte[0x10];
            var tmp = BitConverter.GetBytes(part0);
            Buffer.BlockCopy(tmp, 0, key, 0, tmp.Length);
            tmp = BitConverter.GetBytes(part1);
            Buffer.BlockCopy(tmp, 0, key, tmp.Length, tmp.Length);
            VerifyCPUKeyHammingWeight(ref key);
            VerifyCPUKeyECD(ref key);
   }

        public bool ReadKeyfile(string file, out string cpukey) {
            cpukey = "";
            using(var sr = new StreamReader(file)) {
                if(sr.BaseStream.Length > 0x5000)
                    return false; // We don't want to read files that are HUGE!
                var key = sr.ReadLine();
                if(key != null && ((key.Trim().IndexOf("cpukey", StringComparison.CurrentCultureIgnoreCase) >= 0) && (key.Trim().Length == 38)))
                    key = key.Trim().Substring(key.Trim().Length - 32, 32);
                if(string.IsNullOrEmpty(key) || !StringUtils.StringIsHex(key))
                    return false;
                cpukey = key.Trim().ToUpper();
                try {
                    VerifyCpuKey(cpukey);
                    return true;
                }
                catch(X360UtilsException) {
                    return false;
                }
            }
        }

        public bool ReadFusefile(string file, out string cpukey, out int ldv) {
            var fuse = new FUSE(file);
            ldv = fuse.CFLDV;
            cpukey = "";
            try {
                cpukey = fuse.CPUKey;
                VerifyCpuKey(cpukey);
                return true;
            }
            catch(X360UtilsException ex) {
                if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.NoValidKeyFound)
                    throw; // Dafuq?
                return false; // Key not found...
            }
        }

        public string GetCPUKeyFromTextFile(string file) {
            string cpukey;
            int ldv;
            if(!ReadKeyfile(file, out cpukey) && !ReadFusefile(file, out cpukey, out ldv))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.NoValidKeyFound);
            return cpukey;
        }
    }
}

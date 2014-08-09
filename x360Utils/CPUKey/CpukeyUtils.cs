namespace x360Utils.CPUKey {
    using System;
    using System.IO;
    using x360Utils.Common;

    public sealed class CpukeyUtils {
        private static Random _random = new Random((int)(DateTime.Now.Ticks & 0xFFFF));

        public static void UpdateRandom(int seed) { _random = new Random(seed); }

        public byte[] GenerateRandomCpuKey() {
            var key = new byte[0x10];
            do {
                _random.NextBytes(key);
                if(BitOperations.DataIsZero(ref key, 0, key.Length))
                    UpdateRandom((int)(DateTime.Now.Ticks & 0xFFFF));
                try {
                    VerifyCpuKey(ref key);
                    return key;
                }
                catch(X360UtilsException) {}
            }
            while(true);
        }

        private static void CalculateCpuKeyEcd(ref byte[] key) {
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

        public static void VerifyCpuKey(string cpukey) {
            cpukey = cpukey.Trim();
            var tmp = StringUtils.HexToArray(cpukey);
            VerifyCpuKey(ref tmp);
        }

        public static void VerifyCpuKey(ref byte[] cpukey) {
            if(cpukey.Length != 0x10)
                throw new CpuKeyException(CpuKeyException.ExceptionTypes.InvalidLength);
            VerifyCpuKey(BitOperations.Swap(BitConverter.ToUInt64(cpukey, 0)), BitOperations.Swap(BitConverter.ToUInt64(cpukey, 8)));
        }

        public static void VerifyCpuKey(UInt64 cpukey0, UInt64 cpukey1) {
            var hamming = BitOperations.CountSetBits(cpukey0) + BitOperations.CountSetBits(cpukey1 & 0xFFFFFFFFFF030000);
            if(hamming != 53)
                throw new CpuKeyException(CpuKeyException.ExceptionTypes.Hamming);
            var tmp = BitConverter.GetBytes(BitOperations.Swap(cpukey0));
            var key = new byte[0x10];
            Buffer.BlockCopy(tmp, 0, key, 0, tmp.Length);
            tmp = BitConverter.GetBytes(BitOperations.Swap(cpukey1));
            Buffer.BlockCopy(tmp, 0, key, tmp.Length, tmp.Length);
            var key2 = new byte[key.Length];
            Buffer.BlockCopy(key, 0, key2, 0, key.Length);
            CalculateCpuKeyEcd(ref key2);
            if(!BitOperations.CompareByteArrays(ref key, ref key2))
                throw new CpuKeyException(CpuKeyException.ExceptionTypes.Ecd);
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
                catch(CpuKeyException) {}
            }
            return false;
        }

        public bool ReadFusefile(string file, out string cpukey, out int ldv) {
            var fuse = new Fuse(file);
            ldv = fuse.CFLDV;
            cpukey = "";
            try {
                cpukey = fuse.CPUKey;
                VerifyCpuKey(cpukey);
                return true;
            }
            catch(CpuKeyException) {}
            return false;
        }

        public string GetCpuKeyFromTextFile(string file) {
            string cpukey;
            int ldv;
            if(!ReadKeyfile(file, out cpukey) && !ReadFusefile(file, out cpukey, out ldv))
                throw new CpuKeyException(CpuKeyException.ExceptionTypes.NoValidKeyFound);
            return cpukey;
        }
    }
}
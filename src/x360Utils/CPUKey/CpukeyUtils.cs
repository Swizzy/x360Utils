#region

using System;
using System.Globalization;
using System.IO;
using x360Utils.Common;

#endregion

namespace x360Utils.CPUKey {
    public sealed class CpukeyUtils {
        private static Random _random = new Random((int) (DateTime.Now.Ticks & 0xFFFF));

        public static void UpdateRandom(int seed) {
            _random = new Random(seed);
        }

        public byte[] GenerateRandomCPUKey() {
            var key = new byte[0x10];
            do {
                _random.NextBytes(key);
                if (BitOperations.DataIsZero(ref key, 0, key.Length))
                    UpdateRandom((int) (DateTime.Now.Ticks & 0xFFFF));
                try
                {
                    VerifyCpuKey(ref key);
                    return key;
                }
                catch (X360UtilsException)
                {
                }
            } while (true);
        }

        private static void CalculateCPUKeyECD(ref byte[] key) {
            uint acc1 = 0, acc2 = 0;
            for (var cnt = 0; cnt < 0x80; cnt++, acc1 >>= 1) {
                var bTmp = key[cnt >> 3];
                var dwTmp = (uint) ((bTmp >> (cnt & 7)) & 1);
                if (cnt < 0x6A) {
                    acc1 = dwTmp ^ acc1;
                    if ((acc1 & 1) > 0)
                        acc1 = acc1 ^ 0x360325;
                    acc2 = dwTmp ^ acc2;
                }
                else if (cnt < 0x7F) {
                    if (dwTmp != (acc1 & 1))
                        key[(cnt >> 3)] = (byte) ((1 << (cnt & 7)) ^ (bTmp & 0xFF));
                    acc2 = (acc1 & 1) ^ acc2;
                }
                else if (dwTmp != acc2)
                    key[0xF] = (byte) ((0x80 ^ bTmp) & 0xFF);
            }
        }

        public void VerifyCpuKey(string cpukey) {
            cpukey = cpukey.Trim();
            var tmp = StringUtils.HexToArray(cpukey);
            VerifyCpuKey(ref tmp);
        }

        public void VerifyCpuKey(ref byte[] cpukey) {
            if (cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyTooShort);
            if (cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyTooLong);
            var tmp = BitConverter.ToUInt64(cpukey, 0);
            var hamming = BitOperations.CountSetBits(tmp);
            tmp = BitOperations.Swap(BitConverter.ToUInt64(cpukey, 8));
            hamming += BitOperations.CountSetBits(tmp & 0xFFFFFFFFFF030000);
            if (hamming != 53)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyInvalidHamming);
            var key2 = new byte[0x10];
            Buffer.BlockCopy(cpukey, 0, key2, 0, cpukey.Length);
            CalculateCPUKeyECD(ref key2);
            if (!BitOperations.CompareByteArrays(ref cpukey, ref key2))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyInvalidECD);
        }

        public bool ReadKeyfile(string file, out string cpukey) {
            cpukey = "";
            using (var sr = new StreamReader(file)) {
                var key = sr.ReadLine();
                if (key != null &&
                    ((key.Trim().IndexOf("cpukey", StringComparison.CurrentCultureIgnoreCase) >= 0) &&
                     (key.Trim().Length == 38)))
                    key = key.Trim().Substring(key.Trim().Length - 32, 32);
                if (string.IsNullOrEmpty(key) || !StringUtils.StringIsHex(key))
                    return false;
                cpukey = key.Trim().ToUpper();
                try {
                    VerifyCpuKey(cpukey);
                    return true;
                }
                catch (X360UtilsException) {
                    return false;
                }
            }
        }

        public bool ReadFusefile(string file, out string cpukey) {
            cpukey = "";
            var val = "";
            UInt64 key1 = 0, key2 = 0, key3 = 0, key4 = 0;
            using (var sr = new StreamReader(file)) {
                while (val != null) {
                    val = sr.ReadLine();
                    if (string.IsNullOrEmpty(val))
                        continue;
                    if (val.StartsWith("fuseset 03:", StringComparison.CurrentCultureIgnoreCase))
                        UInt64.TryParse(val.Remove(0, 11), NumberStyles.HexNumber, null, out key1);
                    else if (val.StartsWith("fuseset 04:", StringComparison.CurrentCultureIgnoreCase))
                        UInt64.TryParse(val.Remove(0, 11), NumberStyles.HexNumber, null, out key2);
                    else if (val.StartsWith("fuseset 05:", StringComparison.CurrentCultureIgnoreCase))
                        UInt64.TryParse(val.Remove(0, 11), NumberStyles.HexNumber, null, out key3);
                    else if (val.StartsWith("fuseset 06:", StringComparison.CurrentCultureIgnoreCase))
                        UInt64.TryParse(val.Remove(0, 11), NumberStyles.HexNumber, null, out key4);
                }
                sr.Close();
            }
            if (key1 == 0 || key2 == 0 || key3 == 0 || key4 == 0)
                return false;
            cpukey = (key1 | key2).ToString("X16") + (key3 | key4).ToString("X16");
            try
            {
                VerifyCpuKey(cpukey);
                return true;
            }
            catch (X360UtilsException)
            {
                return false;
            }
        }

        public void GetCPUKeyFromTextFile(string file, out string cpukey) {
            if (!ReadKeyfile(file, out cpukey) && !ReadFusefile(file, out cpukey))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.KeyFileNoKeyFound);
        }
    }
}
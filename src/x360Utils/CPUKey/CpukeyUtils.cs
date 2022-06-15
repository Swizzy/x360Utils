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
            }
            while(!TryVerifyCPUKeyHammingWeight(ref key));
            CalculateCPUKeyECD(ref key);
            return key;
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

		private static bool TryVerifyCPUKeyECD(ref byte[] cpukey)
		{
			if (cpukey is null || cpukey.Length != 0x10)
				return false;
            var scratch = new byte[0x10];
            Buffer.BlockCopy(cpukey, 0, scratch, 0, cpukey.Length);
            CalculateCPUKeyECD(ref scratch);
			return !BitOperations.CompareByteArrays(ref cpukey, ref scratch);
		}

		private static void VerifyCPUKeyECD(ref byte[] cpukey) {
			if (cpukey is null)
				throw new ArgumentNullException(nameof(cpukey));
			if (!TryVerifyCPUKeyECD(ref cpukey))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.InvalidKeyECD);
        }

		private static bool TryVerifyCPUKeyHammingWeight(ref byte[] cpukey)
		{
			if (cpukey is null || cpukey.Length != 0x10)
				return false;
			return TryVerifyCPUKeyHammingWeight(BitOperations.Swap(BitConverter.ToUInt64(cpukey, 0)), BitOperations.Swap(BitConverter.ToUInt64(cpukey, 8)));
		}

		private static bool TryVerifyCPUKeyHammingWeight(UInt64 part0, UInt64 part1)
		{
			return BitOperations.CountSetBits(part0) + BitOperations.CountSetBits(part1 & 0xFFFFFFFFFF030000) == 53;
		}

		private static void VerifyCPUKeyHammingWeight(ref byte[] cpukey) {
			if (cpukey is null)
				throw new ArgumentNullException(nameof(cpukey));
			if (!TryVerifyCPUKeyHammingWeight(BitOperations.Swap(BitConverter.ToUInt64(cpukey, 0)), BitOperations.Swap(BitConverter.ToUInt64(cpukey, 8))))
				throw new X360UtilsException(X360UtilsException.X360UtilsErrors.InvalidKeyHamming);
		}

		private static void VerifyCPUKeyHammingWeight(UInt64 part0, UInt64 part1)
		{
			if (!TryVerifyCPUKeyHammingWeight(part0, part1))
				throw new X360UtilsException(X360UtilsException.X360UtilsErrors.InvalidKeyHamming);
		}

		/// <summary>
		/// Verify a CPUKey, without throwing exceptions.
		/// </summary>
		/// <returns>true if the CPUKey is valid, otherwise false</returns>
		public static bool TryVerifyCPUKey(string cpukey)
		{
			if (cpukey is null)
				return false;

			cpukey = cpukey.Trim();
			if (String.IsNullOrEmpty(cpukey))
				return false;

			var tmp = StringUtils.HexToArray(cpukey.Trim());
			return TryVerifyCPUKey(ref tmp);
		}

		/// <summary>
		/// Verify a CPUKey, without throwing exceptions.
		/// </summary>
		/// <returns>true if the CPUKey is valid, otherwise false</returns>
		public static bool TryVerifyCPUKey(ref byte[] cpukey)
		{
			if (cpukey is null || cpukey.Length != 0x10)
				return false;
			return (TryVerifyCPUKeyHammingWeight(ref cpukey) && TryVerifyCPUKeyECD(ref cpukey));
		}

		/// <summary>
		/// Verify a CPUKey, without throwing exceptions.
		/// </summary>
		/// <returns>true if the CPUKey is valid, otherwise false</returns>
		public static bool TryVerifyCPUKey(UInt64 part0, UInt64 part1)
		{
			if (!TryVerifyCPUKeyHammingWeight(part0, part1))
				return false;

			var key = new byte[0x10];
			var tmp = BitConverter.GetBytes(BitOperations.Swap(part0));
			Buffer.BlockCopy(tmp, 0, key, 0, tmp.Length);
			tmp = BitConverter.GetBytes(BitOperations.Swap(part1));
			Buffer.BlockCopy(tmp, 0, key, tmp.Length, tmp.Length);
			return TryVerifyCPUKeyECD(ref key);
		}

		/// <summary>
		/// Verify a CPUKey. Throws an <see cref="X360UtilsException"/> if verification fails,
		/// which can be examined to determine the specific cause of failure.
		/// </summary>
		/// <param name="cpukey"></param>
		/// <exception cref="X360UtilsException">Throws if CPUKey is wrong length (0x20 chars), or invalid hamming/ECD</exception>
		public static void VerifyCpuKey(string cpukey) {
			if (cpukey is null)
				throw new ArgumentNullException(nameof(cpukey));

			cpukey = cpukey.Trim();
			if (String.IsNullOrEmpty(cpukey))
				throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooShortKey);

			var tmp = StringUtils.HexToArray(cpukey);
            VerifyCpuKey(ref tmp);
        }

		/// <summary>
		/// Verify a CPUKey. Throws an <see cref="X360UtilsException"/> if verification fails,
		/// which can be examined to determine the specific cause of failure.
		/// </summary>
		/// <exception cref="X360UtilsException">Throws if CPUKey is wrong length (0x10 bytes), or invalid hamming/ECD</exception>
		public static void VerifyCpuKey(ref byte[] cpukey) {
			if (cpukey is null)
				throw new ArgumentNullException(nameof(cpukey));
			if (cpukey.Length < 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooShortKey);
            if(cpukey.Length > 0x10)
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.TooLongKey);
            VerifyCPUKeyHammingWeight(ref cpukey);
            VerifyCPUKeyECD(ref cpukey);
        }

		/// <summary>
		/// Verify a CPUKey. Throws an <see cref="X360UtilsException"/> if verification fails,
		/// which can be examined to determine the specific cause of failure.
		/// </summary>
		/// <exception cref="X360UtilsException">Throws if CPUKey is wrong length (0x10 bytes), or invalid hamming/ECD</exception>
		public static void VerifyCpuKey(UInt64 part0, UInt64 part1) {
            VerifyCPUKeyHammingWeight(part0, part1);
            var key = new byte[0x10];
            var tmp = BitConverter.GetBytes(BitOperations.Swap(part0));
            Buffer.BlockCopy(tmp, 0, key, 0, tmp.Length);
            tmp = BitConverter.GetBytes(BitOperations.Swap(part1));
            Buffer.BlockCopy(tmp, 0, key, tmp.Length, tmp.Length);
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

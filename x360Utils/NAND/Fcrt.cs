namespace x360Utils.NAND {
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using x360Utils.Common;

    public class Fcrt {
        public Fcrt(byte[] fcrt) { Data = fcrt; }

        public bool Encrypted { get; private set; }

        public byte[] Data { get; private set; }

        #region Crypto

        private UInt32 DataOffset { get { return BitOperations.Swap(BitConverter.ToUInt32(Data, 0x11C)); } }

        private UInt32 DataLength { get { return BitOperations.Swap(BitConverter.ToUInt32(Data, 0x118)); } }

        private byte[] Iv {
            get {
                var buf = new byte[0x10];
                Array.Copy(Data, 0x100, buf, 0, buf.Length);
                return buf;
            }
        }

        private static RijndaelManaged GetAes(byte[] key, byte[] iv) {
            return new RijndaelManaged {
                                           Mode = CipherMode.CBC,
                                           KeySize = 128,
                                           Key = key,
                                           IV = iv,
                                           Padding = PaddingMode.None
                                       };
        }

        public bool VerifyDecrypted() {
            Main.SendInfo(Main.VerbosityLevels.Medium, "Verifying FCRT Decryption...");
            if(Data == null)
                throw new InvalidOperationException("Data can't be null!");
            if(Data.Length < 0x140)
                throw new InvalidOperationException("Data is far to short!");
            using(var sha1 = new SHA1Managed()) {
                var hash = new byte[0x14];
                Buffer.BlockCopy(Data, 0x12C, hash, 0, hash.Length);
                var hash2 = sha1.ComputeHash(Data, (int)DataOffset, (int)DataLength);
                if(!BitOperations.CompareByteArrays(ref hash, ref hash2)) {
                    Main.SendInfo(Main.VerbosityLevels.Medium, "Verification failed, Expected data: {0} Actual data: {1}", StringUtils.ArrayToHex(hash), StringUtils.ArrayToHex(hash2));
                    Encrypted = true;
                }
                else {
                    Encrypted = false;
                    Main.SendInfo(Main.VerbosityLevels.Medium, "Verification success!");
                }
                return !Encrypted;
            }
        }

        public void Decrypt(string key) { Decrypt(StringUtils.HexToArray(key)); }

        public void Decrypt(byte[] key) {
            if(Data == null)
                throw new InvalidOperationException("Data can't be null!");
            if(Data.Length < 0x140)
                throw new InvalidOperationException("Data is far to short!");
            var offset = (int)DataOffset;
            var length = (int)DataLength;
            if(Data.Length < offset + length)
                throw new InvalidOperationException("Data is to short/invalid");
            using(var aes = GetAes(key, Iv)) {
                var buf = aes.CreateDecryptor().TransformFinalBlock(Data, offset, length);
                Buffer.BlockCopy(buf, 0, Data, offset, buf.Length);
            }
            if(!VerifyDecrypted())
                throw new Exception("Decryption failed");
        }

        public void Encrypt(string key) { Encrypt(StringUtils.HexToArray(key)); }

        public void Encrypt(byte[] key) {
            if(Data == null)
                throw new InvalidOperationException("Data can't be null!");
            if(Data.Length < 0x140)
                throw new InvalidOperationException("Data is far to short!");
            var offset = (int)DataOffset;
            var length = (int)DataLength;
            if(Data.Length < offset + length)
                throw new InvalidOperationException("Data is to short/invalid");
            using(var aes = GetAes(key, Iv)) {
                var buf = aes.CreateEncryptor().TransformFinalBlock(Data, offset, length);
                Buffer.BlockCopy(buf, 0, Data, offset, buf.Length);
            }
        }

        #endregion

        private static byte SwapBits(byte chunk, IList<int> bits) {
            byte num = 0;
            for(var i = 0; i < 8; i++) {
                var num3 = (byte)((chunk & 1 << (bits[i] & 0x1f)) >> bits[i]);
                num = (byte)(num << 1 | num3);
            }
            return num;
        }

        public byte[] Cr {
            get {
                if(Encrypted)
                    throw new InvalidOperationException("You must decrypt the FCRT first!");
                var xorTable = Main.GetEmbeddedResource("NAND.FCRTXorTable.bin");
                var cr = new List<byte>();
                var tmp = new byte[0x10];
#if CRRandom
            foreach(var i in GetRandomOrder((decryptedFcrt.Length - 0x140) / 0x20)) {
#else
                for(var i = 0; i < (Data.Length - 0x140) / 0x20; i++) {
#endif
                    cr.Add(Data[(i * 0x20) + 0x140]);
                    cr.Add(Data[(i * 0x20) + 0x140 + 1]);
                    cr.Add(Data[(i * 0x20) + 0x140 + 15]);
                    Buffer.BlockCopy(Data, (i * 0x20) + 0x140 + 0x10, tmp, 0, tmp.Length);
                    cr.AddRange(tmp);
                }
                tmp = cr.ToArray();
                var bits = new[] {
                                     3, 2, 7, 6, 1, 0, 5, 4
                                 };
                for(var i = 0; i < tmp.Length; i++)
                    tmp[i] = SwapBits((byte)(tmp[i] ^ xorTable[i]), bits);
                return tmp;
            }
        }

#if CRRandom
        private static IEnumerable<int> GetRandomOrder(int max) {
            var list = new List<int>();
            var rnd = new Random();
            for(var i = 0; i < max; i++) {
                while(true) {
                    var val = rnd.Next(0, max);
                    if(list.Contains(val))
                        continue;
                    list.Add(val);
                    break;
                }
            }
            return list.ToArray();
        }
#endif
    }
}
namespace x360Utils.NAND {
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using x360Utils.Common;

    public class Keyvault {
        public enum DateFormats {
            // ReSharper disable InconsistentNaming
            YYMMDD,
            DDMMYY,
            MMYYDD,
            DDYYMM,
            MMDDYY,
            YYDDMM
            // ReSharper restore InconsistentNaming
        }

        public Keyvault(byte[] kv, bool encrypted = true) {
            Data = kv;
            Encrypted = encrypted;
        }

        private Keyvault(byte[] kv, byte[] cpukey) {
            Data = kv;
            Decrypt(cpukey);
        }

        private Keyvault(byte[] kv, string cpukey) {
            Data = kv;
            Decrypt(cpukey);
        }

        public bool Encrypted { get; private set; }

        public byte[] Data { get; private set; }

        public ushort FcrtFlag { get { return BitOperations.Swap(BitConverter.ToUInt16(Data, 0x1C)); } }

        public bool FcrtRequired { get { return (FcrtFlag & 0x120) == 0x120; } }

        public bool FcrtUsed { get { return (FcrtFlag & 0x20) == 0x20; } }

        public string GameRegion { get { return Translators.TranslateGameRegion(GameRegionHex); } }

        public string GameRegionFull { get { return Translators.TranslateGameRegion(GameRegionHex, true); } }

        public string GameRegionHex { get { return string.Format("0x{0:X2}{1:X2}", Data[0xC8], Data[0xC9]); } }

        public string DvdKey { get { return StringUtils.ArrayToHex(Data, 0x100, 0x10); } }

        public byte[] DvdKeyBytes {
            get {
                var ret = new byte[0x10];
                Buffer.BlockCopy(Data, 0x100, ret, 0, ret.Length);
                return ret;
            }
        }

        public string ConsoleId { get { return StringUtils.ArrayToHex(Data, 0x9CA, 0x6); } }

        public string MfrDate { get { return GetMfrDate(DateFormats.YYMMDD); } }

        private string OsigData { get { return StringUtils.GetAciiString(Data, 0xC92, 0x1C, true); } }

        private string ConsoleSerial { get { return StringUtils.GetAciiString(Data, 0xB0, 0x10); } }

        #region Crypto

        public void Decrypt(string key) { Decrypt(StringUtils.HexToArray(key)); }

        public void Decrypt(byte[] key) { DoCrypto(key); }

        public void Encrypt(string key) { Encrypt(StringUtils.HexToArray(key)); }

        public void Encrypt(byte[] key) { DoCrypto(key, false); }

        private void DoCrypto(byte[] key, bool decrypt = true) {
            if((!Encrypted && decrypt) || (Encrypted && !decrypt))
                return;
            Main.SendInfo(Main.VerbosityLevels.Medium, "Decrypting KV with key: {0}", StringUtils.ArrayToHex(key));
            if(Data == null)
                throw new InvalidOperationException("_kvData can't be null");
            if(Data.Length != 0x4000)
                throw new InvalidOperationException("_kvData should be 0x4000 bytes");
            if(key.Length != 0x10)
                throw new ArgumentOutOfRangeException("key");
            var tmp = new byte[Data.Length - 0x10];
            var header = new byte[0x10];
            Array.Copy(Data, 0x0, header, 0x0, 0x10);
            Buffer.BlockCopy(Data, 0x10, tmp, 0x0, tmp.Length);
            key = new HMACSHA1(key).ComputeHash(header);
            Array.Resize(ref key, 0x10);
            Main.SendInfo(Main.VerbosityLevels.Debug, "Cipher key: {0}", StringUtils.ArrayToHex(key));
            Rc4.Compute(ref tmp, key);
            Array.Copy(header, Data, header.Length);
            Buffer.BlockCopy(tmp, 0x0, Data, header.Length, tmp.Length);
            Encrypted = !VerifyDecrypted(key);
            if(Encrypted && decrypt)
                throw new Exception("Decryption failed");
        }

        public bool VerifyDecrypted(string key) { return VerifyDecrypted(StringUtils.HexToArray(key)); }

        public bool VerifyDecrypted(byte[] key) {
            Main.SendInfo(Main.VerbosityLevels.Medium, "Verifying KV Decryption with key: {0}", StringUtils.ArrayToHex(key));
            if(Data == null)
                throw new InvalidOperationException("Data can't be null");
            if(Data.Length != 0x4000)
                throw new InvalidOperationException("Data should be 0x4000 bytes");
            if(key.Length != 0x10)
                throw new ArgumentOutOfRangeException("key");
            byte[] header = new byte[0x10], tmp = new byte[(Data.Length - 0x10) + 2];
            Array.Copy(Data, 0x0, header, 0x0, 0x10);
            Buffer.BlockCopy(Data, 0x10, tmp, 0x0, Data.Length - 0x10);
            tmp[Data.Length - 0x10] = 0x7;
            tmp[Data.Length - 0xF] = 0x12;
            var checkdata = new HMACSHA1(key).ComputeHash(tmp);
            Array.Resize(ref checkdata, 0x10);
            if(!BitOperations.CompareByteArrays(ref checkdata, ref header)) {
                Main.SendInfo(Main.VerbosityLevels.Medium, "Verification failed, Expected data: {0} Actual data: {1}", StringUtils.ArrayToHex(header), StringUtils.ArrayToHex(checkdata));
                return false;
            }
            Main.SendInfo(Main.VerbosityLevels.Medium, "Verification success!");
            return true;
        }

        #endregion

        public string GetMfrDate(DateFormats format) {
            var ret = Encoding.ASCII.GetString(Data, 0x9E4, 8);
            if(!Regex.IsMatch(ret, "^[0-9]{2}-[0-9]{2}-[0-9]{2}$"))
                throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid);
            var split = ret.Split('-');
            switch(format) {
                case DateFormats.YYMMDD:
                    return string.Format("{0}-{1}-{2}", split[1], split[0], split[2]);
                case DateFormats.DDMMYY:
                    return string.Format("{0}-{1}-{2}", split[2], split[0], split[1]);
                case DateFormats.MMYYDD:
                    return string.Format("{0}-{1}-{2}", split[0], split[1], split[2]);
                case DateFormats.DDYYMM:
                    return string.Format("{0}-{1}-{2}", split[2], split[1], split[0]);
                case DateFormats.MMDDYY:
                    return string.Format("{0}-{1}-{2}", split[0], split[2], split[1]);
                case DateFormats.YYDDMM:
                    return string.Format("{0}-{1}-{2}", split[1], split[2], split[0]);
                default:
                    throw new ArgumentOutOfRangeException("format");
            }
        }
    }
}
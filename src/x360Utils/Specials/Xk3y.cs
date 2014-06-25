namespace x360Utils.Specials {
    using System;
    using System.IO;
    using x360Utils.Common;
    using x360Utils.NAND;

    // ReSharper disable InconsistentNaming
    public class Xk3y {
        private readonly Cryptography _crypto = new Cryptography();
        private readonly Keyvault _kvutils = new Keyvault();
        private readonly X360NAND _nand = new X360NAND();

        private static string TranslateOsigToFile(string osig) {
            if(osig.Contains("0500"))
                return "0500.txt";
            if(osig.Contains("0502"))
                return "0502.txt";
            if(osig.Contains("1175"))
                return "1175.txt";
            if(osig.Contains("1532"))
                return "1532.txt";
            throw new NotSupportedException(string.Format("This OSIG isn't supported by this version of the x360Utils/xk3y: {0}", osig));
        }

        public void ExtractXk3yCompatibleFiles(string nandfile, string cpukey, string outdir) { ExtractXk3yCompatibleFiles(new NANDReader(nandfile), StringUtils.HexToArray(cpukey), outdir); }

        public void ExtractXk3yCompatibleFiles(string nandfile, byte[] cpukey, string outdir) { ExtractXk3yCompatibleFiles(new NANDReader(nandfile), cpukey, outdir); }

        public void ExtractXk3yCompatibleFiles(NANDReader nandreader, string cpukey, string outdir) { ExtractXk3yCompatibleFiles(nandreader, StringUtils.HexToArray(cpukey), outdir); }

        public void ExtractXk3yCompatibleFiles(NANDReader nandReader, byte[] cpukey, string outdir) {
            var origdir = Directory.GetCurrentDirectory();
            try {
                if(!Directory.Exists(outdir))
                    Directory.CreateDirectory(outdir);
                Directory.SetCurrentDirectory(outdir);
                var fcrt = _nand.GetFcrt(nandReader);
                var tmp = new byte[fcrt.Length];
                Buffer.BlockCopy(fcrt, 0, tmp, 0, fcrt.Length);
                _crypto.DecryptFcrt(ref tmp, cpukey);
                if(!_crypto.VerifyFcrtDecrypted(ref tmp))
                    throw new X360UtilsException(X360UtilsException.X360UtilsErrors.DataInvalid, "FCRT Can't be verified to be for this cpukey!");
                File.WriteAllBytes("fcrt_enc.bin", fcrt);
                var kv = _nand.GetKeyVault(nandReader, cpukey);
                File.WriteAllText("dvd.txt", _kvutils.GetDVDKey(ref kv));
                File.WriteAllText("cpu.txt", StringUtils.ArrayToHex(cpukey));
                File.WriteAllText(TranslateOsigToFile(_kvutils.GetOSIGData(ref kv)), "");
            }
            finally {
                Directory.SetCurrentDirectory(origdir);
            }
        }
    }
}
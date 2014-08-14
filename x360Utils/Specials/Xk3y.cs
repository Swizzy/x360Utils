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
            osig = osig.Trim().ToUpper();
            //I've commented out the match for DG-16D4S because they're not supported yet by xk3y afaik (with this "style")
            if (osig.StartsWith("PLDS") && (osig.Contains("DG-16D5S") /*|| osig.Contains("DG-16D4S")*/))
                return osig.Substring(osig.LastIndexOf(' ')) + ".txt"; // We found a Liteon DG-16D5S/Liteon DG-16D4S, they always have the version at the end...
            if(osig.StartsWith("HL-DT-STDVD-ROM") && osig.Contains("DL10N"))
                return osig.Substring(osig.LastIndexOf(' ')) + ".txt"; // We found a Hitachi DL10N, they always have the version at the end...
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
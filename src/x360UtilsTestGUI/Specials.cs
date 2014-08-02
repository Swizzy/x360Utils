namespace x360UtilsTestGUI {
    using System;
    using System.ComponentModel;
    using System.IO;
    using System.Windows.Forms;
    using x360Utils.CPUKey;
    using x360Utils.NAND;
    using x360Utils.Specials;

    public partial class Specials: Form {
        private readonly JungleFlasher _jf = new JungleFlasher();
        private readonly CpukeyUtils _keyUtils = new CpukeyUtils();
        private readonly Xk3y _xk = new Xk3y();
        private readonly X360NAND _nand = new X360NAND();
        private readonly Cryptography _crypto = new Cryptography();
        private readonly Keyvault _kv = new Keyvault();

        public Specials() { InitializeComponent(); }

        private void Process(bool jf, bool spider = false) {
            var ofd = new OpenFileDialog {
                                             FileName = "flashdmp.bin"
                                         };
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var nand = ofd.FileName;
            ofd.FileName = "cpukey.txt";
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var key = _keyUtils.GetCPUKeyFromTextFile(ofd.FileName);
            var fbd = new FolderBrowserDialog();
            if(fbd.ShowDialog() != DialogResult.OK)
                return;
            var outdir = fbd.SelectedPath;
            var bw = new BackgroundWorker();
            bw.RunWorkerCompleted += (sender, args) => {
                                         if(args.Error != null)
                                             throw args.Error;
                                     };
            if(jf && !spider)
                bw.DoWork += (o, eventArgs) => _jf.ExtractJungleFlasherData(nand, key, outdir);
            else if (!spider)
                bw.DoWork += (o, eventArgs) => _xk.ExtractXk3yCompatibleFiles(nand, key, outdir);
            else {
                bw.DoWork += (sender, args) => {
                    Directory.SetCurrentDirectory(outdir);
                                 var reader = new NANDReader(nand);
                                 var fcrt = _nand.GetFcrt(reader);
                                 _crypto.DecryptFcrt(ref fcrt, x360Utils.Common.StringUtils.HexToArray(key));
                                 if(_crypto.VerifyFcrtDecrypted(ref fcrt))
                                     File.WriteAllBytes("fcrt_dec.bin", fcrt);
                                 var kv = _nand.GetKeyVault(reader, key);
                                 if (_crypto.VerifyKvDecrypted(ref kv, key))
                                     File.WriteAllText("dvdkey.txt", _kv.GetDVDKey(ref kv));

                             };
            }
            bw.RunWorkerAsync();
        }

        private void JfbtnClick(object sender, EventArgs e) { Process(true); }

        private void xkbtn_Click(object sender, EventArgs e) { Process(false); }

        private void spiderbtn_Click(object sender, EventArgs e)
        {
            Process(true, true);
        }
    }
}
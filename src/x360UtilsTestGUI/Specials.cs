namespace x360UtilsTestGUI {
    using System;
    using System.ComponentModel;
    using System.Windows.Forms;
    using x360Utils.CPUKey;
    using x360Utils.Specials;

    public partial class Specials: Form {
        private readonly JungleFlasher _jf = new JungleFlasher();
        private readonly CpukeyUtils _keyUtils = new CpukeyUtils();
        private readonly Xk3y _xk = new Xk3y();

        public Specials() { InitializeComponent(); }

        private void Process(bool jf) {
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
            if(jf)
                bw.DoWork += (o, eventArgs) => _jf.ExtractJungleFlasherData(nand, key, outdir);
            else
                bw.DoWork += (o, eventArgs) => _xk.ExtractXk3yCompatibleFiles(nand, key, outdir);
            bw.RunWorkerAsync();
        }

        private void JfbtnClick(object sender, EventArgs e) { Process(true); }

        private void xkbtn_Click(object sender, EventArgs e) { Process(false); }
    }
}
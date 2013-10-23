namespace x360UtilsTestGUI {
    using System;
    using System.ComponentModel;
    using System.Reflection;
    using System.Windows.Forms;
    using x360Utils;
    using x360Utils.NAND;

    internal sealed partial class MainForm : Form {
        private readonly X360NAND x360NAND = new X360NAND();

        internal MainForm() {
            InitializeComponent();
            dllversionlbl.Text = Main.Version;
            var version = Assembly.GetAssembly(typeof(MainForm)).GetName().Version;
            Debug.DebugOutput += DebugOnDebugOutput;
            Text = string.Format(Text, version.Major, version.Minor, version.Build);
        }

        private void DebugOnDebugOutput(object sender, EventArg<string> eventArg) {
            try {
                if(!InvokeRequired) {
                    debugbox.AppendText(string.Format("[DEBUG] {0}{1}", eventArg.Data, Environment.NewLine));
                    debugbox.Select(debugbox.Text.Length, 0);
                    debugbox.ScrollToCaret();
                }
                else
                    Invoke(new MethodInvoker(() => DebugOnDebugOutput(sender, eventArg)));
            }
            catch(Exception) {
            }
        }

        private void AddOutput(string output) {
            try {
                if(!InvokeRequired) {
                    outbox.AppendText(output);
                    outbox.Select(outbox.Text.Length, 0);
                    outbox.ScrollToCaret();
                }
                else
                    Invoke(new MethodInvoker(() => AddOutput(output)));
            }
            catch(Exception) {
            }
        }

        private void AddException(string exception) {
            try {
                if(!InvokeRequired) {
                    debugbox.AppendText(string.Format("[EXCEPTION]{1}{0}{1}", exception, Environment.NewLine));
                    debugbox.Select(debugbox.Text.Length, 0);
                    debugbox.ScrollToCaret();
                }
                else
                    Invoke(new MethodInvoker(() => AddException(exception)));
            }
            catch(Exception) {
            }
        }

        private void GetKeyBtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            using(var reader = new NANDReader(ofd.FileName)) {
                try {
                    AddOutput("Grabbing CPUKey from NAND: ");
                    AddOutput(x360NAND.GetNANDCPUKey(reader));
                }
                catch(X360UtilsException ex) {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
            }
            AddOutput(Environment.NewLine);
        }

        private void GetfusebtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            using(var reader = new NANDReader(ofd.FileName)) {
                try {
                    AddOutput("Grabbing FUSES from NAND: ");
                    AddOutput(Environment.NewLine);
                    AddOutput(x360NAND.GetVirtualFuses(reader));
                }
                catch(X360UtilsException ex) {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
            }
            AddOutput(Environment.NewLine);
        }

        private void ClearToolStripMenuItemClick(object sender, EventArgs e) {
            var rbox = outmenu.SourceControl as RichTextBox;
            if(rbox != null)
                rbox.Clear();
            var tbox = outmenu.SourceControl as TextBox;
            if(tbox != null)
                tbox.Clear();
        }

        private void GetlaunchinibtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += Getlaunchini;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void Getlaunchini(object sender, DoWorkEventArgs e) {
            using(var reader = new NANDReader(e.Argument as string)) {
                try {
                    AddOutput("Grabbing Launch.ini from NAND: ");
                    AddOutput(Environment.NewLine);
                    AddOutput(x360NAND.GetLaunchIni(reader));
                }
                catch(X360UtilsException ex) {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
            }
            AddOutput(Environment.NewLine);
        }

        private void GetbadblocksbtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += GetBadblocks;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void GetBadblocks(object sender, DoWorkEventArgs e) {
            using(var reader = new NANDReader(e.Argument as string)) {
                try {
                    AddOutput("Grabbing BadBlock info from NAND: ");
                    var blocks = reader.FindBadBlocks();
                    foreach(var block in blocks)
                        AddOutput(string.Format("{1}BadBlock @ 0x{0:X}", block, Environment.NewLine));
                }
                catch(X360UtilsException ex) {
                    if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataNotFound) {
                        AddOutput("FAILED!");
                        AddException(ex.ToString());
                    }
                    else
                        AddOutput("No BadBlocks Found!");
                }
            }
            AddOutput(Environment.NewLine);
        }
    }
}
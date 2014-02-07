namespace x360UtilsTestGUI {
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Reflection;
    using System.Windows.Forms;
    using x360Utils;
    using x360Utils.NAND;
    using Debug = x360Utils.Debug;

    internal sealed partial class MainForm : Form {
        private readonly X360NAND _x360NAND = new X360NAND();
        private Stopwatch _sw;

        internal MainForm() {
            InitializeComponent();
            dllversionlbl.Text = Main.Version;
            var version = Assembly.GetAssembly(typeof(MainForm)).GetName().Version;
            Debug.DebugOutput += DebugOnDebugOutput;
            Main.InfoOutput += MainOnInfoOutput;
            Text = string.Format(Text, version.Major, version.Minor, version.Build);
            Main.VerbosityLevel = int.MaxValue;
        }

        private void MainOnInfoOutput(object sender, EventArg<string> eventArg) { AddOutput(eventArg.Data); }

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

        private void AddOutput(string output, params object[] args) {
            try {
                if(!InvokeRequired) {
                    outbox.AppendText(string.Format(output, args));
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

        private void AddDone() {
            _sw.Stop();
            AddOutput("Took: {0} Minutes {1} Seconds {2} Milliseconds\r\n", _sw.Elapsed.Minutes, _sw.Elapsed.Seconds, _sw.Elapsed.Milliseconds);
        }

        private void GetKeyBtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            using(var reader = new NANDReader(ofd.FileName)) {
                try {
                    AddOutput("Grabbing CPUKey from NAND: ");
                    AddOutput(_x360NAND.GetNANDCPUKey(reader));
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
                    AddOutput(_x360NAND.GetVirtualFuses(reader));
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
            _sw = Stopwatch.StartNew();
            using(var reader = new NANDReader(e.Argument as string)) {
                try {
                    AddOutput("Grabbing Launch.ini from NAND: ");
                    AddOutput("{0}{1}", Environment.NewLine, _x360NAND.GetLaunchIni(reader));
                }
                catch(X360UtilsException ex) {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
            }
            AddOutput(Environment.NewLine);
            AddDone();
        }

        private void GetbadblocksbtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += GetBadblocks;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void GetBadblocks(object sender, DoWorkEventArgs e) {
            _sw = Stopwatch.StartNew();
            using(var reader = new NANDReader(e.Argument as string)) {
                try {
                    AddOutput("Grabbing BadBlock info from NAND: ");
                    var blocks = reader.FindBadBlocks();
                    foreach(var block in blocks)
                        AddOutput("{1}BadBlock @ 0x{0:X}", block, Environment.NewLine);
                }
                catch(X360UtilsException ex) {
                    if(ex.ErrorCode != X360UtilsException.X360UtilsErrors.DataNotFound) {
                        AddOutput("FAILED!\r\n");
                        AddException(ex.ToString());
                    }
                    else
                        AddOutput("No BadBlocks Found!\r\n");
                }
                catch(NotSupportedException) {
                    AddOutput("Not Supported for this image type!");
                }
            }
            AddOutput(Environment.NewLine);
            AddDone();
        }

        private static void TestMetaUtils(object sender, DoWorkEventArgs e) {
            var spareUtils = new NANDSpare();
            spareUtils.TestMetaUtils(e.Argument as string);
        }

        private void GetsmcconfigbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            using(var reader = new NANDReader(ofd.FileName)) {
                try {
                    AddOutput("Grabbing SMC_Config from NAND: ");
                    var cfg = _x360NAND.GetSMCConfig(reader);
                    var config = new SMCConfig();
                    AddOutput("\r\nChecksum: {0}", config.GetCheckSum(ref cfg));
                    AddOutput("\r\nDVDRegion: {0}", config.GetDVDRegion(ref cfg));
                    AddOutput("\r\nCPUFanSpeed: {0}", config.GetFanSpeed(ref cfg, SMCConfig.SMCConfigFans.CPU));
                    AddOutput("\r\nGPUFanSpeed: {0}", config.GetFanSpeed(ref cfg, SMCConfig.SMCConfigFans.GPU));
                    AddOutput("\r\nGameRegion: {0}", config.GetGameRegion(ref cfg));
                    AddOutput("\r\nMACAdress: {0}", config.GetMACAdress(ref cfg));
                    AddOutput("\r\nCPUTemp: {0}", config.GetTempString(ref cfg, SMCConfig.SMCConfigTemps.CPU));
                    AddOutput("\r\nCPUMaxTemp: {0}", config.GetTempString(ref cfg, SMCConfig.SMCConfigTemps.CPUMax));
                    AddOutput("\r\nGPUTemp: {0}", config.GetTempString(ref cfg, SMCConfig.SMCConfigTemps.GPU));
                    AddOutput("\r\nGPUMaxTemp: {0}", config.GetTempString(ref cfg, SMCConfig.SMCConfigTemps.GPUMax));
                    AddOutput("\r\nRAMTemp: {0}", config.GetTempString(ref cfg, SMCConfig.SMCConfigTemps.RAM));
                    AddOutput("\r\nRAMMaxTemp: {0}", config.GetTempString(ref cfg, SMCConfig.SMCConfigTemps.RAMMax));
                    AddOutput("\r\nVideoRegion: {0}", config.GetVideoRegion(ref cfg));
                    AddOutput("\r\nResetCode: {0} ({1})", config.GetResetCode(ref cfg, true), config.GetResetCode(ref cfg));
                }
                catch(X360UtilsException ex) {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
            }
            AddOutput(Environment.NewLine);
            AddDone();
        }

        private void GetsmcbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            using(var reader = new NANDReader(ofd.FileName)) {
                try {
                    AddOutput("Grabbing SMC from NAND: ");
                    var data = _x360NAND.GetSMC(reader, true);
                    var smc = new SMC();
                    var type = smc.GetType(ref data);
                    AddOutput("\r\nSMC Version: {0} [{1}]", smc.GetVersion(ref data), smc.GetMotherBoardFromVersion(ref data));
                    AddOutput("\r\nSMC Type: {0}", type);
                    if(type == SMC.SMCTypes.Jtag || type == SMC.SMCTypes.RJtag)
                        SMC.JTAGSMCPatches.AnalyseSMC(ref data);
                    AddOutput("\r\nSMC Glitch Patched: {0}", smc.CheckGlitchPatch(ref data) ? "Yes" : "No");
                }
                catch(X360UtilsException ex) {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
            }
            AddOutput(Environment.NewLine);
            AddDone();
        }

        private void MetaUtilsClick(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += TestMetaUtils;
            bw.RunWorkerAsync(ofd.FileName);
        }
    }
}
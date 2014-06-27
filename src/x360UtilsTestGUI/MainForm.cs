namespace x360UtilsTestGUI {
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Windows.Forms;
    using x360Utils;
    using x360Utils.Common;
    using x360Utils.CPUKey;
    using x360Utils.NAND;
    using Debug = x360Utils.Debug;

    internal sealed partial class MainForm: Form {
        private readonly X360NAND _x360NAND = new X360NAND();
        private Stopwatch _sw;

        internal MainForm() {
            InitializeComponent();
            dllversionlbl.Text = Main.Version;
            var version = Assembly.GetAssembly(typeof(MainForm)).GetName().Version;
            Debug.DebugOutput += DebugOnDebugOutput;
            Main.InfoOutput += MainOnInfoOutput;
            Main.BlockInReader += MainOnBlockInReader;
            Main.MaxBlocksChanged += MainOnMaxBlocksChanged;
            Text = string.Format(Text, version.Major, version.Minor, version.Build);
            Main.VerbosityLevel = int.MaxValue;
        }

        private void MainOnMaxBlocksChanged(object sender, EventArg<int> eventArg) {
            try {
                if(!InvokeRequired)
                    progressBar1.Maximum = eventArg.Data;
                else
                    Invoke(new MethodInvoker(() => MainOnMaxBlocksChanged(null, eventArg)));
            }
            catch(Exception ex) {
                AddException(ex.ToString());
            }
        }

        private void MainOnBlockInReader(object sender, EventArg<int> eventArg) {
            try {
                if(!InvokeRequired)
                    progressBar1.Value = eventArg.Data;
                else
                    Invoke(new MethodInvoker(() => MainOnBlockInReader(null, eventArg)));
            }
            catch(Exception ex) {
                AddException(ex.ToString());
            }
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
            catch(Exception) {}
        }

        private void AddOutput(string output, params object[] args) {
            try {
                if(!InvokeRequired) {
                    outbox.AppendText(string.Format(output, args));
                    outbox.Select(outbox.Text.Length, 0);
                    outbox.ScrollToCaret();
                }
                else
                    Invoke(new MethodInvoker(() => AddOutput(output, args)));
            }
            catch(Exception ex) {
                AddException(ex.ToString());
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
            catch(Exception) {}
        }

        private void AddDone() {
            _sw.Stop();
            AddOutput("Took: {0} Minutes {1} Seconds {2} Milliseconds\r\n", _sw.Elapsed.Minutes, _sw.Elapsed.Seconds, _sw.Elapsed.Milliseconds);
        }

        private void GetKeyBtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) =>
            {
                try
                {
                    using (var reader = new NANDReader(ofd.FileName))
                    {
                        AddOutput("Grabbing CPUKey from NAND: ");
                        AddOutput(_x360NAND.GetNandCpuKey(reader));
                    }
                }
                catch (X360UtilsException ex)
                {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
                AddOutput(Environment.NewLine);
                AddDone();
            };
            bw.RunWorkerCompleted += BwCompleted;
            bw.RunWorkerAsync();
        }

        private void GetfusebtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) =>
            {
                try
                {
                    using (var reader = new NANDReader(ofd.FileName))
                    {
                        AddOutput("Grabbing FUSES from NAND: ");
                        AddOutput(Environment.NewLine);
                        AddOutput(_x360NAND.GetVirtualFuses(reader));
                    }
                }
                catch (X360UtilsException ex)
                {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
                AddOutput(Environment.NewLine);
                AddDone();
            };
            bw.RunWorkerCompleted += BwCompleted;
            bw.RunWorkerAsync();
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
            try {
                using(var reader = new NANDReader(e.Argument as string)) {
                    AddOutput("Grabbing Launch.ini from NAND: ");
                    AddOutput("{0}{0}{1}", Environment.NewLine, _x360NAND.GetLaunchIni(reader));
                }
            }
            catch(X360UtilsException ex) {
                AddOutput("FAILED!");
                AddException(ex.ToString());
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
            try {
                using(var reader = new NANDReader(e.Argument as string)) {
                    AddOutput("Grabbing BadBlock info from NAND: ");
                    var blocks = reader.FindBadBlocks();
                    foreach(var block in blocks)
                        AddOutput("{1}BadBlock @ 0x{0:X}", block, Environment.NewLine);
                }
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
            AddOutput(Environment.NewLine);
            AddDone();
        }

        private void TestMetaUtils(object sender, DoWorkEventArgs e) {
            try {
                NANDSpare.TestMetaUtils(e.Argument as string);
            }
            catch(X360UtilsException ex) {
                AddOutput("FAILED!");
                AddException(ex.ToString());
            }
        }

        private void GetsmcconfigbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) => {
                             try {
                                 using(var reader = new NANDReader(ofd.FileName)) {
                                     AddOutput("Grabbing SMC_Config from NAND: ");
                                     var cfg = _x360NAND.GetSmcConfig(reader);
                                     var config = new SmcConfig();
                                     AddOutput("\r\nChecksum: {0}", config.GetCheckSum(ref cfg));
                                     AddOutput("\r\nDVDRegion: {0}", config.GetDVDRegion(ref cfg));
                                     AddOutput("\r\nCPUFanSpeed: {0}", config.GetFanSpeed(ref cfg, SmcConfig.SmcConfigFans.Cpu));
                                     AddOutput("\r\nGPUFanSpeed: {0}", config.GetFanSpeed(ref cfg, SmcConfig.SmcConfigFans.Gpu));
                                     AddOutput("\r\nGameRegion: {0}", config.GetGameRegion(ref cfg));
                                     AddOutput("\r\nMACAdress: {0}", config.GetMACAdress(ref cfg));
                                     AddOutput("\r\nCPUTemp: {0}", config.GetTempString(ref cfg, SmcConfig.SmcConfigTemps.Cpu));
                                     AddOutput("\r\nCPUMaxTemp: {0}", config.GetTempString(ref cfg, SmcConfig.SmcConfigTemps.CpuMax));
                                     AddOutput("\r\nGPUTemp: {0}", config.GetTempString(ref cfg, SmcConfig.SmcConfigTemps.Gpu));
                                     AddOutput("\r\nGPUMaxTemp: {0}", config.GetTempString(ref cfg, SmcConfig.SmcConfigTemps.GpuMax));
                                     AddOutput("\r\nRAMTemp: {0}", config.GetTempString(ref cfg, SmcConfig.SmcConfigTemps.Ram));
                                     AddOutput("\r\nRAMMaxTemp: {0}", config.GetTempString(ref cfg, SmcConfig.SmcConfigTemps.RamMax));
                                     AddOutput("\r\nVideoRegion: {0}", config.GetVideoRegion(ref cfg));
                                     AddOutput("\r\nResetCode: {0} ({1})", config.GetResetCode(ref cfg, true), config.GetResetCode(ref cfg));
                                 }
                             }
                             catch(X360UtilsException ex) {
                                 AddOutput("FAILED!");
                                 AddException(ex.ToString());
                             }
                             AddOutput(Environment.NewLine);
                             AddDone();
                         };
            bw.RunWorkerCompleted += BwCompleted;
            bw.RunWorkerAsync();
        }

        private void BwCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs) {
            if (runWorkerCompletedEventArgs.Error != null)
                AddException(runWorkerCompletedEventArgs.Error.ToString());
        }

        private void GetsmcbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += (o, args) =>
            {
                try
                {
                    using (var reader = new NANDReader(ofd.FileName))
                    {
                        AddOutput("Grabbing SMC from NAND: ");
                        var data = _x360NAND.GetSmc(reader, true);
                        var smc = new Smc();
                        var type = smc.GetType(ref data);
                        AddOutput("\r\nSMC Version: {0} [{1}]", smc.GetVersion(ref data), smc.GetMotherBoardFromVersion(ref data));
                        AddOutput("\r\nSMC Type: {0}", type);
                        if (type == Smc.SmcTypes.Jtag || type == Smc.SmcTypes.RJtag)
                            Smc.JtagsmcPatches.AnalyseSmc(ref data);
                        AddOutput("\r\nSMC Glitch Patched: {0}", smc.CheckGlitchPatch(ref data) ? "Yes" : "No");
                    }
                }
                catch (X360UtilsException ex)
                {
                    AddOutput("FAILED!");
                    AddException(ex.ToString());
                }
                AddOutput(Environment.NewLine);
                AddDone();
            };
            bw.RunWorkerCompleted += BwCompleted;
            bw.RunWorkerAsync();
        }

        private void MetaUtilsClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += TestMetaUtils;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void TestFusebtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            PrintFuseInfo(new FUSE(ofd.FileName));
        }

        private void PrintFuseInfo(FUSE info) {
            outbox.Clear();
            outbox.AppendText(string.Format("CPUKey                 : {0}{1}", info.CPUKey, Environment.NewLine));
            outbox.AppendText(string.Format("CF LDV                 : {0}{1}", info.CFLDV, Environment.NewLine));
            outbox.AppendText(string.Format("CB LDV                 : {0}{1}", info.CBLDV, Environment.NewLine));
            if(info.FatRetail)
                outbox.AppendText(string.Format("Dashboard Compatibility: {0}{1}", TranslateCbLdvFat(info.CBLDV), Environment.NewLine));
            else if(info.SlimRetail)
                outbox.AppendText(string.Format("Dashboard Compatibility: {0}{1}", TranslateCbLdvSlim(info.CBLDV), Environment.NewLine));
            outbox.AppendText(string.Format("FUSE Type              : {0}{1}", GetFuseType(info), Environment.NewLine));
            outbox.AppendText(string.Format("Unlocked               : {0}{1}", info.Unlocked ? "Yes" : "No", Environment.NewLine));
            outbox.AppendText(string.Format("Uses Eeprom            : {0}{1}", info.UsesEeprom ? "Yes" : "No", Environment.NewLine));
            if(info.UsesEeprom) {
                outbox.AppendText(string.Format("EepromKey1             : {0:X16}{1}", info.EepromKey1, Environment.NewLine));
                outbox.AppendText(string.Format("EepromKey2             : {0:X16}{1}", info.EepromKey2, Environment.NewLine));
                outbox.AppendText(string.Format("EepromHash1            : {0:X16}{1}", info.EepromHash1, Environment.NewLine));
                outbox.AppendText(string.Format("EepromHash2            : {0:X16}{1}", info.EepromHash2, Environment.NewLine));
            }
            outbox.AppendText(string.Format("Secure                 : {0}{1}", info.Secure ? "Yes" : "No", Environment.NewLine));
            outbox.AppendText(string.Format("Not Valid Flag         : {0}{1}", info.Invalid ? "Yes" : "No", Environment.NewLine));
            outbox.AppendText(string.Format("Reserved OK            : {0}{1}", info.ReservedOk ? "Yes" : "No", Environment.NewLine));
            outbox.AppendText(string.Format("Original fusesets:{0}", Environment.NewLine));
            for(var i = 0; i < info.FUSELines.Length; i++)
                outbox.AppendText(string.Format("fuseset {0:D2}: {1:X16}{2}", i, info.FUSELines[i], Environment.NewLine));
        }

        private static string TranslateCbLdvFat(int cbldv) {
            switch(cbldv) {
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                    return "Dashboards 1888 -> 7371 Are compatible";
                case 6:
                case 7:
                    return "Dashboard 8498 -> 14699 Are compatible";
                    //case 8:
                case 9:
                case 10:
                    return "Dashboards 14717 & 14719 are compatible";
                case 11:
                case 12:
                    return "Dashboard 15572 & later is compatible";
                    //case 13:
                    //case 14:
                    //case 15:
                    //case 16:
                default:
                    return "Unknown";
            }
        }

        private static string TranslateCbLdvSlim(int cbldv) {
            switch(cbldv) {
                case 1:
                case 2:
                    return ("Dashboard 14699 is compatible");
                case 3:
                    return "Dashboards 14717 & 14719 are compatible";
                case 4:
                    return "Dashboard 15572 & later is compatible";
                    //case 5:
                    //case 6:
                    //case 7:
                    //case 8:
                    //case 9:
                    //case 10:
                    //case 11:
                    //case 12:
                    //case 13:
                    //case 14:
                    //case 15:
                    //case 16:
                default:
                    return "Unknown";
            }
        }

        private static string GetFuseType(FUSE info) {
            if(info.FatRetail)
                return "Fat Retail";
            if(info.SlimRetail)
                return "Slim Retail";
            if(info.Devkit)
                return "Devkit";
            if(info.Testkit)
                return "Testkit";
            return "Unknown";
        }

        private void TestFcrTbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var fname = ofd.FileName;
            ofd.FileName = "cpukey.txt";
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var args = new EventArg<string, string>(fname, ofd.FileName);
            var bw = new BackgroundWorker();
            bw.DoWork += TestFcrtDoWork;
            bw.RunWorkerAsync(args);
        }

        private void TestFcrtDoWork(object sender, DoWorkEventArgs e) {
            try {
                var args = e.Argument as EventArg<string, string>;
                if(args != null) {
                    using(var reader = new NANDReader(args.Data1)) {
                        AddOutput("Looking for FCRT.bin in NAND:{0}", Environment.NewLine);
                        var data = _x360NAND.GetFcrt(reader);
                        var keyutils = new CpukeyUtils();
                        var key = keyutils.GetCPUKeyFromTextFile(args.Data2);
                        AddOutput("{0}Decrypting FCRT.bin...{0}", Environment.NewLine);
                        var crypt = new Cryptography();
                        crypt.DecryptFcrt(ref data, StringUtils.HexToArray(key));
                        AddOutput("Verifying FCRT.bin... Result: ");
                        AddOutput(crypt.VerifyFcrtDecrypted(ref data) ? "OK!" : "Failed!");
                    }
                }
            }
            catch(Exception ex) {
                AddOutput("FAILED!");
                AddException(ex.ToString());
            }
            AddOutput(Environment.NewLine);
            AddDone();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            var sfd = new SaveFileDialog();
            if(sfd.ShowDialog() != DialogResult.OK)
                return;
            var rbox = outmenu.SourceControl as RichTextBox;
            if(rbox != null)
                File.WriteAllLines(sfd.FileName, rbox.Lines);
            var tbox = outmenu.SourceControl as TextBox;
            if(tbox != null)
                File.WriteAllLines(sfd.FileName, tbox.Lines);
        }

        private void TestFsRootScanbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += TestFsRootScanDoWork;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void TestFsRootScanDoWork(object sender, DoWorkEventArgs doWorkEventArgs) {
            var reader = new NANDReader(doWorkEventArgs.Argument as string);
            AddOutput("Testing FSRootScanner... {0}", Environment.NewLine);
            try {
                reader.ScanForFsRootAndMobile();
                AddOutput("FSRoot found:{0}", Environment.NewLine);
                AddOutput("{0}{1}", reader.FsRoot, Environment.NewLine);
                AddOutput("Mobiles found:{0}", Environment.NewLine);
                foreach(var mobileEntry in reader.MobileArray)
                    AddOutput("{0}{1}", mobileEntry, Environment.NewLine);
            }
            catch(Exception ex) {
                AddException(ex.ToString());
            }
            finally {
                reader.Close();
            }
            AddDone();
        }

        private void TestFsParserbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += TestFsParserDoWork;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void TestFsParserDoWork(object sender, DoWorkEventArgs doWorkEventArgs) {
            var reader = new NANDReader(doWorkEventArgs.Argument as string);
            try {
                AddOutput("Scanning for RootFS... {0}", Environment.NewLine);
                reader.ScanForFsRootAndMobile();
                AddOutput("Parsing RootFS @ 0x{0:X}...{1}", reader.FsRoot.Offset, Environment.NewLine);
                var fs = new NANDFileSystem();
                var entries = fs.ParseFileSystem(ref reader);
                AddOutput("FSEntries found:{0}", Environment.NewLine);
                foreach(var fileSystemEntry in entries)
                    AddOutput("{0}{1}", fileSystemEntry, Environment.NewLine);
            }
            catch(Exception ex) {
                AddException(ex.ToString());
            }
            finally {
                reader.Close();
            }
            AddDone();
        }

        private void ExtractCurrentFSbtnClick(object sender, EventArgs e) {
            _sw = Stopwatch.StartNew();
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var bw = new BackgroundWorker();
            bw.DoWork += ExtractCurrentFsDoWork;
            bw.RunWorkerAsync(ofd.FileName);
        }

        private void ExtractCurrentFsDoWork(object sender, DoWorkEventArgs doWorkEventArgs) {
            var reader = new NANDReader(doWorkEventArgs.Argument as string);
            try {
                AddOutput("Scanning for RootFS... {0}", Environment.NewLine);
                reader.ScanForFsRootAndMobile();
                AddOutput("Parsing RootFS @ 0x{0:X}...{1}", reader.FsRoot.Offset, Environment.NewLine);
                var fs = new NANDFileSystem();
                var entries = fs.ParseFileSystem(ref reader);
                AddOutput("FSEntries found: {0}{1}", entries.Length, Environment.NewLine);
                var dir = (doWorkEventArgs.Argument as string) + "_ExtractedFS";
                Directory.CreateDirectory(dir);
                foreach(var fileSystemEntry in entries) {
                    AddOutput("Extracting: {0}...{1}", fileSystemEntry.Filename, Environment.NewLine);
                    File.WriteAllBytes(Path.Combine(dir, fileSystemEntry.Filename), fileSystemEntry.GetData(ref reader));
                }
            }
            catch(Exception ex) {
                AddException(ex.ToString());
            }
            finally {
                reader.Close();
            }
            AddDone();
        }

        private void TestSpecialsbtnClick(object sender, EventArgs e) { new Specials().ShowDialog(); }
    }
}
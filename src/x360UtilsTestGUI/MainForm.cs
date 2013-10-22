namespace x360UtilsTestGUI {
    using System;
    using System.Windows.Forms;
    using x360Utils;
    using x360Utils.NAND;

    internal sealed partial class MainForm : Form {
        X360NAND x360NAND = new X360NAND();

        internal MainForm() {
            InitializeComponent();
            dllversionlbl.Text = Main.Version;
            Debug.DebugOutput += DebugOnDebugOutput;
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

        private void AddException(string exception) {
            debugbox.AppendText(string.Format("[EXCEPTION]{1}{0}{1}", exception, Environment.NewLine));
        }

        private void GetKeyBtnClick(object sender, EventArgs e) {
            var ofd = new OpenFileDialog();
            if(ofd.ShowDialog() != DialogResult.OK)
                return;
            var reader = new NANDReader(ofd.FileName);
            outbox.AppendText("Grabbing CPUKey from NAND: ");
            try {
                outbox.AppendText(x360NAND.GetNANDCPUKey(ref reader));
            }
            catch (X360UtilsException ex) {
                AddException(ex.ToString());
            }
            outbox.AppendText(Environment.NewLine);
        }
    }
}
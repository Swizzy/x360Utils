using System.Windows.Forms;

namespace SMCVersionCheck
{
    using System;
    using System.IO;
    using x360Utils.NAND;

    public partial class Form1 : Form {
        private X360NAND _nand = new X360NAND();
        private Smc _smc = new Smc();
        private static NANDReader reader;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e) {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            try {
                var file = ((string[])e.Data.GetData(DataFormats.FileDrop, false))[0];
                if (reader != null)
                    reader.Close();
                reader = new NANDReader(file);
                textBox1.Text = file;
                var smc = _nand.GetSmc(reader, true);
                textBox2.Text = _smc.GetVersion(ref smc);
            }
            catch { }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog();
            if (sfd.ShowDialog() == DialogResult.OK)
                File.WriteAllBytes(sfd.FileName, _nand.GetSmc(reader, true));
        }
    }
}

namespace x360UtilsTestGUI
{
    internal sealed partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.dllversionlbl = new System.Windows.Forms.ToolStripStatusLabel();
            this.getkeybtn = new System.Windows.Forms.Button();
            this.getfusebtn = new System.Windows.Forms.Button();
            this.getlaunchinibtn = new System.Windows.Forms.Button();
            this.getbadblocksbtn = new System.Windows.Forms.Button();
            this.getsmcconfigbtn = new System.Windows.Forms.Button();
            this.getsmcbtn = new System.Windows.Forms.Button();
            this.outtab = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.outbox = new System.Windows.Forms.RichTextBox();
            this.outmenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clearToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.debugbox = new System.Windows.Forms.RichTextBox();
            this.MetaUtils = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.outtab.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.outmenu.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.dllversionlbl});
            this.statusStrip1.Location = new System.Drawing.Point(0, 338);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1112, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // dllversionlbl
            // 
            this.dllversionlbl.Name = "dllversionlbl";
            this.dllversionlbl.Size = new System.Drawing.Size(69, 17);
            this.dllversionlbl.Text = "DLL Version";
            // 
            // getkeybtn
            // 
            this.getkeybtn.Location = new System.Drawing.Point(726, 12);
            this.getkeybtn.Name = "getkeybtn";
            this.getkeybtn.Size = new System.Drawing.Size(184, 23);
            this.getkeybtn.TabIndex = 3;
            this.getkeybtn.Text = "Get CPUKey From NAND";
            this.getkeybtn.UseVisualStyleBackColor = true;
            this.getkeybtn.Click += new System.EventHandler(this.GetKeyBtnClick);
            // 
            // getfusebtn
            // 
            this.getfusebtn.Location = new System.Drawing.Point(916, 12);
            this.getfusebtn.Name = "getfusebtn";
            this.getfusebtn.Size = new System.Drawing.Size(184, 23);
            this.getfusebtn.TabIndex = 3;
            this.getfusebtn.Text = "Get Fuses From NAND";
            this.getfusebtn.UseVisualStyleBackColor = true;
            this.getfusebtn.Click += new System.EventHandler(this.GetfusebtnClick);
            // 
            // getlaunchinibtn
            // 
            this.getlaunchinibtn.Location = new System.Drawing.Point(726, 41);
            this.getlaunchinibtn.Name = "getlaunchinibtn";
            this.getlaunchinibtn.Size = new System.Drawing.Size(184, 23);
            this.getlaunchinibtn.TabIndex = 3;
            this.getlaunchinibtn.Text = "Get Launch.ini from NAND";
            this.getlaunchinibtn.UseVisualStyleBackColor = true;
            this.getlaunchinibtn.Click += new System.EventHandler(this.GetlaunchinibtnClick);
            // 
            // getbadblocksbtn
            // 
            this.getbadblocksbtn.Location = new System.Drawing.Point(916, 41);
            this.getbadblocksbtn.Name = "getbadblocksbtn";
            this.getbadblocksbtn.Size = new System.Drawing.Size(184, 23);
            this.getbadblocksbtn.TabIndex = 3;
            this.getbadblocksbtn.Text = "Get BadBlocks";
            this.getbadblocksbtn.UseVisualStyleBackColor = true;
            this.getbadblocksbtn.Click += new System.EventHandler(this.GetbadblocksbtnClick);
            // 
            // getsmcconfigbtn
            // 
            this.getsmcconfigbtn.Location = new System.Drawing.Point(727, 71);
            this.getsmcconfigbtn.Name = "getsmcconfigbtn";
            this.getsmcconfigbtn.Size = new System.Drawing.Size(183, 23);
            this.getsmcconfigbtn.TabIndex = 4;
            this.getsmcconfigbtn.Text = "Get SMC_Config from NAND";
            this.getsmcconfigbtn.UseVisualStyleBackColor = true;
            this.getsmcconfigbtn.Click += new System.EventHandler(this.GetsmcconfigbtnClick);
            // 
            // getsmcbtn
            // 
            this.getsmcbtn.Location = new System.Drawing.Point(916, 70);
            this.getsmcbtn.Name = "getsmcbtn";
            this.getsmcbtn.Size = new System.Drawing.Size(183, 23);
            this.getsmcbtn.TabIndex = 4;
            this.getsmcbtn.Text = "Get SMC From NAND";
            this.getsmcbtn.UseVisualStyleBackColor = true;
            this.getsmcbtn.Click += new System.EventHandler(this.GetsmcbtnClick);
            // 
            // outtab
            // 
            this.outtab.Controls.Add(this.tabPage1);
            this.outtab.Controls.Add(this.tabPage2);
            this.outtab.Location = new System.Drawing.Point(0, 0);
            this.outtab.Name = "outtab";
            this.outtab.SelectedIndex = 0;
            this.outtab.Size = new System.Drawing.Size(721, 335);
            this.outtab.TabIndex = 5;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.outbox);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(713, 309);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Output";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // outbox
            // 
            this.outbox.BackColor = System.Drawing.Color.Black;
            this.outbox.ContextMenuStrip = this.outmenu;
            this.outbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outbox.ForeColor = System.Drawing.Color.Green;
            this.outbox.Location = new System.Drawing.Point(3, 3);
            this.outbox.Name = "outbox";
            this.outbox.ReadOnly = true;
            this.outbox.Size = new System.Drawing.Size(707, 303);
            this.outbox.TabIndex = 1;
            this.outbox.Text = "";
            // 
            // outmenu
            // 
            this.outmenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clearToolStripMenuItem});
            this.outmenu.Name = "outmenu";
            this.outmenu.Size = new System.Drawing.Size(102, 26);
            // 
            // clearToolStripMenuItem
            // 
            this.clearToolStripMenuItem.Name = "clearToolStripMenuItem";
            this.clearToolStripMenuItem.Size = new System.Drawing.Size(101, 22);
            this.clearToolStripMenuItem.Text = "Clear";
            this.clearToolStripMenuItem.Click += new System.EventHandler(this.ClearToolStripMenuItemClick);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.debugbox);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(713, 309);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "DEBUG";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // debugbox
            // 
            this.debugbox.BackColor = System.Drawing.Color.Black;
            this.debugbox.ContextMenuStrip = this.outmenu;
            this.debugbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.debugbox.ForeColor = System.Drawing.Color.Green;
            this.debugbox.Location = new System.Drawing.Point(3, 3);
            this.debugbox.Name = "debugbox";
            this.debugbox.ReadOnly = true;
            this.debugbox.Size = new System.Drawing.Size(707, 303);
            this.debugbox.TabIndex = 2;
            this.debugbox.Text = "";
            // 
            // MetaUtils
            // 
            this.MetaUtils.Location = new System.Drawing.Point(727, 100);
            this.MetaUtils.Name = "MetaUtils";
            this.MetaUtils.Size = new System.Drawing.Size(183, 23);
            this.MetaUtils.TabIndex = 3;
            this.MetaUtils.Text = "TestMetaUtils";
            this.MetaUtils.UseVisualStyleBackColor = true;
            this.MetaUtils.Click += new System.EventHandler(this.MetaUtilsClick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1112, 360);
            this.Controls.Add(this.outtab);
            this.Controls.Add(this.getsmcbtn);
            this.Controls.Add(this.getsmcconfigbtn);
            this.Controls.Add(this.getfusebtn);
            this.Controls.Add(this.MetaUtils);
            this.Controls.Add(this.getbadblocksbtn);
            this.Controls.Add(this.getlaunchinibtn);
            this.Controls.Add(this.getkeybtn);
            this.Controls.Add(this.statusStrip1);
            this.Name = "MainForm";
            this.Text = "x360UtilsTest GUI v{0}.{1} (Build: {2})";
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.outtab.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.outmenu.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel dllversionlbl;
        private System.Windows.Forms.Button getkeybtn;
        private System.Windows.Forms.Button getfusebtn;
        private System.Windows.Forms.Button getlaunchinibtn;
        private System.Windows.Forms.Button getbadblocksbtn;
        private System.Windows.Forms.Button getsmcconfigbtn;
        private System.Windows.Forms.Button getsmcbtn;
        private System.Windows.Forms.TabControl outtab;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.RichTextBox outbox;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.RichTextBox debugbox;
        private System.Windows.Forms.ContextMenuStrip outmenu;
        private System.Windows.Forms.ToolStripMenuItem clearToolStripMenuItem;
        private System.Windows.Forms.Button MetaUtils;

    }
}


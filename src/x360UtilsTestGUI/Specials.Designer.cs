namespace x360UtilsTestGUI
{
    partial class Specials
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
            this.jfbtn = new System.Windows.Forms.Button();
            this.xkbtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // jfbtn
            // 
            this.jfbtn.Location = new System.Drawing.Point(12, 12);
            this.jfbtn.Name = "jfbtn";
            this.jfbtn.Size = new System.Drawing.Size(171, 23);
            this.jfbtn.TabIndex = 0;
            this.jfbtn.Text = "Extract JF Files";
            this.jfbtn.UseVisualStyleBackColor = true;
            this.jfbtn.Click += new System.EventHandler(this.JfbtnClick);
            // 
            // xkbtn
            // 
            this.xkbtn.Location = new System.Drawing.Point(12, 41);
            this.xkbtn.Name = "xkbtn";
            this.xkbtn.Size = new System.Drawing.Size(171, 23);
            this.xkbtn.TabIndex = 0;
            this.xkbtn.Text = "Extract Xk3y Files";
            this.xkbtn.UseVisualStyleBackColor = true;
            this.xkbtn.Click += new System.EventHandler(this.xkbtn_Click);
            // 
            // Specials
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(195, 76);
            this.Controls.Add(this.xkbtn);
            this.Controls.Add(this.jfbtn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Specials";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Specials";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button jfbtn;
        private System.Windows.Forms.Button xkbtn;
    }
}
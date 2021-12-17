using System.Net;
using System.IO;

namespace Viking.UI.Forms
{
    partial class LogonASPMembership
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LogonASPMembership));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.marclabLink = new System.Windows.Forms.LinkLabel();
            this.annotationsLink = new System.Windows.Forms.LinkLabel();
            this.linkDocumentation = new System.Windows.Forms.LinkLabel();
            this.labelVolume = new System.Windows.Forms.Label();
            this.comboVolumeURL = new System.Windows.Forms.ComboBox();
            this.linkVersionHistory = new System.Windows.Forms.LinkLabel();
            this.btnAnonymous = new System.Windows.Forms.Button();
            this.groupCredentials = new System.Windows.Forms.GroupBox();
            this.update_label = new System.Windows.Forms.Label();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.linkRegister = new System.Windows.Forms.LinkLabel();
            this.btnLogin = new System.Windows.Forms.Button();
            this.remember_me_check_box = new System.Windows.Forms.CheckBox();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textUsername = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnFindOCPVolume = new System.Windows.Forms.Button();
            this.SubmitURLChangedTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupCredentials.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::Viking.Properties.Resources.marclab;
            this.pictureBox1.Location = new System.Drawing.Point(22, 51);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(578, 374);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // marclabLink
            // 
            this.marclabLink.AutoSize = true;
            this.marclabLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.marclabLink.Location = new System.Drawing.Point(18, 434);
            this.marclabLink.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.marclabLink.Name = "marclabLink";
            this.marclabLink.Size = new System.Drawing.Size(112, 29);
            this.marclabLink.TabIndex = 3;
            this.marclabLink.TabStop = true;
            this.marclabLink.Text = "Marc Lab";
            this.marclabLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.marclabLink_LinkClicked);
            // 
            // annotationsLink
            // 
            this.annotationsLink.AutoSize = true;
            this.annotationsLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.annotationsLink.Location = new System.Drawing.Point(400, 434);
            this.annotationsLink.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.annotationsLink.Name = "annotationsLink";
            this.annotationsLink.Size = new System.Drawing.Size(215, 29);
            this.annotationsLink.TabIndex = 4;
            this.annotationsLink.TabStop = true;
            this.annotationsLink.Text = "Web Visualizations";
            this.annotationsLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.annotationsLink_LinkClicked);
            // 
            // linkDocumentation
            // 
            this.linkDocumentation.AutoSize = true;
            this.linkDocumentation.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkDocumentation.Location = new System.Drawing.Point(194, 434);
            this.linkDocumentation.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.linkDocumentation.Name = "linkDocumentation";
            this.linkDocumentation.Size = new System.Drawing.Size(174, 29);
            this.linkDocumentation.TabIndex = 5;
            this.linkDocumentation.TabStop = true;
            this.linkDocumentation.Text = "Documentation";
            this.linkDocumentation.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.vikingLink_LinkClicked);
            // 
            // labelVolume
            // 
            this.labelVolume.AutoSize = true;
            this.labelVolume.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelVolume.Location = new System.Drawing.Point(18, 14);
            this.labelVolume.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelVolume.Name = "labelVolume";
            this.labelVolume.Size = new System.Drawing.Size(127, 22);
            this.labelVolume.TabIndex = 6;
            this.labelVolume.Text = "Volume URL:";
            // 
            // comboVolumeURL
            // 
            this.comboVolumeURL.DisplayMember = "VolumeURL";
            this.comboVolumeURL.FormattingEnabled = true;
            this.comboVolumeURL.Location = new System.Drawing.Point(154, 9);
            this.comboVolumeURL.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.comboVolumeURL.Name = "comboVolumeURL";
            this.comboVolumeURL.Size = new System.Drawing.Size(716, 28);
            this.comboVolumeURL.TabIndex = 7;
            this.comboVolumeURL.SelectedIndexChanged += new System.EventHandler(this.comboVolumeURL_SelectedIndexChanged);
            this.comboVolumeURL.TextUpdate += new System.EventHandler(this.comboVolumeURL_TextUpdate);
            this.comboVolumeURL.Validating += new System.ComponentModel.CancelEventHandler(this.comboVolumeURL_Validating);
            // 
            // linkVersionHistory
            // 
            this.linkVersionHistory.AutoSize = true;
            this.linkVersionHistory.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkVersionHistory.Location = new System.Drawing.Point(194, 485);
            this.linkVersionHistory.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.linkVersionHistory.Name = "linkVersionHistory";
            this.linkVersionHistory.Size = new System.Drawing.Size(175, 29);
            this.linkVersionHistory.TabIndex = 8;
            this.linkVersionHistory.TabStop = true;
            this.linkVersionHistory.Text = "Version History";
            this.linkVersionHistory.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkVersionHistory_LinkClicked);
            // 
            // btnAnonymous
            // 
            this.btnAnonymous.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAnonymous.Location = new System.Drawing.Point(42, 29);
            this.btnAnonymous.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnAnonymous.Name = "btnAnonymous";
            this.btnAnonymous.Size = new System.Drawing.Size(356, 62);
            this.btnAnonymous.TabIndex = 0;
            this.btnAnonymous.Text = "Read only access";
            this.btnAnonymous.UseVisualStyleBackColor = true;
            this.btnAnonymous.Click += new System.EventHandler(this.Handle_Anonymmous);
            // 
            // groupCredentials
            // 
            this.groupCredentials.Controls.Add(this.update_label);
            this.groupCredentials.Controls.Add(this.linkLabel2);
            this.groupCredentials.Controls.Add(this.linkRegister);
            this.groupCredentials.Controls.Add(this.btnLogin);
            this.groupCredentials.Controls.Add(this.remember_me_check_box);
            this.groupCredentials.Controls.Add(this.textPassword);
            this.groupCredentials.Controls.Add(this.label3);
            this.groupCredentials.Controls.Add(this.label2);
            this.groupCredentials.Controls.Add(this.textUsername);
            this.groupCredentials.Location = new System.Drawing.Point(644, 162);
            this.groupCredentials.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupCredentials.Name = "groupCredentials";
            this.groupCredentials.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupCredentials.Size = new System.Drawing.Size(440, 351);
            this.groupCredentials.TabIndex = 2;
            this.groupCredentials.TabStop = false;
            this.groupCredentials.Text = "Annotating";
            // 
            // update_label
            // 
            this.update_label.AutoSize = true;
            this.update_label.Location = new System.Drawing.Point(38, 297);
            this.update_label.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.update_label.Name = "update_label";
            this.update_label.Size = new System.Drawing.Size(70, 20);
            this.update_label.TabIndex = 10;
            this.update_label.Text = "Updates";
            this.update_label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel2.Location = new System.Drawing.Point(234, 42);
            this.linkLabel2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(161, 22);
            this.linkLabel2.TabIndex = 9;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "Retrieve Password";
            // 
            // linkRegister
            // 
            this.linkRegister.AutoSize = true;
            this.linkRegister.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkRegister.Location = new System.Drawing.Point(38, 42);
            this.linkRegister.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.linkRegister.Name = "linkRegister";
            this.linkRegister.Size = new System.Drawing.Size(183, 22);
            this.linkRegister.TabIndex = 7;
            this.linkRegister.TabStop = true;
            this.linkRegister.Text = "Register new account";
            this.linkRegister.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            this.linkRegister.Click += new System.EventHandler(this.linkLabel1_Click);
            // 
            // btnLogin
            // 
            this.btnLogin.Enabled = false;
            this.btnLogin.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnLogin.Location = new System.Drawing.Point(42, 229);
            this.btnLogin.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(356, 48);
            this.btnLogin.TabIndex = 5;
            this.btnLogin.Text = "Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.login_handle);
            // 
            // remember_me_check_box
            // 
            this.remember_me_check_box.AutoSize = true;
            this.remember_me_check_box.Checked = true;
            this.remember_me_check_box.CheckState = System.Windows.Forms.CheckState.Checked;
            this.remember_me_check_box.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.remember_me_check_box.Location = new System.Drawing.Point(142, 191);
            this.remember_me_check_box.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.remember_me_check_box.Name = "remember_me_check_box";
            this.remember_me_check_box.Size = new System.Drawing.Size(152, 26);
            this.remember_me_check_box.TabIndex = 4;
            this.remember_me_check_box.Text = "Remember me";
            this.remember_me_check_box.UseVisualStyleBackColor = true;
            // 
            // textPassword
            // 
            this.textPassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textPassword.Location = new System.Drawing.Point(142, 138);
            this.textPassword.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textPassword.Name = "textPassword";
            this.textPassword.PasswordChar = '*';
            this.textPassword.Size = new System.Drawing.Size(253, 28);
            this.textPassword.TabIndex = 3;
            this.textPassword.GotFocus += new System.EventHandler(this.password_GotFocus);
            this.textPassword.KeyUp += new System.Windows.Forms.KeyEventHandler(this.password_KeyUp);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(38, 143);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(94, 22);
            this.label3.TabIndex = 2;
            this.label3.Text = "Password:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(38, 89);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(97, 22);
            this.label2.TabIndex = 1;
            this.label2.Text = "Username:";
            // 
            // textUsername
            // 
            this.textUsername.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textUsername.Location = new System.Drawing.Point(142, 85);
            this.textUsername.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.textUsername.Name = "textUsername";
            this.textUsername.Size = new System.Drawing.Size(253, 28);
            this.textUsername.TabIndex = 0;
            this.textUsername.GotFocus += new System.EventHandler(this.username_GotFocus);
            this.textUsername.KeyUp += new System.Windows.Forms.KeyEventHandler(this.username_KeyUp);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btnAnonymous);
            this.groupBox1.Location = new System.Drawing.Point(644, 51);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBox1.Size = new System.Drawing.Size(438, 117);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Viewing";
            // 
            // btnFindOCPVolume
            // 
            this.btnFindOCPVolume.Location = new System.Drawing.Point(882, 9);
            this.btnFindOCPVolume.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.btnFindOCPVolume.Name = "btnFindOCPVolume";
            this.btnFindOCPVolume.Size = new System.Drawing.Size(201, 35);
            this.btnFindOCPVolume.TabIndex = 10;
            this.btnFindOCPVolume.Text = "Find Volumes";
            this.btnFindOCPVolume.UseVisualStyleBackColor = true;
            this.btnFindOCPVolume.Click += new System.EventHandler(this.btnFindOCPVolume_Click);
            // 
            // SubmitURLChangedTimer
            // 
            this.SubmitURLChangedTimer.Interval = 1000;
            this.SubmitURLChangedTimer.Tick += new System.EventHandler(this.SubmitURLChangedTimer_Tick);
            // 
            // LogonASPMembership
            // 
            this.AcceptButton = this.btnAnonymous;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.ClientSize = new System.Drawing.Size(1101, 515);
            this.Controls.Add(this.btnFindOCPVolume);
            this.Controls.Add(this.groupCredentials);
            this.Controls.Add(this.linkVersionHistory);
            this.Controls.Add(this.comboVolumeURL);
            this.Controls.Add(this.labelVolume);
            this.Controls.Add(this.linkDocumentation);
            this.Controls.Add(this.annotationsLink);
            this.Controls.Add(this.marclabLink);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.groupBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.MaximizeBox = false;
            this.Name = "LogonASPMembership";
            this.Opacity = 0.95D;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Viking Login";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Logon_FormClosed);
            this.Load += new System.EventHandler(this.Logon_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Logon_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupCredentials.ResumeLayout(false);
            this.groupCredentials.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

     

     

        void password_GotFocus(object sender, System.EventArgs e)
        {
            this.textPassword.SelectAll();
        }

        void username_GotFocus(object sender, System.EventArgs e)
        {
            this.textUsername.SelectAll();
        }
          
        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.LinkLabel marclabLink;
        private System.Windows.Forms.LinkLabel annotationsLink;
        private System.Windows.Forms.LinkLabel linkDocumentation;
        private System.Windows.Forms.Label labelVolume;
        private System.Windows.Forms.ComboBox comboVolumeURL;
        private System.Windows.Forms.LinkLabel linkVersionHistory;
        private System.Windows.Forms.Button btnAnonymous;
        private System.Windows.Forms.GroupBox groupCredentials;
        private System.Windows.Forms.Label update_label;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.LinkLabel linkRegister;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.CheckBox remember_me_check_box;
        private System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnFindOCPVolume;
        private System.Windows.Forms.Timer SubmitURLChangedTimer;
    }
}
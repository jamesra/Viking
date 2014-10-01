using System.Net;
using System.IO;

namespace Viking.UI.Forms
{
    partial class LogonForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LogonForm));
            this.groupUserInfo = new System.Windows.Forms.GroupBox();
            this.update_label = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.groupCredentials = new System.Windows.Forms.GroupBox();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.label5 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label4 = new System.Windows.Forms.Label();
            this.btnLogin = new System.Windows.Forms.Button();
            this.remember_me_check_box = new System.Windows.Forms.CheckBox();
            this.textPassword = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textUsername = new System.Windows.Forms.TextBox();
            this.btnAnonymous = new System.Windows.Forms.Button();
            this.pictureMarcLab = new System.Windows.Forms.PictureBox();
            this.marclabLink = new System.Windows.Forms.LinkLabel();
            this.annotationsLink = new System.Windows.Forms.LinkLabel();
            this.vikingLink = new System.Windows.Forms.LinkLabel();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.button1 = new System.Windows.Forms.Button();
            this.groupUserInfo.SuspendLayout();
            this.groupCredentials.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureMarcLab)).BeginInit();
            this.SuspendLayout();
            // 
            // groupUserInfo
            // 
            this.groupUserInfo.Controls.Add(this.update_label);
            this.groupUserInfo.Controls.Add(this.label8);
            this.groupUserInfo.Controls.Add(this.groupCredentials);
            this.groupUserInfo.Controls.Add(this.btnAnonymous);
            this.groupUserInfo.Location = new System.Drawing.Point(403, 38);
            this.groupUserInfo.Name = "groupUserInfo";
            this.groupUserInfo.Size = new System.Drawing.Size(385, 336);
            this.groupUserInfo.TabIndex = 0;
            this.groupUserInfo.TabStop = false;
            this.groupUserInfo.Text = "User Info";
            this.groupUserInfo.Enter += new System.EventHandler(this.groupBox1_Enter);
            // 
            // update_label
            // 
            this.update_label.AutoSize = true;
            this.update_label.Location = new System.Drawing.Point(120, 323);
            this.update_label.Name = "update_label";
            this.update_label.Size = new System.Drawing.Size(0, 13);
            this.update_label.TabIndex = 10;
            this.update_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(56, 81);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(287, 15);
            this.label8.TabIndex = 3;
            this.label8.Text = "Info: Click \"Anonymous Login\" (or) Enter Credentials";
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // groupCredentials
            // 
            this.groupCredentials.Controls.Add(this.linkLabel2);
            this.groupCredentials.Controls.Add(this.label5);
            this.groupCredentials.Controls.Add(this.linkLabel1);
            this.groupCredentials.Controls.Add(this.label4);
            this.groupCredentials.Controls.Add(this.btnLogin);
            this.groupCredentials.Controls.Add(this.remember_me_check_box);
            this.groupCredentials.Controls.Add(this.textPassword);
            this.groupCredentials.Controls.Add(this.label3);
            this.groupCredentials.Controls.Add(this.label2);
            this.groupCredentials.Controls.Add(this.textUsername);
            this.groupCredentials.Location = new System.Drawing.Point(18, 109);
            this.groupCredentials.Name = "groupCredentials";
            this.groupCredentials.Size = new System.Drawing.Size(351, 201);
            this.groupCredentials.TabIndex = 2;
            this.groupCredentials.TabStop = false;
            this.groupCredentials.Text = "Credentials";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel2.Location = new System.Drawing.Point(275, 135);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(52, 15);
            this.linkLabel2.TabIndex = 9;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "Retrieve";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(174, 135);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(106, 15);
            this.label5.TabIndex = 8;
            this.label5.Text = "Forgot Password?";
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel1.Location = new System.Drawing.Point(187, 26);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(53, 15);
            this.linkLabel1.TabIndex = 7;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "Register";
            this.linkLabel1.Click += new System.EventHandler(this.linkLabel1_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(102, 26);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(88, 15);
            this.label4.TabIndex = 6;
            this.label4.Text = "New to Viking?";
            // 
            // btnLogin
            // 
            this.btnLogin.Enabled = false;
            this.btnLogin.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnLogin.Location = new System.Drawing.Point(135, 159);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(88, 31);
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
            this.remember_me_check_box.Location = new System.Drawing.Point(58, 134);
            this.remember_me_check_box.Name = "remember_me_check_box";
            this.remember_me_check_box.Size = new System.Drawing.Size(110, 19);
            this.remember_me_check_box.TabIndex = 4;
            this.remember_me_check_box.Text = "Remember me";
            this.remember_me_check_box.UseVisualStyleBackColor = true;
            // 
            // textPassword
            // 
            this.textPassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textPassword.Location = new System.Drawing.Point(135, 95);
            this.textPassword.Name = "textPassword";
            this.textPassword.PasswordChar = '*';
            this.textPassword.Size = new System.Drawing.Size(121, 21);
            this.textPassword.TabIndex = 3;
            this.textPassword.GotFocus += new System.EventHandler(this.password_GotFocus);
            this.textPassword.KeyUp += new System.Windows.Forms.KeyEventHandler(this.password_KeyUp);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(65, 98);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(64, 15);
            this.label3.TabIndex = 2;
            this.label3.Text = "Password:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(61, 63);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(68, 15);
            this.label2.TabIndex = 1;
            this.label2.Text = "Username:";
            // 
            // textUsername
            // 
            this.textUsername.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textUsername.Location = new System.Drawing.Point(135, 60);
            this.textUsername.Name = "textUsername";
            this.textUsername.Size = new System.Drawing.Size(121, 21);
            this.textUsername.TabIndex = 0;
            this.textUsername.GotFocus += new System.EventHandler(this.username_GotFocus);
            this.textUsername.KeyUp += new System.Windows.Forms.KeyEventHandler(this.username_KeyUp);
            // 
            // btnAnonymous
            // 
            this.btnAnonymous.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAnonymous.Location = new System.Drawing.Point(103, 28);
            this.btnAnonymous.Name = "btnAnonymous";
            this.btnAnonymous.Size = new System.Drawing.Size(183, 40);
            this.btnAnonymous.TabIndex = 0;
            this.btnAnonymous.Text = "Anonymous Login";
            this.btnAnonymous.UseVisualStyleBackColor = true;
            this.btnAnonymous.Click += new System.EventHandler(this.Handle_Anonymmous);
            // 
            // pictureMarcLab
            // 
            this.pictureMarcLab.Image = global::Viking.Properties.Resources.marclab;
            this.pictureMarcLab.Location = new System.Drawing.Point(12, 38);
            this.pictureMarcLab.Name = "pictureMarcLab";
            this.pictureMarcLab.Size = new System.Drawing.Size(385, 269);
            this.pictureMarcLab.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureMarcLab.TabIndex = 1;
            this.pictureMarcLab.TabStop = false;
            this.pictureMarcLab.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // marclabLink
            // 
            this.marclabLink.AutoSize = true;
            this.marclabLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.marclabLink.Location = new System.Drawing.Point(23, 324);
            this.marclabLink.Name = "marclabLink";
            this.marclabLink.Size = new System.Drawing.Size(70, 18);
            this.marclabLink.TabIndex = 3;
            this.marclabLink.TabStop = true;
            this.marclabLink.Text = "Marc Lab";
            this.marclabLink.Click += new System.EventHandler(this.linkLabel3_Click);
            // 
            // annotationsLink
            // 
            this.annotationsLink.AutoSize = true;
            this.annotationsLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.annotationsLink.Location = new System.Drawing.Point(247, 324);
            this.annotationsLink.Name = "annotationsLink";
            this.annotationsLink.Size = new System.Drawing.Size(121, 18);
            this.annotationsLink.TabIndex = 4;
            this.annotationsLink.TabStop = true;
            this.annotationsLink.Text = "Web Annotations";
            this.annotationsLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.annotationsLink_LinkClicked);
            // 
            // vikingLink
            // 
            this.vikingLink.AutoSize = true;
            this.vikingLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.vikingLink.Location = new System.Drawing.Point(117, 324);
            this.vikingLink.Name = "vikingLink";
            this.vikingLink.Size = new System.Drawing.Size(114, 18);
            this.vikingLink.TabIndex = 5;
            this.vikingLink.TabStop = true;
            this.vikingLink.Text = "Viking Webpage";
            this.vikingLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.vikingLink_LinkClicked);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(76, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Volume http://";
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(94, 10);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(607, 21);
            this.comboBox1.TabIndex = 7;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(707, 9);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(81, 23);
            this.button1.TabIndex = 8;
            this.button1.Text = "Load";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // LogonForm
            // 
            this.AcceptButton = this.btnAnonymous;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.ClientSize = new System.Drawing.Size(800, 382);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.vikingLink);
            this.Controls.Add(this.annotationsLink);
            this.Controls.Add(this.marclabLink);
            this.Controls.Add(this.pictureMarcLab);
            this.Controls.Add(this.groupUserInfo);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "LogonForm";
            this.Opacity = 0.95D;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Viking Login";
            this.Load += new System.EventHandler(this.Logon_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Logon_KeyDown);
            this.groupUserInfo.ResumeLayout(false);
            this.groupUserInfo.PerformLayout();
            this.groupCredentials.ResumeLayout(false);
            this.groupCredentials.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureMarcLab)).EndInit();
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

        private System.Windows.Forms.GroupBox groupUserInfo;
        private System.Windows.Forms.GroupBox groupCredentials;
        private System.Windows.Forms.Button btnAnonymous;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.CheckBox remember_me_check_box;
        private System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.PictureBox pictureMarcLab;
        private System.Windows.Forms.LinkLabel marclabLink;
        private System.Windows.Forms.LinkLabel annotationsLink;
        private System.Windows.Forms.Label update_label;
        private System.Windows.Forms.LinkLabel vikingLink;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.Button button1;

        
        
    }
}
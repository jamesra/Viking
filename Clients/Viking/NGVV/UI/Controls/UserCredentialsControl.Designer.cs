namespace Viking.UI.Controls
{
    partial class UserCredentialsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
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
            this.update_label = new System.Windows.Forms.Label();
            this.groupCredentials.SuspendLayout();
            this.SuspendLayout();
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(53, 68);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(287, 15);
            this.label8.TabIndex = 13;
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
            this.groupCredentials.Location = new System.Drawing.Point(15, 96);
            this.groupCredentials.Name = "groupCredentials";
            this.groupCredentials.Size = new System.Drawing.Size(351, 201);
            this.groupCredentials.TabIndex = 12;
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
            // 
            // btnAnonymous
            // 
            this.btnAnonymous.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAnonymous.Location = new System.Drawing.Point(100, 15);
            this.btnAnonymous.Name = "btnAnonymous";
            this.btnAnonymous.Size = new System.Drawing.Size(183, 40);
            this.btnAnonymous.TabIndex = 11;
            this.btnAnonymous.Text = "Anonymous Login";
            this.btnAnonymous.UseVisualStyleBackColor = true;
            // 
            // update_label
            // 
            this.update_label.AutoSize = true;
            this.update_label.Location = new System.Drawing.Point(12, 311);
            this.update_label.Name = "update_label";
            this.update_label.Size = new System.Drawing.Size(0, 13);
            this.update_label.TabIndex = 14;
            // 
            // UserCredentialsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.update_label);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.groupCredentials);
            this.Controls.Add(this.btnAnonymous);
            this.Name = "UserCredentialsControl";
            this.Size = new System.Drawing.Size(385, 339);
            this.groupCredentials.ResumeLayout(false);
            this.groupCredentials.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.GroupBox groupCredentials;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.CheckBox remember_me_check_box;
        private System.Windows.Forms.TextBox textPassword;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textUsername;
        private System.Windows.Forms.Button btnAnonymous;
        private System.Windows.Forms.Label update_label;
    }
}

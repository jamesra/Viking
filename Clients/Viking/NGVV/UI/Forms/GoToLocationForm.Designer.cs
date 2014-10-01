namespace Viking.UI.Forms
{
    partial class GoToLocationForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GoToLocationForm));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.f = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.textDownsample = new Viking.UI.Controls.NumericTextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textZ = new Viking.UI.Controls.NumericTextBox();
            this.textY = new Viking.UI.Controls.NumericTextBox();
            this.textX = new Viking.UI.Controls.NumericTextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(17, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "X:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(10, 35);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(17, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Y:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 57);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(17, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Z:";
            // 
            // f
            // 
            this.f.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.f.Location = new System.Drawing.Point(176, 15);
            this.f.Name = "f";
            this.f.Size = new System.Drawing.Size(75, 23);
            this.f.TabIndex = 6;
            this.f.Text = "Go!";
            this.f.UseVisualStyleBackColor = true;
            this.f.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoToLocationForm_KeyDown);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(176, 69);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoToLocationForm_KeyDown);
            // 
            // textDownsample
            // 
            this.textDownsample.AllowSpace = false;
            this.textDownsample.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastDownsample", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textDownsample.Location = new System.Drawing.Point(79, 76);
            this.textDownsample.Name = "textDownsample";
            this.textDownsample.Size = new System.Drawing.Size(74, 20);
            this.textDownsample.TabIndex = 9;
            this.textDownsample.Text = global::Viking.Properties.Settings.Default.LastDownsample;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(10, 79);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(71, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "Downsample:";
            // 
            // textZ
            // 
            this.textZ.AllowSpace = false;
            this.textZ.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastGotoZ", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textZ.Location = new System.Drawing.Point(33, 54);
            this.textZ.Name = "textZ";
            this.textZ.Size = new System.Drawing.Size(120, 20);
            this.textZ.TabIndex = 5;
            this.textZ.Text = global::Viking.Properties.Settings.Default.LastGotoZ;
            this.textZ.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoToLocationForm_KeyDown);
            // 
            // textY
            // 
            this.textY.AllowSpace = false;
            this.textY.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastGotoY", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textY.Location = new System.Drawing.Point(33, 32);
            this.textY.Name = "textY";
            this.textY.Size = new System.Drawing.Size(120, 20);
            this.textY.TabIndex = 4;
            this.textY.Text = global::Viking.Properties.Settings.Default.LastGotoY;
            this.textY.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoToLocationForm_KeyDown);
            // 
            // textX
            // 
            this.textX.AllowSpace = false;
            this.textX.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::Viking.Properties.Settings.Default, "LastGotoX", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.textX.Location = new System.Drawing.Point(33, 12);
            this.textX.Name = "textX";
            this.textX.Size = new System.Drawing.Size(120, 20);
            this.textX.TabIndex = 3;
            this.textX.Text = global::Viking.Properties.Settings.Default.LastGotoX;
            this.textX.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoToLocationForm_KeyDown);
            // 
            // GoToLocationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(263, 109);
            this.Controls.Add(this.textDownsample);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.f);
            this.Controls.Add(this.textZ);
            this.Controls.Add(this.textY);
            this.Controls.Add(this.textX);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "GoToLocationForm";
            this.Text = "Go to location";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoToLocationForm_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private Viking.UI.Controls.NumericTextBox textX;
        private Viking.UI.Controls.NumericTextBox textY;
        private Viking.UI.Controls.NumericTextBox textZ;
        private System.Windows.Forms.Button f;
        private System.Windows.Forms.Button btnCancel;
        private Viking.UI.Controls.NumericTextBox textDownsample;
        private System.Windows.Forms.Label label4;
    }
}
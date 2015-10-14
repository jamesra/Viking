namespace Viking.UI.Controls
{
    partial class VolumeListControl
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.listServers = new System.Windows.Forms.ListBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.listVolumes = new System.Windows.Forms.ListView();
            this.textInstructions = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 46);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listServers);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.listVolumes);
            this.splitContainer1.Size = new System.Drawing.Size(352, 182);
            this.splitContainer1.SplitterDistance = 117;
            this.splitContainer1.TabIndex = 0;
            // 
            // listServers
            // 
            this.listServers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listServers.FormattingEnabled = true;
            this.listServers.Location = new System.Drawing.Point(0, 34);
            this.listServers.Name = "listServers";
            this.listServers.Size = new System.Drawing.Size(117, 148);
            this.listServers.TabIndex = 3;
            this.listServers.SelectedIndexChanged += new System.EventHandler(this.listServers_SelectedIndexChanged);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btnRemove);
            this.panel1.Controls.Add(this.btnAdd);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(117, 34);
            this.panel1.TabIndex = 4;
            // 
            // btnRemove
            // 
            this.btnRemove.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnRemove.Location = new System.Drawing.Point(58, 0);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(59, 34);
            this.btnRemove.TabIndex = 2;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnAdd
            // 
            this.btnAdd.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnAdd.Location = new System.Drawing.Point(0, 0);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(59, 34);
            this.btnAdd.TabIndex = 1;
            this.btnAdd.Text = "Add";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // listVolumes
            // 
            this.listVolumes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listVolumes.FullRowSelect = true;
            this.listVolumes.Location = new System.Drawing.Point(0, 0);
            this.listVolumes.MultiSelect = false;
            this.listVolumes.Name = "listVolumes";
            this.listVolumes.Size = new System.Drawing.Size(231, 182);
            this.listVolumes.TabIndex = 4;
            this.listVolumes.UseCompatibleStateImageBehavior = false;
            this.listVolumes.View = System.Windows.Forms.View.List;
            // 
            // textInstructions
            // 
            this.textInstructions.Dock = System.Windows.Forms.DockStyle.Top;
            this.textInstructions.Location = new System.Drawing.Point(0, 0);
            this.textInstructions.Multiline = true;
            this.textInstructions.Name = "textInstructions";
            this.textInstructions.ReadOnly = true;
            this.textInstructions.Size = new System.Drawing.Size(352, 46);
            this.textInstructions.TabIndex = 5;
            this.textInstructions.Text = "Select a server URL to retrieve all of the volumes available.  Select a volume to" +
    " load it.  The Add/Remove buttons are used to alter the server URL list.";
            // 
            // VolumeListControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.textInstructions);
            this.Name = "VolumeListControl";
            this.Size = new System.Drawing.Size(352, 228);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.ListBox listServers;
        private System.Windows.Forms.ListView listVolumes;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.TextBox textInstructions;
    }
}

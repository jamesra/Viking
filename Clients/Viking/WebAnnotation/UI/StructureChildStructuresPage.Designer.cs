namespace WebAnnotation.UI
{
    partial class StructureChildStructuresPage
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
            this.listStructures = new WebAnnotation.UI.Controls.ListStructures();
            this.SuspendLayout();
            // 
            // listStructures
            // 
            this.listStructures.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listStructures.ListBackColor = System.Drawing.SystemColors.Window;
            this.listStructures.ListForeColor = System.Drawing.SystemColors.WindowText;
            this.listStructures.Location = new System.Drawing.Point(0, 0);
            this.listStructures.Name = "listStructures";
            this.listStructures.Size = new System.Drawing.Size(280, 360);
            this.listStructures.TabIndex = 0;
            this.listStructures.Title = "Structures";
            this.listStructures.TitleBackColor = System.Drawing.SystemColors.Highlight;
            this.listStructures.TitleForeColor = System.Drawing.SystemColors.ControlText;
            this.listStructures.TitleVisible = false;
            // 
            // StructureChildStructuresPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.listStructures);
            this.Name = "StructureChildStructuresPage";
            this.VisibleChanged += new System.EventHandler(this.StructureChildStructuresPage_VisibleChanged);
            this.ResumeLayout(false);

        }

        #endregion

        private Controls.ListStructures listStructures;
    }
}

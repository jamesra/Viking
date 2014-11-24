namespace WebAnnotation.UI
{
    partial class StructureLocationsChangeLogPropertiesPage
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
            this.listLocations = new WebAnnotation.UI.Controls.ListLocations();
            this.SuspendLayout();
            // 
            // listLocations
            // 
            this.listLocations.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listLocations.ListBackColor = System.Drawing.SystemColors.Window;
            this.listLocations.ListForeColor = System.Drawing.SystemColors.WindowText;
            this.listLocations.Location = new System.Drawing.Point(0, 0);
            this.listLocations.Name = "listLocations";
            this.listLocations.Size = new System.Drawing.Size(280, 360);
            this.listLocations.TabIndex = 1;
            this.listLocations.Title = "LocationHistory";
            this.listLocations.TitleBackColor = System.Drawing.SystemColors.Highlight;
            this.listLocations.TitleForeColor = System.Drawing.SystemColors.ControlText;
            this.listLocations.TitleVisible = false;
            // 
            // StructureLocationsChangeLogPropertiesPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.listLocations);
            this.Name = "StructureLocationsChangeLogPropertiesPage";
            this.VisibleChanged += new System.EventHandler(this.StructureLocationsChangeLogPropertiesPage_VisibleChanged);
            this.ResumeLayout(false);

        }

        #endregion

        private Controls.ListLocations listLocations;
    }
}

namespace WebAnnotation.UI.Controls
{
    partial class ListLocations
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
            this.SuspendLayout();
            // 
            // ListItems
            // 
            this.ListItems.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Clickable;
            this.ListItems.Location = new System.Drawing.Point(0, 36);
            this.ListItems.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.ListItems.Objects = new Viking.Common.IUIObject[0];
            this.ListItems.Size = new System.Drawing.Size(168, 247);
            // 
            // LabelTitle
            // 
            this.LabelTitle.Margin = new System.Windows.Forms.Padding(9, 0, 9, 0);
            this.LabelTitle.Size = new System.Drawing.Size(168, 36);
            this.LabelTitle.Text = "Locations";
            // 
            // ListLocations
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Margin = new System.Windows.Forms.Padding(9, 12, 9, 12);
            this.Name = "ListLocations";
            this.Title = "Locations";
            this.ResumeLayout(false);

        }

        #endregion
    }
}

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
            this.ListItems.Objects = new Viking.Common.IUIObject[0];
    //        this.ListItems.SelectedObjects = new Viking.Common.IUIObject[0];
            // 
            // LabelTitle
            // 
            this.LabelTitle.Text = "Locations";
            // 
            // ListLocations
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ListLocations";
            this.Title = "Locations";
            this.ResumeLayout(false);

        }

        #endregion
    }
}

namespace WebAnnotation.UI
{
    partial class StructureGeneralPage
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
            this.labelID = new System.Windows.Forms.Label();
            this.labelType = new System.Windows.Forms.Label();
            this.textID = new System.Windows.Forms.TextBox();
            this.labelLabel = new System.Windows.Forms.Label();
            this.textLabel = new System.Windows.Forms.TextBox();
            this.dataGridTags = new System.Windows.Forms.DataGridView();
            this.nameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.valueDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tagBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.labelTags = new System.Windows.Forms.Label();
            this.labelDataGridError = new System.Windows.Forms.Label();
            this.linkType = new Viking.UI.Controls.ObjectLinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridTags)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tagBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // labelID
            // 
            this.labelID.AutoSize = true;
            this.labelID.Location = new System.Drawing.Point(18, 26);
            this.labelID.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelID.Name = "labelID";
            this.labelID.Size = new System.Drawing.Size(34, 25);
            this.labelID.TabIndex = 0;
            this.labelID.Text = "ID:";
            // 
            // labelType
            // 
            this.labelType.AutoSize = true;
            this.labelType.Location = new System.Drawing.Point(18, 56);
            this.labelType.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelType.Name = "labelType";
            this.labelType.Size = new System.Drawing.Size(53, 25);
            this.labelType.TabIndex = 1;
            this.labelType.Text = "Type:";
            // 
            // textID
            // 
            this.textID.Location = new System.Drawing.Point(64, 21);
            this.textID.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textID.Name = "textID";
            this.textID.ReadOnly = true;
            this.textID.Size = new System.Drawing.Size(336, 31);
            this.textID.TabIndex = 5;
            // 
            // labelLabel
            // 
            this.labelLabel.AutoSize = true;
            this.labelLabel.Location = new System.Drawing.Point(24, 116);
            this.labelLabel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelLabel.Name = "labelLabel";
            this.labelLabel.Size = new System.Drawing.Size(57, 25);
            this.labelLabel.TabIndex = 12;
            this.labelLabel.Text = "Label:";
            // 
            // textLabel
            // 
            this.textLabel.Location = new System.Drawing.Point(22, 142);
            this.textLabel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textLabel.Name = "textLabel";
            this.textLabel.Size = new System.Drawing.Size(378, 31);
            this.textLabel.TabIndex = 13;
            // 
            // dataGridTags
            // 
            this.dataGridTags.AutoGenerateColumns = false;
            this.dataGridTags.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridTags.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.dataGridTags.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridTags.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.nameDataGridViewTextBoxColumn,
            this.valueDataGridViewTextBoxColumn});
            this.dataGridTags.DataSource = this.tagBindingSource;
            this.dataGridTags.Location = new System.Drawing.Point(22, 200);
            this.dataGridTags.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.dataGridTags.Name = "dataGridTags";
            this.dataGridTags.RowHeadersWidth = 62;
            this.dataGridTags.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridTags.Size = new System.Drawing.Size(374, 323);
            this.dataGridTags.TabIndex = 14;
            this.dataGridTags.CellErrorTextChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridTags_CellErrorTextChanged);
            this.dataGridTags.CellValidating += new System.Windows.Forms.DataGridViewCellValidatingEventHandler(this.dataGridTags_CellValidating);
            this.dataGridTags.RowErrorTextChanged += new System.Windows.Forms.DataGridViewRowEventHandler(this.dataGridTags_RowErrorTextChanged);
            this.dataGridTags.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridTags_KeyDown);
            // 
            // nameDataGridViewTextBoxColumn
            // 
            this.nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
            this.nameDataGridViewTextBoxColumn.HeaderText = "Name";
            this.nameDataGridViewTextBoxColumn.MinimumWidth = 8;
            this.nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            // 
            // valueDataGridViewTextBoxColumn
            // 
            this.valueDataGridViewTextBoxColumn.DataPropertyName = "Value";
            this.valueDataGridViewTextBoxColumn.HeaderText = "Value";
            this.valueDataGridViewTextBoxColumn.MinimumWidth = 8;
            this.valueDataGridViewTextBoxColumn.Name = "valueDataGridViewTextBoxColumn";
            // 
            // tagBindingSource
            // 
            this.tagBindingSource.DataSource = typeof(WebAnnotationModel.ObjAttribute);
            // 
            // labelTags
            // 
            this.labelTags.AutoSize = true;
            this.labelTags.Location = new System.Drawing.Point(24, 171);
            this.labelTags.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTags.Name = "labelTags";
            this.labelTags.Size = new System.Drawing.Size(51, 25);
            this.labelTags.TabIndex = 10;
            this.labelTags.Text = "Tags:";
            // 
            // labelDataGridError
            // 
            this.labelDataGridError.AutoSize = true;
            this.labelDataGridError.ForeColor = System.Drawing.Color.Red;
            this.labelDataGridError.Location = new System.Drawing.Point(84, 171);
            this.labelDataGridError.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDataGridError.Name = "labelDataGridError";
            this.labelDataGridError.Size = new System.Drawing.Size(0, 25);
            this.labelDataGridError.TabIndex = 15;
            this.labelDataGridError.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // linkType
            // 
            this.linkType.Location = new System.Drawing.Point(22, 86);
            this.linkType.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.linkType.Name = "linkType";
            this.linkType.ReadOnly = true;
            this.linkType.Size = new System.Drawing.Size(380, 32);
            this.linkType.SourceObject = null;
            this.linkType.SourceType = null;
            this.linkType.TabIndex = 4;
            // 
            // StructureGeneralPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.labelDataGridError);
            this.Controls.Add(this.dataGridTags);
            this.Controls.Add(this.textLabel);
            this.Controls.Add(this.labelLabel);
            this.Controls.Add(this.labelTags);
            this.Controls.Add(this.textID);
            this.Controls.Add(this.linkType);
            this.Controls.Add(this.labelType);
            this.Controls.Add(this.labelID);
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.Name = "StructureGeneralPage";
            this.Size = new System.Drawing.Size(420, 540);
            this.Load += new System.EventHandler(this.StructureGeneralPage_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridTags)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tagBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelID;
        private System.Windows.Forms.Label labelType;
        private System.Windows.Forms.TextBox textID;
        private System.Windows.Forms.Label labelLabel;
        private System.Windows.Forms.TextBox textLabel;
        private System.Windows.Forms.DataGridView dataGridTags;
        private System.Windows.Forms.Label labelTags;
        private System.Windows.Forms.DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueDataGridViewTextBoxColumn;
        private System.Windows.Forms.BindingSource tagBindingSource;
        private System.Windows.Forms.Label labelDataGridError;
        private Viking.UI.Controls.ObjectLinkLabel linkType;
    }
}

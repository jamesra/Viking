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
            this.linkType = new Viking.UI.Controls.ObjectLinkLabel();
            this.textID = new System.Windows.Forms.TextBox();
            this.labelLabel = new System.Windows.Forms.Label();
            this.textLabel = new System.Windows.Forms.TextBox();
            this.dataGridTags = new System.Windows.Forms.DataGridView();
            this.nameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.valueDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tagBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.labelTags = new System.Windows.Forms.Label();
            this.labelDataGridError = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridTags)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tagBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // labelID
            // 
            this.labelID.AutoSize = true;
            this.labelID.Location = new System.Drawing.Point(12, 17);
            this.labelID.Name = "labelID";
            this.labelID.Size = new System.Drawing.Size(21, 13);
            this.labelID.TabIndex = 0;
            this.labelID.Text = "ID:";
            // 
            // labelType
            // 
            this.labelType.AutoSize = true;
            this.labelType.Location = new System.Drawing.Point(12, 37);
            this.labelType.Name = "labelType";
            this.labelType.Size = new System.Drawing.Size(34, 13);
            this.labelType.TabIndex = 1;
            this.labelType.Text = "Type:";
            // 
            // linkType
            // 
            this.linkType.Location = new System.Drawing.Point(15, 53);
            this.linkType.Name = "linkType";
            this.linkType.ReadOnly = true;
            this.linkType.Size = new System.Drawing.Size(253, 21);
            this.linkType.SourceObject = null;
            this.linkType.SourceType = null;
            this.linkType.TabIndex = 4;
            // 
            // textID
            // 
            this.textID.Location = new System.Drawing.Point(43, 14);
            this.textID.Name = "textID";
            this.textID.ReadOnly = true;
            this.textID.Size = new System.Drawing.Size(225, 20);
            this.textID.TabIndex = 5;
            // 
            // labelLabel
            // 
            this.labelLabel.AutoSize = true;
            this.labelLabel.Location = new System.Drawing.Point(16, 77);
            this.labelLabel.Name = "labelLabel";
            this.labelLabel.Size = new System.Drawing.Size(36, 13);
            this.labelLabel.TabIndex = 12;
            this.labelLabel.Text = "Label:";
            // 
            // textLabel
            // 
            this.textLabel.Location = new System.Drawing.Point(15, 91);
            this.textLabel.Name = "textLabel";
            this.textLabel.Size = new System.Drawing.Size(253, 20);
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
            this.dataGridTags.Location = new System.Drawing.Point(15, 130);
            this.dataGridTags.Name = "dataGridTags";
            this.dataGridTags.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridTags.Size = new System.Drawing.Size(249, 219);
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
            this.nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            // 
            // valueDataGridViewTextBoxColumn
            // 
            this.valueDataGridViewTextBoxColumn.DataPropertyName = "Value";
            this.valueDataGridViewTextBoxColumn.HeaderText = "Value";
            this.valueDataGridViewTextBoxColumn.Name = "valueDataGridViewTextBoxColumn";
            // 
            // tagBindingSource
            // 
            this.tagBindingSource.DataSource = typeof(WebAnnotationModel.ObjAttribute);
            // 
            // labelTags
            // 
            this.labelTags.AutoSize = true;
            this.labelTags.Location = new System.Drawing.Point(16, 114);
            this.labelTags.Name = "labelTags";
            this.labelTags.Size = new System.Drawing.Size(34, 13);
            this.labelTags.TabIndex = 10;
            this.labelTags.Text = "Tags:";
            // 
            // labelDataGridError
            // 
            this.labelDataGridError.AutoSize = true;
            this.labelDataGridError.ForeColor = System.Drawing.Color.Red;
            this.labelDataGridError.Location = new System.Drawing.Point(56, 114);
            this.labelDataGridError.Name = "labelDataGridError";
            this.labelDataGridError.Size = new System.Drawing.Size(0, 13);
            this.labelDataGridError.TabIndex = 15;
            this.labelDataGridError.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // StructureGeneralPage
            // 
            this.Controls.Add(this.labelDataGridError);
            this.Controls.Add(this.dataGridTags);
            this.Controls.Add(this.textLabel);
            this.Controls.Add(this.labelLabel);
            this.Controls.Add(this.labelTags);
            this.Controls.Add(this.textID);
            this.Controls.Add(this.linkType);
            this.Controls.Add(this.labelType);
            this.Controls.Add(this.labelID);
            this.Name = "StructureGeneralPage";
            this.Load += new System.EventHandler(this.StructureGeneralPage_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridTags)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tagBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelID;
        private System.Windows.Forms.Label labelType;
        private Viking.UI.Controls.ObjectLinkLabel linkType;
        private System.Windows.Forms.TextBox textID;
        private System.Windows.Forms.Label labelLabel;
        private System.Windows.Forms.TextBox textLabel;
        private System.Windows.Forms.DataGridView dataGridTags;
        private System.Windows.Forms.Label labelTags;
        private System.Windows.Forms.DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn valueDataGridViewTextBoxColumn;
        private System.Windows.Forms.BindingSource tagBindingSource;
        private System.Windows.Forms.Label labelDataGridError;
    }
}

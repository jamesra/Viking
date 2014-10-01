namespace Viking.UI.BaseClasses
{
    partial class VikingObjectEventControl
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
            /*
            DBObject.OnNewObject -= OnNewObjectEventHandler;
            DBObject.BeforeAnyDelete -= BeforeAnyDeleteEventHandler;
            DBObject.OnAnyDelete -= OnAnyDeleteEventHandler;
            DBObject.OnAnySave -= OnAnySaveEventHandler;
            DBObject.BeforeAnySave -= BeforeAnySaveEventHandler;
             */

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
            components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        }

        #endregion
    }
}

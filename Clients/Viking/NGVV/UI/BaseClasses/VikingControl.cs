using System;
using System.Diagnostics;

namespace Viking.UI.BaseClasses
{
    public class VikingControl : System.Windows.Forms.UserControl
    {
        /// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

        public VikingControl()
        {
            Trace.WriteLine("Enter CTOR: " + this.GetType().ToString(), "UI");
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            if (!DesignMode)
            {
                //BUG: Had to comment this out to get designer to load the state class.  There must be a static somewhere in the code with a constructor which depends on XNA initialization
                // UI.State.ViewChanged += new Viking.Common.ViewChangeEventHandler(this.OnViewChanged);
            }

            Trace.WriteLine("Exit CTOR: " + this.GetType().ToString(), "UI");

        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
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
            // NGVVControl
            // 
            this.Name = "NGVVControl";
            this.Size = new System.Drawing.Size(640, 480);
            this.ResumeLayout(false);

        }
        #endregion


        protected virtual void OnViewChanged(object sender, Viking.Common.ViewChangeEventArgs e)
        {
            if (this.GetType().ToString() == e.TypeString)
            {
                this.Visible = e.Visible;
            }
        }


        protected int LoadComboItems(System.Windows.Forms.ComboBox CBox, System.Type enumtype)
        {
            Debug.Assert(enumtype.IsEnum, "Non enumerated type passed to LoadComboItems");

            string[] Names = Enum.GetNames(enumtype);
            CBox.Items.AddRange(Names);

            return Names.Length;
        }
        /*
		protected int LoadComboItems(ComboBox CBox, string TableName, string ColumnName)
		{

			object[] Values = new object[0];
     
            Values = PlantMap.Database.Store.GetColumnValues(TableName, ColumnName); 
            if(Values.Length > 0)
            {
                CBox.Items.Clear();
                CBox.Items.AddRange(Values);
            }
            
            return Values.Length; 
          
        }
         */

    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.Forms
{
    /// <summary>
    /// This form is automatically added to the main Viking window as an MDI child and handls
    /// </summary>
    public partial class VikingForm : Form
    {
        /// <summary>
        /// Tab that represents this form on the TabsOpenForms control.
        /// </summary>
        public TabPage Tab;
        //        object FormObject = null;

        /// <summary>
        /// This table tracks which objects are already being shown so we can give those forms focus instead of launching a new one
        /// </summary>
        private static readonly Dictionary<object, VikingForm> ShownFormsTable = new Dictionary<object, VikingForm>();

        public VikingForm()
        {
            Trace.WriteLine("Enter CTOR: " + this.GetType().ToString(), "UI");

            this.MdiParent = UI.State.MdiParent;

            InitializeComponent();

            Trace.WriteLine("Exit CTOR: " + this.GetType().ToString(), "UI");
        }

        public VikingForm(object FormObject)
            : this()
        {
            //	this.FormObject = FormObject; 

            ShownFormsTable.Add(FormObject, this);
        }

        protected static VikingForm Show(Dictionary<IUIObject, VikingForm> FormTable, System.Type FormType, IUIObject Object, System.Windows.Forms.Form ParentForm)
        {
            Debug.Assert(Object != null, "Cannot display properties for null object");
            if (Object == null)
                return null;

            VikingForm ShownForm;

            //return PropertySheetForm.Show(Object.Row, ParentForm);
            if (FormTable.ContainsKey(Object))
            {
                ShownForm = FormTable[Object] as VikingForm;
                ShownForm.Focus();
                return ShownForm;
            }

            return null;
        }

        private void VikingForm_Load(object sender, System.EventArgs e)
        {
            Trace.WriteLine("Enter Load: " + this.GetType().ToString(), "UI");

            //     if (UI.State.MdiParent != null)
            //         UI.State.MdiParent.AddOwnedForm(this);

            Trace.WriteLine("Exit Load: " + this.GetType().ToString(), "UI");

        }
    }
}

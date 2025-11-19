using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.Forms
{


    public partial class PropertySheetForm : Form
    {

        public System.Windows.Forms.DialogResult Result = DialogResult.Cancel;

        protected PropertySheetForm(IUIObjectBasic Object)
        {
            this.Object = Object;
            this.Text = Object.ToString() + " Properties";

            //BUG: This was preventing the form from displaying, so I removed it temporarily.  Will prevent property pages from closing on app exit
            //            this.MdiParent = UI.State.Appwindow;

            InitializeComponent();

            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.MinimumSize = new System.Drawing.Size(400, 440);
        }

        #region Variables

        public IUIObjectBasic Object;

        /// <summary>
        /// Mapping of DBObject.Row instances to property sheets. Used so we only display one property sheet
        /// for any given object
        /// </summary>
        private static readonly Dictionary<IUIObjectBasic, PropertySheetForm> ShownProperties = new Dictionary<IUIObjectBasic, PropertySheetForm>();

        #endregion

        #region Static Methods

        public static PropertySheetForm Show(IUIObjectBasic Object)
        {
            return PropertySheetForm.Show(Object, UI.State.Appwindow);
        }

        public static PropertySheetForm Show(IUIObjectBasic Object, System.Windows.Forms.Form ParentForm)
        {
            PropertySheetForm PropertyForm;

            Debug.Assert(Object != null, "Cannot display properties for null object");
            if (Object is null)
                return null;

            //return PropertySheetForm.Show(Object.Row, ParentForm);
            if (ShownProperties.ContainsKey(Object))
            {
                PropertyForm = ShownProperties[Object] as PropertySheetForm;
                PropertyForm.Focus();
                return PropertyForm;
            }

            //If we aren't showing those properties, create a new property sheet and show it.
            PropertyForm = new PropertySheetForm(Object);

            ShownProperties.Add(Object, PropertyForm);

            PropertyForm.Show();

            return PropertyForm;
        }

        public static PropertySheetForm[] Show(IUIObjectBasic[] Objects, System.Windows.Forms.Form ParentForm)
        {
            List<PropertySheetForm> Forms = new List<PropertySheetForm>(Objects.Length);
            foreach (IUIObject Obj in Objects)
            {
                PropertySheetForm Form = Show(Obj, ParentForm);
                Forms.Add(Form);
            }

            return Forms.ToArray();
        }

        public static System.Windows.Forms.DialogResult ShowDialog(IUIObjectBasic Object, System.Windows.Forms.Form ParentForm)
        {
            //If we aren't showing those properties, create a new property sheet and show it.
            using (PropertySheetForm PropertyForm = new PropertySheetForm(Object))
            {
                PropertyForm.Owner = ParentForm;
                return PropertyForm.ShowDialog();
            }
        }

        #endregion

        #region Control Events

        private void PropertySheetForm_Load(object sender, System.EventArgs e)
        {
            TabsProperty.ShowObject(Object);

            LayoutPropertySheetForm();

            if (this.Tools.Buttons.Count == 0)
            {
                this.Tools.Visible = false;
                //				this.TabsProperty.Height += TabsProperty.Location.Y;
                //this.Height -= TabsProperty.Height; 
                //this.TabsProperty.Location = new Point(0,0);
            }
        }

        private void BtnOK_Click(object sender, System.EventArgs e)
        {
            if (TabsProperty.CanSaveChanges() == false)
            {
                Trace.WriteLine("Apply: Could not save changes", "UI");
                return;

            }

            TabsProperty.SaveChanges();
            Result = DialogResult.OK;
            this.Close();
        }

        private void BtnApply_Click(object sender, System.EventArgs e)
        {
            if (TabsProperty.CanSaveChanges() == false)
            {
                Trace.WriteLine("Apply: Could not save changes", "UI");
                return;
            }

            TabsProperty.SaveChanges();
        }

        private void BtnCancel_Click(object sender, System.EventArgs e)
        {
            TabsProperty.CancelChanges();
            Result = DialogResult.Cancel;
            this.Close();
        }

        #endregion

        #region Methods

        void LayoutPropertySheetForm()
        {
            //if (TabsProperty.IPropertyPages.Length > 1)
            {
                Size MaxTabSize = TabsProperty.RecalculateMaxTabSize();

                Size Margin = new Size
                {
                    Width = this.Width - TabsProperty.Width,
                    Height = this.Height - TabsProperty.Height
                };

                int targetClientWidth = MaxTabSize.Width + Margin.Width;
                int targetClientHeight = MaxTabSize.Height + Margin.Height;

                if (targetClientWidth < this.ClientSize.Width)
                {
                    targetClientWidth = this.ClientSize.Width;
                }

                if (targetClientHeight < this.ClientSize.Height)
                {
                    targetClientHeight = this.ClientSize.Height;
                }

                this.ClientSize = new System.Drawing.Size(targetClientWidth, targetClientHeight);

                foreach (IPropertyPage IPage in TabsProperty.IPropertyPages)
                {
                    IToolBarButtons IButtons = IPage as IToolBarButtons;
                    IButtons?.AddButtons(this.Tools);
                }
            }
        }

        #endregion

        /// <summary>
        /// Removes the object we were showing from the property list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PropertySheetForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //            if (Object.Deleted == false)
            //            {
            if (ShownProperties.ContainsKey(Object as IUIObject))
                ShownProperties.Remove(Object as IUIObject);
            //            }

            return;
        }

    }
}

using System.ComponentModel;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.BaseClasses
{
    public class PropertyPageBase : VikingObjectEventControl, IPropertyPage
    {
        public PropertyPageBase()
        {
            InitializeComponent();
        }

        #region Variables
        [Browsable(true)]
        [Category("Misc")]
        public string Title = "General";
        #endregion

        #region IPropertyPage Members

        void IPropertyPage.InitPage()
        {
            this.OnInitPage();
        }

        void IPropertyPage.ShowObject(object Object)
        {
            this.OnShowObject(Object);
        }

        void IPropertyPage.Reset()
        {
            this.OnReset();
        }

        void IPropertyPage.Enable(bool Enabled)
        {
            this.OnEnable(Enabled);
        }

        System.Windows.Forms.TabPage IPropertyPage.GetPage()
        {
            return this.GetPage();
        }

        bool IPropertyPage.OnValidateChanges()
        {
            return this.OnValidateChanges();
        }

        void IPropertyPage.OnSaveChanges()
        {
            this.OnSaveChanges();
        }

        void IPropertyPage.OnCancelChanges()
        {
            this.OnCancelChanges();
        }

        #endregion

        protected virtual void OnInitPage()
        {
        }

        protected virtual void OnShowObject(object Object)
        {
            return;
        }

        protected virtual void OnReset()
        {
        }

        protected virtual void OnEnable(bool Enabled)
        {
            this.Enabled = Enabled;
        }

        protected virtual System.Windows.Forms.TabPage GetPage()
        {
            TabPage Page = new TabPage(Title);
            Page.Controls.Add(this);
            Page.Width = this.Width;
            Page.Height = this.Height;
            Page.Controls[0].Dock = DockStyle.Fill;
            return Page;
        }

        protected virtual bool OnValidateChanges()
        {
            return true;
        }

        protected virtual void OnSaveChanges()
        {
        }

        protected virtual void OnCancelChanges()
        {
            return;
        }


        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // PropertyPageBase
            // 
            this.Name = "PropertyPageBase";
            this.Size = new System.Drawing.Size(280, 360);
            this.ResumeLayout(false);

        }
    }
}

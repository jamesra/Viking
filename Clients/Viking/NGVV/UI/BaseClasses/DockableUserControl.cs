using System.ComponentModel;
using System.Windows.Forms;
using Viking.Common;

namespace Viking.UI.BaseClasses
{
    public partial class DockableUserControl : VikingObjectEventControl, ITabExtension
    {
        [Browsable(true)]
        public string Title
        {
            get { return LabelTitle.Text; }
            set
            {
                //Convert single '&' to '&&' so they display correctly
                LabelTitle.Text = value.Replace("&", "&&");
            }
        }

        [Browsable(true)]
        [Category("Appearance")]
        public bool TitleVisible
        {
            get { return LabelTitle.Visible; }
            set { LabelTitle.Visible = value; }
        }

        /// <summary>
        /// If we host the control in it's own form this is a pointer to the parent form
        /// </summary>
        private System.Windows.Forms.Form _StandaloneForm = null;

        public System.Windows.Forms.Form GetStandaloneForm()
        {
            if (_StandaloneForm != null)
                return _StandaloneForm;

            _StandaloneForm = new Form();

            _StandaloneForm.Controls.Add(this);
            this.Dock = DockStyle.Fill;
            this.LabelTitle.Visible = false;
            _StandaloneForm.Text = this.LabelTitle.Text;

            _StandaloneForm.Closed += new System.EventHandler(this.OnStandaloneFormClosed);

            return _StandaloneForm;
        }

        protected void OnStandaloneFormClosed(object sender, System.EventArgs e)
        {
            this._StandaloneForm = null;
        }

        public DockableUserControl()
        {
            InitializeComponent();
        }

        #region ITabExtension Members

        public TabPage GetPage()
        {
            return OnGetTabPage();
        }

        protected virtual System.Windows.Forms.TabPage OnGetTabPage()
        {
            TabPage OverviewPage = new TabPage(this.Title);
            OverviewPage.Controls.Add(this);

            this.Dock = DockStyle.Fill;

            //Our title is now displayed on the tab, so we don't need to display the Title Label. 
            this.LabelTitle.Visible = false;

            return OverviewPage;
        }

        #endregion
    }
}

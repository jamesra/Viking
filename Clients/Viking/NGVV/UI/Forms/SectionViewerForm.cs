using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Viking.Common;
using Viking.VolumeModel;
using Viking.ViewModels;

namespace Viking.UI.Forms
{
    public partial class SectionViewerForm : VikingForm
    {
        /// <summary>
        /// Currently displayed section
        /// </summary>
        public SectionViewModel Section 
        {
            get{ return SectionControl.Section;}
            set { SectionControl.Section = value; }
        }

        public SectionViewerForm(SectionViewModel section)
        {
            InitializeComponent();

            this.SectionControl.Section = section;
            this.SectionControl.OnSectionChanged += new SectionChangedEventHandler(OnSectionChanged); 

            if(section != null)
                this.Text = this.BuildTitleString(section.ToString()); 
        }

        private string BuildTitleString(string text)
        {
            string title = text;
            string[] overlayTitles = this.SectionControl.ExtensionOverlayTitles();

            foreach(string ot in overlayTitles)
            {
                title += " " + ot;
            }

            return title;

        }

        public void OnSectionChanged(object sender, SectionChangedEventArgs e)
        {
            if (e.NewSection != null)
            {
                this.Text = this.BuildTitleString(e.NewSection.ToString()); 
            }

            this.Refresh();
        }


        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace)
        {
            this.SectionControl.GoToLocation(location, Z, InputInSectionSpace, SectionControl.Downsample);
        }

        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace, double Downsample)
        {
            this.SectionControl.GoToLocation(location, Z, InputInSectionSpace, Downsample);
        }

        public double CameraDownsample
        {
            get { return this.SectionControl.Downsample; }
            set { this.SectionControl.Downsample = value; }
        }

        /// <summary>
        /// Create a new form or use the existing form and show the specified section
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public static SectionViewerForm Show(SectionViewModel section)
        {
          //  SectionViewerForm form = new SectionViewerForm(section);
          //  form.Show();
            SectionViewerForm form = State.ViewerForm;
            if (form == null)
            {
                form = new SectionViewerForm(section);
                State.ViewerForm = form; 
            }
            else if (form.IsDisposed)
            {
                form = new SectionViewerForm(section);
                State.ViewerForm = form; 
            }
            else
            {
                form.Section = section;
            }

            form.WindowState = FormWindowState.Maximized;
            form.Show();
            

            return form; 
        }


    }
}

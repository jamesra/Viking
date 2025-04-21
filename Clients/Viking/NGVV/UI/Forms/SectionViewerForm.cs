using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using System.Windows.Forms;
using Viking.Common;
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
            get => SectionControl.Section;
            set => SectionControl.Section = value;
        }

        public SectionViewerForm(SectionViewModel section)
        {
            InitializeComponent();

            this.SectionControl.Section = section;
            this.SectionControl.OnSectionChanged += new SectionChangedEventHandler(OnSectionChanged);

            if (section != null)
                this.Text = this.BuildTitleString(section.ToString());
        }

        private string BuildTitleString(string text)
        {
            string title = text;
            string[] overlayTitles = this.SectionControl.ExtensionOverlayTitles();

            foreach (string ot in overlayTitles)
            {
                title += " " + ot;
            }

            return title;

        }

        public async Task OnSectionChanged(object sender, SectionChangedEventArgs e, CancellationToken token)
        {
            if(token.IsCancellationRequested)
                return;

            if (e.NewSection != null)
            {
                this.Invoke(new Action(() => Text = this.BuildTitleString(e.NewSection.ToString())));
            }

            this.Invoke(new Action(() => this.Invalidate()));
        }


        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace)
        {
            this.SectionControl.GoToLocation(location, Z, InputInSectionSpace);
        }

        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace, double Downsample)
        {
            this.SectionControl.GoToLocation(location, Z, InputInSectionSpace, Downsample);
        }

        public double CameraDownsample
        {
            set => this.SectionControl.Downsample = value;
            get => this.SectionControl.Downsample;
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
            if (form is null)
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

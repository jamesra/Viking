using System.ComponentModel;
using System.Windows.Forms;
using Viking.ViewModels;


namespace WebAnnotation.UI.Forms
{
    public partial class PenAnnotationViewForm : Form
    {

        /// <summary>
        /// Currently displayed section
        /// </summary>
        [Browsable(false)]
        public Viking.ViewModels.SectionViewModel Section
        {
            get { return SectionView.Section; }
            set { SectionView.Section = value; }
        }

        public PenAnnotationViewForm()
        {
            InitializeComponent();
        }

        public PenAnnotationViewForm(Viking.ViewModels.SectionViewModel section)
        {
            InitializeComponent();

            this.SectionView.Section = section;
        }

        /// <summary>
        /// Create a new form or use the existing form and show the specified section
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public static PenAnnotationViewForm Show(SectionViewModel section)
        {
            //  SectionViewerForm form = new SectionViewerForm(section);
            //  form.Show();
            PenAnnotationViewForm form = Global.PenAnnotationForm;
            if (form == null)
            {
                form = new PenAnnotationViewForm(section);
                Global.PenAnnotationForm = form;
            }
            else if (form.IsDisposed)
            {
                form = new PenAnnotationViewForm(section);
                Global.PenAnnotationForm = form;
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

using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 2)]
    public partial class StructureNotesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure Obj;

        public StructureNotesPage()
        {
            Title = "Notes";
            InitializeComponent();
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);

            if (null != Obj.Notes)
            {
                textNotes.Text = Obj.Notes;
            }
        }

        protected override void OnSaveChanges()
        {
            Obj.Notes = textNotes.Text;
        }
    }
}

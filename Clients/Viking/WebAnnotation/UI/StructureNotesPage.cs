using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 2)]
    public partial class StructureNotesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        Structure Obj;

        public StructureNotesPage()
        {
            this.Title = "Notes";
            InitializeComponent();
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as Structure;
            Debug.Assert(this.Obj != null);

            if (null != this.Obj.Notes)
                this.textNotes.Text = this.Obj.Notes;
        }

        protected override void OnSaveChanges()
        {
            this.Obj.Notes = this.textNotes.Text;
        }
    }
}

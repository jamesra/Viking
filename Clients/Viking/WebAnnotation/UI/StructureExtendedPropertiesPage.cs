using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;


namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 6)]
    public partial class StructureExtendedPropertiesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        Structure Obj;

        public StructureExtendedPropertiesPage()
        {
            this.Title = "Misc";
            InitializeComponent();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as Structure;
            Debug.Assert(this.Obj != null);

            this.checkVerified.Checked = this.Obj.Verified;
            this.numConfidence.Value = System.Convert.ToDecimal(this.Obj.Confidence);

        }



        protected override void OnSaveChanges()
        {
            this.Obj.Verified = this.checkVerified.Checked;
            this.Obj.Confidence = System.Convert.ToDouble(this.numConfidence.Value);
        }
    }
}

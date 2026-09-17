using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;


namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 6)]
    public partial class StructureExtendedPropertiesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure Obj;

        public StructureExtendedPropertiesPage()
        {
            Title = "Misc";
            InitializeComponent();
        }

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);

            checkVerified.Checked = Obj.Verified;
            numConfidence.Value = System.Convert.ToDecimal(Obj.Confidence);

        }



        protected override void OnSaveChanges()
        {
            Obj.Verified = checkVerified.Checked;
            Obj.Confidence = System.Convert.ToDouble(numConfidence.Value);
        }
    }
}

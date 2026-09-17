using System.Diagnostics;

using Viking.Common;

using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(StructureTypeObj), 2)]
    public partial class StructureTypesRelationsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private StructureTypeObj? Obj = null;

        public StructureTypesRelationsPage()
        {
            Title = "Relations";
            InitializeComponent();
        }

        protected override void OnShowObject(object Object)
        {
            Obj = Object as StructureTypeObj;
            Debug.Assert(Obj != null);

            if (Obj.Parent != null)
            {
                linkParent.SourceObject = Obj.Parent as IUIObject;
            }

            for (int iChild = 0; iChild < Obj.Children.Length; iChild++)
            {
                listChildren.AddObject(Obj.Children[iChild] as IUIObject);
            }
        }

        protected override void OnSaveChanges() => Obj.Parent = linkParent.SourceObject as StructureTypeObj;
    }
}

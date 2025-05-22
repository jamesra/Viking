using System;
using System.Collections.Generic;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 2)]
    public partial class StructureChildStructuresPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure Obj;
        private bool listLoaded = false;

        public StructureChildStructuresPage()
        {
            Title = "Child Structures";

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
        }

        private void StructureChildStructuresPage_VisibleChanged(object sender, EventArgs e)
        {
            if (!listLoaded)
            {

                UseWaitCursor = true;
                ICollection<StructureObj> childStructureObjs = Store.Structures.GetChildStructuresForStructure(Obj.ID);
                List<Structure> childStructures = new List<Structure>(childStructureObjs.Count);

                foreach (StructureObj s in childStructureObjs)
                {
                    childStructures.Add(new Structure(s));
                }

                if (!IsDisposed)
                {
                    listStructures.SetStructures(childStructures.ToArray());

                    listLoaded = true;

                    UseWaitCursor = false;
                }
            }
        }
    }
}

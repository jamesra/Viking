using System;
using System.Collections.Generic;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(StructureType), 3)]
    public partial class StructureTypeStructuresPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private StructureType Obj = null;
        private bool listLoaded = false;

        public StructureTypeStructuresPage()
        {
            InitializeComponent();

            Title = "Structures";
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            Obj = Object as StructureType;
            Debug.Assert(Obj != null);
        }

        /// <summary>
        /// Wait to initialize the list until we are displayed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listStructures_VisibleChanged(object sender, EventArgs e)
        {
            if (!listLoaded)
            {
                UseWaitCursor = true;

                ICollection<StructureObj> structureObjs = Store.Structures.GetStructuresOfType(Obj.ID);

                List<Structure> structures = new List<Structure>(structureObjs.Count);

                foreach (StructureObj s in structureObjs)
                {
                    structures.Add(new Structure(s));
                }

                listStructures.SetStructures(structures.ToArray());

                listLoaded = true;

                UseWaitCursor = false;
            }

        }
    }
}

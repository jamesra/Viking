using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using WebAnnotation.ViewModel;
using Viking.Common;

using WebAnnotationModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(StructureType), 3)]
    public partial class StructureTypeStructuresPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        StructureType Obj = null;

        bool listLoaded = false; 

        public StructureTypeStructuresPage()
        {
            InitializeComponent();

            this.Title = "Structures"; 
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as StructureType;
            Debug.Assert(this.Obj != null);
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
                this.UseWaitCursor = true; 

                StructureObj[] structureObjs = Store.StructureTypes.GetStructuresForType(this.Obj.ID);

                List<Structure> structures = new List<Structure>(structureObjs.Length);

                foreach (StructureObj s in structureObjs)
                {
                    structures.Add(new Structure(s));
                }

                listStructures.SetStructures(structures.ToArray());

                listLoaded = true;

                this.UseWaitCursor = false;
            }

        }
    }
}

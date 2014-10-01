using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WebAnnotation;
using Viking.Common;
using System.Diagnostics;
using WebAnnotationModel;
using WebAnnotation.ViewModel; 

namespace WebAnnotation.UI.Controls
{
    [Viking.Common.SupportedUITypes(typeof(StructureObj))]
    public partial class ListStructures : Viking.UI.BaseClasses.DockingListControl
    {
        Structure[] _structures;
        
        EventHandler StructureCreateEventHandler;

        public ListStructures()
        {
            InitializeComponent();

            this.ListItems.ShowPropertiesOnDoubleClick = false;
            InitializeComponent();

            StructureCreateEventHandler = new EventHandler(OnLocationCreate);
            LocationObj.Create += StructureCreateEventHandler; 
        }

        public void SetStructures(Structure[] structures)
        {
            this._structures = structures; 

            this.ListItems.DisplayObjects(_structures);
        }

        protected override void OnObjectDoubleClick(IUIObject obj)
        {
            Structure s = obj as Structure;
            Debug.Assert(s != null);

            LocationObj centerLoc = s.Center;
            if(centerLoc != null)
                AnnotationOverlay.GoToLocation(centerLoc);
        }

        public void OnLocationCreate(object sender, EventArgs e)
        {
            Structure structure = sender as Structure;
            Debug.Assert(structure != null);
            if (structure != null)
                this.ListItems.AddObject(structure);

        }

        protected override void parentForm_Closing(object sender, CancelEventArgs e)
        {
            LocationObj.Create -= StructureCreateEventHandler;

            base.parentForm_Closing(sender, e);
        }

        private void ListStructures_Load(object sender, EventArgs e)
        {
        }
    }
}

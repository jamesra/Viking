using System;
using System.ComponentModel;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
            if (centerLoc != null)
                AnnotationOverlay.GoToLocation(centerLoc);
        }

        public void OnLocationCreate(object sender, EventArgs e)
        {
            Structure structure = sender as Structure;
            Debug.Assert(structure != null);
            if (structure != null)
                if (InvokeRequired)
                {
                    this.ListItems.Invoke(new Action(() => this.ListItems.AddObject(structure)));
                }
                else
                {
                    this.ListItems.AddObject(structure);
                }
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

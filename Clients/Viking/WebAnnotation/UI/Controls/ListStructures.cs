using System;
using System.Collections.Specialized;
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
        private Structure[] _structures;
        private readonly NotifyCollectionChangedEventHandler StructureCreateEventHandler;

        public ListStructures()
        {
            InitializeComponent();

            ListItems.ShowPropertiesOnDoubleClick = false;
            InitializeComponent();

            StructureCreateEventHandler = new NotifyCollectionChangedEventHandler(OnStructuresCollectionChanged);
            Store.Structures.OnCollectionChanged += StructureCreateEventHandler;
        }

        public void SetStructures(Structure[] structures)
        {
            _structures = structures;

            ListItems.DisplayObjects(_structures);
        }

        protected override void OnObjectDoubleClick(IUIObject obj)
        {
            Structure s = obj as Structure;
            Debug.Assert(s != null);

            LocationObj centerLoc = s.Center;
            if (centerLoc != null)
            {
                AnnotationOverlay.GoToLocation(centerLoc);
            }
        }

        private void OnStructuresCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
            {
                return;
            }

            foreach (StructureObj addedObj in e.NewItems)
            {
                OnStructureCreate(new Structure(addedObj));
            }
        }

        private void OnStructureCreate(Structure structure)
        {
            Debug.Assert(structure != null);
            if (structure != null)
            {
                if (InvokeRequired)
                {
                    ListItems.Invoke(new Action(() => ListItems.AddObject(structure)));
                }
                else
                {
                    ListItems.AddObject(structure);
                }
            }
        }

        protected override void parentForm_Closing(object sender, CancelEventArgs e)
        {
            Store.Structures.OnCollectionChanged -= StructureCreateEventHandler;

            base.parentForm_Closing(sender, e);
        }

        private void ListStructures_Load(object sender, EventArgs e)
        {
        }
    }
}

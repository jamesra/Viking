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
    [Viking.Common.SupportedUITypes(typeof(Location_PropertyPageViewModel))]
    public partial class ListLocations : Viking.UI.BaseClasses.DockingListControl
    {
        private Location_PropertyPageViewModel[] _locations;
        private readonly NotifyCollectionChangedEventHandler LocationCreateEventHandler;


        public ListLocations()
        {
            ListItems.ShowPropertiesOnDoubleClick = false;
            InitializeComponent();

            LocationCreateEventHandler = new NotifyCollectionChangedEventHandler(OnLocationsCollectionChanged);
            Store.Locations.OnCollectionChanged += LocationCreateEventHandler;
        }

        public void SetLocations(Location_PropertyPageViewModel[] locations)
        {
            _locations = locations;

            ListItems.DisplayObjects(_locations);
        }

        protected override void OnObjectDoubleClick(IUIObject obj)
        {
            Location_PropertyPageViewModel loc = obj as Location_PropertyPageViewModel;
            Debug.Assert(loc != null);

            AnnotationOverlay.GoToLocation(loc.modelObj);
        }

        private void OnLocationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
            {
                return;
            }

            foreach (LocationObj addedObj in e.NewItems)
            {
                OnLocationCreate(new Location_PropertyPageViewModel(addedObj.ID));
            }
        }

        private void OnLocationCreate(Location_PropertyPageViewModel loc)
        {
            if (loc != null)
            {
                if (InvokeRequired)
                {
                    ListItems.Invoke(new Action(() => ListItems.AddObject(loc)));
                }
                else
                {
                    ListItems.AddObject(loc);
                }
            }
        }

        protected override void parentForm_Closing(object sender, CancelEventArgs e)
        {
            Store.Locations.OnCollectionChanged -= LocationCreateEventHandler;

            base.parentForm_Closing(sender, e);
        }
    }
}

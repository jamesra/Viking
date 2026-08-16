using System;
using System.Collections.Generic;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 3)]
    public partial class StructureLocationsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure Obj;
        private bool listLoaded = false;

        public StructureLocationsPage()
        {

            InitializeComponent();
            Title = "Locations";
            listLocations.Title = "Locations";
            listLocations.TitleVisible = false;
        }

        protected override void OnInitPage() => base.OnInitPage();

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);
        }

        private void StructureLocationsPage_VisibleChanged(object sender, EventArgs e)
        {
            if (!listLoaded)
            {

                UseWaitCursor = true;
                ICollection<LocationObj> locations = Store.Locations.GetLocationsForStructure(Obj.ID);
                List<Location_PropertyPageViewModel> listLocationViews = new(locations.Count);

                foreach (LocationObj loc in locations)
                {
                    listLocationViews.Add(new Location_PropertyPageViewModel(loc.ID));
                }

                listLocations.SetLocations([.. listLocationViews]);

                listLoaded = true;


                UseWaitCursor = false;
            }
        }
    }
}

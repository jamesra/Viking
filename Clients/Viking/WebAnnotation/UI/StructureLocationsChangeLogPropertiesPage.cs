using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI
{


    //TODO: Work in progresss [PropertyPage(typeof(Structure), 4)]
    public partial class StructureLocationsChangeLogPropertiesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure Obj;
        private readonly BindingList<WebAnnotationModel.Objects.ObjAttribute>? ListTags = null;
        private bool listLoaded = false;

        public StructureLocationsChangeLogPropertiesPage()
        {

            InitializeComponent();
            Title = "Location Change Log";
        }

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);
        }


        private void StructureLocationsChangeLogPropertiesPage_VisibleChanged(object sender, EventArgs e)
        {
            if (!listLoaded)
            {
                listLoaded = true;
                UseWaitCursor = true;
                ICollection<LocationObj> locations = Store.Locations.GetStructureLocationChangeLog(Obj.ID);
                List<Location_PropertyPageViewModel> listLocationViews = new(locations.Count);

                foreach (LocationObj loc in locations)
                {
                    listLocationViews.Add(new Location_PropertyPageViewModel(loc.ID));
                }

                listLocations.SetLocations([.. listLocationViews]);

                UseWaitCursor = false;
            }
        }
    }
}

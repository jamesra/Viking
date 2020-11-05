using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{


    //TODO: Work in progresss [PropertyPage(typeof(Structure), 4)]
    public partial class StructureLocationsChangeLogPropertiesPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        Structure Obj;

        BindingList<WebAnnotationModel.ObjAttribute> ListTags = null;

        bool listLoaded = false;

        public StructureLocationsChangeLogPropertiesPage()
        {

            InitializeComponent();
            this.Title = "Location Change Log";
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as Structure;
            Debug.Assert(this.Obj != null);
        }


        private void StructureLocationsChangeLogPropertiesPage_VisibleChanged(object sender, EventArgs e)
        {
            if (!listLoaded)
            {
                listLoaded = true;
                this.UseWaitCursor = true;
                ICollection<LocationObj> locations = Store.Locations.GetStructureLocationChangeLog(Obj.ID);
                List<Location_PropertyPageViewModel> listLocationViews = new List<Location_PropertyPageViewModel>(locations.Count);

                foreach (LocationObj loc in locations)
                {
                    listLocationViews.Add(new Location_PropertyPageViewModel(loc.ID));
                }

                listLocations.SetLocations(listLocationViews.ToArray());

                this.UseWaitCursor = false;
            }
        }
    }
}

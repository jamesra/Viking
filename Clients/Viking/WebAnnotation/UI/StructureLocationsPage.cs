using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using System.Diagnostics;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 3)]
    public partial class StructureLocationsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        Structure Obj;

        bool listLoaded = false; 

        public StructureLocationsPage()
        {
            
            InitializeComponent();
            this.Title = "Locations";
            this.listLocations.Title = "Locations";
            this.listLocations.TitleVisible = false; 
        }

        protected override void OnInitPage()
        {
            base.OnInitPage();
        }

        protected override void OnShowObject(object Object)
        {
            this.Obj = Object as Structure;
            Debug.Assert(this.Obj != null);            
        }

        private void StructureLocationsPage_VisibleChanged(object sender, EventArgs e)
        {
            if (!listLoaded)
            {

                this.UseWaitCursor = true;
                ICollection<LocationObj> locations = Store.Locations.GetLocationsForStructure(Obj.ID);
                List<Location_PropertyPageViewModel> listLocationViews = new List<Location_PropertyPageViewModel>(locations.Count);

                foreach (LocationObj loc in locations)
                {
                    listLocationViews.Add(new Location_PropertyPageViewModel(loc.ID));
                }

                listLocations.SetLocations(listLocationViews.ToArray());

                listLoaded = true;


                this.UseWaitCursor = false;
            }
        }
    }
}

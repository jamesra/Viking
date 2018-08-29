using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WebAnnotation;
using Viking.Common;
using System.Diagnostics;
using WebAnnotationModel;
using WebAnnotation.ViewModel; 

namespace WebAnnotation.UI.Controls
{
    [Viking.Common.SupportedUITypes(typeof(Location_PropertyPageViewModel))]
    public partial class ListLocations : Viking.UI.BaseClasses.DockingListControl
    {
        Location_PropertyPageViewModel[] _locations;

        EventHandler LocationCreateEventHandler;
        
        
        public ListLocations()
        {
            this.ListItems.ShowPropertiesOnDoubleClick = false;
            InitializeComponent();

            LocationCreateEventHandler = new EventHandler(OnLocationCreate);
            LocationObj.Create += LocationCreateEventHandler; 
        }

        public void SetLocations(Location_PropertyPageViewModel[] locations)
        {
            this._locations = locations;

            this.ListItems.DisplayObjects(_locations); 
        }

        protected override void OnObjectDoubleClick(IUIObject obj)
        {
            Location_PropertyPageViewModel loc = obj as Location_PropertyPageViewModel;
            Debug.Assert(loc != null);

            AnnotationOverlay.GoToLocation(loc.modelObj); 
        }

        public void OnLocationCreate(object sender, EventArgs e)
        {
            Location_PropertyPageViewModel loc = sender as Location_PropertyPageViewModel;
            Debug.Assert(loc != null);
            if (loc != null)
            {
                if (InvokeRequired)
                {
                    this.ListItems.Invoke(new Action(() => this.ListItems.AddObject(loc)));
                }
                else
                {
                    this.ListItems.AddObject(loc);
                }
            }
        }

        protected override void parentForm_Closing(object sender, CancelEventArgs e)
        {
            LocationObj.Create -= LocationCreateEventHandler;

            base.parentForm_Closing(sender, e);
        }
    }
}

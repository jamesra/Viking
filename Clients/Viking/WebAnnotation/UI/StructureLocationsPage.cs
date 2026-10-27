using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI
{
    [PropertyPage(typeof(Structure), 3)]
    public partial class StructureLocationsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure? Obj;

        /// <summary>
        /// Counts the structures this page has been asked to show. A list load carries the number it started with, so the
        /// result of a slow load for a structure that is no longer shown is thrown away.
        /// </summary>
        private int shownObjectNumber;

        /// <summary>The <see cref="shownObjectNumber"/> whose list has been loaded or is loading; 0 when none has.</summary>
        private int loadedObjectNumber;

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

            // The list on screen, if any, belongs to the structure shown before. Clear it, and if the tab is already
            // showing, load the new structure's locations now: no VisibleChanged will come to do it.
            shownObjectNumber++;
            listLocations.SetLocations([]);
            if (Visible)
            {
                _ = LoadLocationsAsync();
            }
        }

        private void StructureLocationsPage_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                _ = LoadLocationsAsync();
            }
        }

        /// <summary>
        /// Asks the server for the structure's locations and lists them. The server call runs off the UI thread, so the
        /// tab and the rest of Viking stay responsive while it waits; the list is filled in when it returns. Does nothing
        /// when this structure's list is already loaded or loading.
        /// </summary>
        private async Task LoadLocationsAsync()
        {
            Structure structure = Obj;
            if (structure is null || loadedObjectNumber == shownObjectNumber)
            {
                return;
            }

            int objectNumber = shownObjectNumber;
            loadedObjectNumber = objectNumber;
            UseWaitCursor = true;
            try
            {
                long structureID = structure.ID;
                ICollection<LocationObj> locations = await Task.Run(() => Store.Locations.GetLocationsForStructure(structureID));

                // Another structure was shown while the server answered, or the page is gone.
                if (objectNumber != shownObjectNumber || IsDisposed)
                {
                    return;
                }

                List<Location_PropertyPageViewModel> listLocationViews = new(locations.Count);
                foreach (LocationObj loc in locations)
                {
                    listLocationViews.Add(new Location_PropertyPageViewModel(loc.ID));
                }

                listLocations.SetLocations([.. listLocationViews]);
            }
            catch (Exception e)
            {
                // Let the next time the tab shows try again instead of leaving it empty for good.
                Trace.WriteLine($"Could not list the locations of structure {structure.ID}: {e}");
                if (loadedObjectNumber == objectNumber)
                {
                    loadedObjectNumber = 0;
                }
            }
            finally
            {
                if (objectNumber == shownObjectNumber)
                {
                    UseWaitCursor = false;
                }
            }
        }
    }
}

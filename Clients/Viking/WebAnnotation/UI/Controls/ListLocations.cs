using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Viking.Common;
using WebAnnotation.StructureLocations;
using WebAnnotationModel;

namespace WebAnnotation.UI.Controls
{
    /// <summary>
    /// The table of a structure's locations (<see cref="StructureLocationsPage"/>), filled a page at a time as the OData
    /// pages arrive.
    /// </summary>
    [Viking.Common.SupportedUITypes(typeof(StructureLocationRow))]
    public partial class ListLocations : Viking.UI.BaseClasses.DockingListControl
    {
        public ListLocations()
        {
            ListItems.ShowPropertiesOnDoubleClick = false;
            InitializeComponent();
        }

        /// <summary>Empties the table and sets its columns.</summary>
        public void ClearLocations() => ListItems.DisplayObjects(Array.Empty<StructureLocationRow>());

        /// <summary>Adds one page of rows below the ones already listed (in the current sort order when the user sorted).</summary>
        public void AppendLocations(StructureLocationRow[] rows)
        {
            ListItems.BeginUpdate();
            try
            {
                foreach (StructureLocationRow row in rows)
                {
                    ListItems.AddObject(row);
                }
            }
            finally
            {
                ListItems.EndUpdate();
            }
        }

        /// <summary>
        /// Lists whole locations, replacing the table. Used by the unfinished change-log page
        /// (<see cref="StructureLocationsChangeLogPropertiesPage"/>); the Locations tab lists <see cref="StructureLocationRow"/>s.
        /// </summary>
        public void SetLocations(ViewModel.Location_PropertyPageViewModel[] locations) => ListItems.DisplayObjects(locations);

        /// <summary>Number of rows listed.</summary>
        public int LocationCount => ListItems.Items.Count;

        /// <summary>
        /// Goes to the row's location. The row has no shape, so the location is loaded first, off the UI thread (one server
        /// call when the store does not hold it).
        /// </summary>
        protected override async void OnObjectDoubleClick(IUIObject obj)
        {
            if (obj is not StructureLocationRow row)
            {
                return;
            }

            try
            {
                LocationObj? location = await Task.Run(() => Store.Locations.GetObjectByID(row.ID));
                if (location is null)
                {
                    Trace.WriteLine($"Location {row.ID} could not be loaded to go to it");
                    return;
                }

                AnnotationOverlay.GoToLocation(location);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Could not go to location {row.ID}: {e}");
            }
        }
    }
}

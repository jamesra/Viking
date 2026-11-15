using System;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.StructureLocations;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    /// <summary>
    /// The Locations tab of a structure's properties: the structure's locations as a table. The rows come from the OData
    /// service without any shapes (<see cref="StructureLocationsODataQuery"/>), a page at a time, and each page is listed as
    /// it arrives (<see cref="ODataListLoad{TRow}"/>), so a cell with thousands of locations shows its first rows quickly even
    /// on a slow connection. A line above the table says how far the load got, and a failed load says why and offers Retry.
    /// </summary>
    [PropertyPage(typeof(Structure), 3)]
    public partial class StructureLocationsPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure? Obj;

        private readonly ODataListLoad<StructureLocationRow> load;

        public StructureLocationsPage()
        {
            InitializeComponent();
            Title = "Locations";
            listLocations.Title = "Locations";
            listLocations.TitleVisible = false;

            load = new ODataListLoad<StructureLocationRow>(this, "location", "locations",
                root => StructureLocationsODataQuery.BuildUri(root, Obj!.ID),
                json => (StructureLocationsODataQuery.ParsePage(json, out Uri? next), next),
                listLocations.ClearLocations,
                listLocations.AppendLocations,
                () => listLocations.LocationCount);
        }

        protected override void OnInitPage() => base.OnInitPage();

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);

            // The list on screen, if any, belongs to the structure shown before. If the tab is already showing, load the new
            // structure's locations now: no VisibleChanged will come to do it.
            load.Reset();
            if (Visible)
            {
                _ = load.LoadIfNeededAsync();
            }
        }

        private void StructureLocationsPage_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                _ = load.LoadIfNeededAsync();
            }
        }
    }
}

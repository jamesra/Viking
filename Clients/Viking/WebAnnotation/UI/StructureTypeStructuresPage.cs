using System;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.StructureLocations;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    /// <summary>
    /// The Structures tab of a structure type's properties: every structure of the type as a table, read from OData a page
    /// at a time off the UI thread (<see cref="StructureListODataQuery.OfTypeUri"/>, <see cref="ODataListLoad{TRow}"/>), so
    /// a type with tens of thousands of structures shows its first rows quickly. The load's progress, count or failure (and
    /// Retry) is shown above the table.
    /// </summary>
    [PropertyPage(typeof(StructureType), 3)]
    public partial class StructureTypeStructuresPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private StructureType? Obj = null;

        private readonly ODataListLoad<StructureRow> load;

        public StructureTypeStructuresPage()
        {
            InitializeComponent();

            Title = "Structures";

            load = new ODataListLoad<StructureRow>(this, "structure", "structures",
                root => StructureListODataQuery.OfTypeUri(root, Obj!.ID),
                json => (StructureListODataQuery.ParsePage(json, out Uri? next), next),
                listStructures.ClearStructures,
                listStructures.AppendStructures,
                () => listStructures.StructureCount);
        }

        protected override void OnInitPage() => base.OnInitPage();

        protected override void OnShowObject(object Object)
        {
            Obj = Object as StructureType;
            Debug.Assert(Obj != null);

            load.Reset();
            if (Visible)
            {
                _ = load.LoadIfNeededAsync();
            }
        }

        /// <summary>Loads the list the first time it is shown for this type.</summary>
        private void listStructures_VisibleChanged(object sender, EventArgs e)
        {
            if (listStructures.Visible)
            {
                _ = load.LoadIfNeededAsync();
            }
        }
    }
}

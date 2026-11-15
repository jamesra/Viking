using System;
using System.Diagnostics;
using Viking.Common;
using WebAnnotation.StructureLocations;
using WebAnnotation.ViewModel;

namespace WebAnnotation.UI
{
    /// <summary>
    /// The Child Structures tab of a structure's properties: the structure's children as a table, read from OData a page at
    /// a time off the UI thread (<see cref="StructureListODataQuery.ChildrenUri"/>, <see cref="ODataListLoad{TRow}"/>), with
    /// the load's progress, count or failure (and Retry) above the table.
    /// </summary>
    [PropertyPage(typeof(Structure), 2)]
    public partial class StructureChildStructuresPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        private Structure? Obj;

        private readonly ODataListLoad<StructureRow> load;

        public StructureChildStructuresPage()
        {
            Title = "Child Structures";

            InitializeComponent();

            load = new ODataListLoad<StructureRow>(this, "child structure", "child structures",
                root => StructureListODataQuery.ChildrenUri(root, Obj!.ID),
                json => (StructureListODataQuery.ParsePage(json, out Uri? next), next),
                listStructures.ClearStructures,
                listStructures.AppendStructures,
                () => listStructures.StructureCount);
        }

        protected override void OnInitPage() => base.OnInitPage();

        protected override void OnShowObject(object Object)
        {
            Obj = Object as Structure;
            Debug.Assert(Obj != null);

            load.Reset();
            if (Visible)
            {
                _ = load.LoadIfNeededAsync();
            }
        }

        private void StructureChildStructuresPage_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
            {
                _ = load.LoadIfNeededAsync();
            }
        }
    }
}

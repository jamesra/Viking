using System;
using Viking.Common;
using WebAnnotation.StructureLocations;

namespace WebAnnotation.UI.Controls
{
    /// <summary>
    /// The table of structures in a property tab (a structure's children, a type's structures), filled a page at a time as
    /// the OData pages arrive (<see cref="ODataListLoad{TRow}"/>).
    /// </summary>
    [Viking.Common.SupportedUITypes(typeof(StructureRow))]
    public partial class ListStructures : Viking.UI.BaseClasses.DockingListControl
    {
        public ListStructures()
        {
            InitializeComponent();
            ListItems.ShowPropertiesOnDoubleClick = false;
        }

        /// <summary>Empties the table and sets its columns.</summary>
        public void ClearStructures() => ListItems.DisplayObjects(Array.Empty<StructureRow>());

        /// <summary>Adds one page of rows below the ones already listed (in the current sort order when the user sorted).</summary>
        public void AppendStructures(StructureRow[] rows)
        {
            ListItems.BeginUpdate();
            try
            {
                foreach (StructureRow row in rows)
                {
                    ListItems.AddObject(row);
                }
            }
            finally
            {
                ListItems.EndUpdate();
            }
        }

        /// <summary>Number of rows listed.</summary>
        public int StructureCount => ListItems.Items.Count;

        /// <summary>Goes to the structure's location nearest its middle (<see cref="StructureGoTo.Center"/>).</summary>
        protected override void OnObjectDoubleClick(IUIObject obj)
        {
            if (obj is StructureRow row)
            {
                _ = StructureGoTo.GoToAsync(row.ID, placed => StructureGoTo.Center(placed, StructureGoTo.GlobalScale));
            }
        }

        private void ListStructures_Load(object sender, EventArgs e)
        {
        }
    }
}

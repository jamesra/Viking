using System;
using System.Windows.Forms;
using WebAnnotation.ViewModel;

namespace WebAnnotation.View
{
    internal class Location_CanvasContextMenuView(long LocationID) : Location_ViewModelBase(LocationID)
    {
        public static ContextMenuStrip ContextMenuGenerator(IViewLocation loc)
        {
            Location_CanvasContextMenuView contextMenuView = null;
            try
            {
                contextMenuView = new Location_CanvasContextMenuView(loc.ID);
            }
            catch (ArgumentException)
            {
                ContextMenuStrip menu = new();
                menu.Items.Add($"Unable to load location {loc.ID}");
                return menu;
            }

            return contextMenuView.ContextMenu;
        }

        public override ContextMenuStrip ContextMenu
        {
            get
            {
                ContextMenuStrip menu = new();
                ToolStripMenuItem propertiesItem = new("Properties");
                propertiesItem.Click += ContextMenu_OnProperties;
                menu.Items.Add(propertiesItem);

                _AddExportMenus(menu);
                _AddCopyLocationIDMenu(menu);
                _AddTerminalOffEdgeMenus(menu);
                Parent.ContextMenu_AddUnverifiedBranchTerminals(menu);
                _AddConvertShapeMenus(menu);
                _AddSimplifyPolygonMenus(menu);
                _AddRandomColorMenu(menu);
                _AddDeleteMenu(menu);

                return menu;
            }
        }
    }
}

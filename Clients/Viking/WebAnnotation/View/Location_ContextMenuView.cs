using System;
using System.Windows.Forms;
using WebAnnotation.ViewModel;

namespace WebAnnotation.View
{
    internal class Location_CanvasContextMenuView : Location_ViewModelBase
    {
        public Location_CanvasContextMenuView(long LocationID) : base(LocationID) { }

        public static ContextMenu ContextMenuGenerator(IViewLocation loc)
        {
            Location_CanvasContextMenuView contextMenuView = null;
            try
            {
                contextMenuView = new Location_CanvasContextMenuView(loc.ID);
            }
            catch (ArgumentException)
            {
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add($"Unable to load location {loc.ID}");
                return menu;
            }

            return contextMenuView.ContextMenu;
        }

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                _AddExportMenus(menu);
                _AddCopyLocationIDMenu(menu);
                _AddTerminalOffEdgeMenus(menu);
                Parent.ContextMenu_AddUnverifiedBranchTerminals(menu);
                _AddConvertShapeMenus(menu);
                _AddSimplifyPolygonMenus(menu);
                _AddDeleteMenu(menu);

                return menu;
            }
        }
    }
}

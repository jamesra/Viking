using System;
using System.Windows.Forms;
using WebAnnotation.ViewModel;

namespace WebAnnotation.View
{
    class Location_CanvasContextMenuView : Location_ViewModelBase
    {
        public Location_CanvasContextMenuView(long LocationID) : base(LocationID) { }

        public static ContextMenu ContextMenuGenerator(IViewLocation loc)
        {
            Location_CanvasContextMenuView contextMenuView = null;
            try
            {
                contextMenuView = new Location_CanvasContextMenuView(loc.ID);
            }
            catch (ArgumentException e)
            {
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add(string.Format("Unable to load location {0}", loc.ID));
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

                this._AddExportMenus(menu);
                this._AddCopyLocationIDMenu(menu);
                this._AddTerminalOffEdgeMenus(menu);
                this.Parent.ContextMenu_AddUnverifiedBranchTerminals(menu);
                this._AddConvertShapeMenus(menu);
                this._AddSimplifyPolygonMenus(menu);
                this._AddDeleteMenu(menu);

                return menu;
            }
        }
    }
}

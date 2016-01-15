using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using System.Windows.Forms;

namespace WebAnnotation.View
{
    class Location_CanvasContextMenuView : Location_ViewModelBase
    {
        public Location_CanvasContextMenuView(LocationObj obj) : base(obj) { }

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
                this._AddDeleteMenu(menu);

                return menu;
            }
        } 
    }
}

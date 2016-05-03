using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using System.Windows.Forms;
using Viking.Common;

namespace WebAnnotation.View
{
    class LocationLink_CanvasContextMenuView : IContextMenu
    {
        public LocationLinkKey linkKey;

        public LocationLink_CanvasContextMenuView(LocationLinkKey link)
        {
            this.linkKey = link;
        }

        public System.Windows.Forms.ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                MenuItem menuSeperator = new MenuItem();
                MenuItem menuDelete = new MenuItem("Delete Link", ContextMenu_OnDelete);

                menu.MenuItems.Add(menuSeperator);
                menu.MenuItems.Add(menuDelete);

                return menu;
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public void Delete()
        {
            Store.LocationLinks.DeleteLink(linkKey.A, linkKey.B);
        }
    }
}

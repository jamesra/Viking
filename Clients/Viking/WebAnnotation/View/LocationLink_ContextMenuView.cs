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

        public static ContextMenu ContextMenuGenerator(IViewLocationLink link)
        {
            LocationLink_CanvasContextMenuView contextMenuView = new LocationLink_CanvasContextMenuView(link.Key);
            return contextMenuView.ContextMenu;
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

                menu.MenuItems.Add(menuSeperator);

                MenuItem menuSplit = new MenuItem("Split structure", ContextMenu_OnSplit);
                menu.MenuItems.Add(menuSplit);

                return menu;
            }
        }

        static WebAnnotation.UI.SplitStructuresForm SplitForm = null;
        protected void ContextMenu_OnSplit(object sender, EventArgs e)
        {
            if (SplitForm == null)
            {
                SplitForm = new WebAnnotation.UI.SplitStructuresForm();
                SplitForm.SplitID = this.linkKey.A;
                SplitForm.KeepID = this.linkKey.B;
                SplitForm.FormClosed += OnSplitFormClosed;
                SplitForm.Show();
            }            
        }

        static private void OnSplitFormClosed(object sender, FormClosedEventArgs e)
        {
            SplitForm = null; 
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

using System;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.View
{
    public class LocationLink_CanvasContextMenuView : IProvideContextMenus
    {  
        public LocationLink_CanvasContextMenuView()
        {
        }
         
        private static WebAnnotation.UI.SplitStructuresForm SplitForm = null;
        protected void ContextMenu_OnSplit(object sender, EventArgs e)
        {
            if (SplitForm is null)
            { 
                if (sender is MenuItem menuItem)
                {
                    if (menuItem.Tag is LocationLinkKey linkKey)
                    {

                        SplitForm = new WebAnnotation.UI.SplitStructuresForm
                        {
                            SplitID = linkKey.A,
                            KeepID = linkKey.B
                        };
                        SplitForm.FormClosed += OnSplitFormClosed;
                        SplitForm.Show();
                    }
                }
            }

        }

        private static void OnSplitFormClosed(object sender, FormClosedEventArgs e)
        {
            SplitForm = null;
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                if (menuItem.Tag is LocationLinkKey linkKey)
                {
                    Store.LocationLinks.DeleteLink(linkKey.A, linkKey.B);
                }
            } 
        } 
        public ContextMenu BuildMenuFor(object Obj, ContextMenu menu)
        {
            if(menu is null)
                return null;

            if (Obj is LocationLinkView link)
            {
                MenuItem menuSeperator = new MenuItem();
                MenuItem menuDelete = new MenuItem("Delete Link", ContextMenu_OnDelete);
                menuDelete.Tag = link.Key;

                menu.MenuItems.Add(menuSeperator);
                menu.MenuItems.Add(menuDelete);

                menu.MenuItems.Add(menuSeperator);

                MenuItem menuSplit = new MenuItem("Split structure", ContextMenu_OnSplit);
                menuSplit.Tag = link.Key;
                menu.MenuItems.Add(menuSplit);

                return menu;
            }

            return menu;
        }

        public ContextMenu BuildMenuFor(Type ObjType, ContextMenu Menu)
        {
            return Menu;
        }
    }
}

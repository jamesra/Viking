using System;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes;
using Viking.Common;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.View
{
    public class LocationLink_CanvasContextMenuView : IProvideContextMenus
    {
        public LocationLink_CanvasContextMenuView()
        {
        }

        private static WebAnnotation.UI.SplitStructuresForm? SplitForm = null;
        protected void ContextMenu_OnSplit(object sender, EventArgs e)
        {
            if (SplitForm is null)
            {
                if (sender is ToolStripMenuItem menuItem)
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

        private static void OnSplitFormClosed(object sender, FormClosedEventArgs e) => SplitForm = null;

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem)
            {
                if (menuItem.Tag is LocationLinkKey linkKey)
                {
                    Store.LocationLinks.DeleteLink(linkKey.A, linkKey.B);
                }
            }
        }
        public ContextMenuStrip BuildMenuFor(object Obj, ContextMenuStrip menu)
        {
            if (menu is null)
                return null;

            if (Obj is LocationLinkView link)
            {
                menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem menuDelete = new("Delete Link")
                {
                    Tag = link.Key
                };
                menuDelete.Click += ContextMenu_OnDelete;

                menu.Items.Add(menuDelete);

                menu.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem menuSplit = new("Split structure")
                {
                    Tag = link.Key
                };
                menuSplit.Click += ContextMenu_OnSplit;
                menu.Items.Add(menuSplit);

                return menu;
            }

            return menu;
        }

        public ContextMenuStrip BuildMenuFor(Type ObjType, ContextMenuStrip Menu) => Menu;
    }
}

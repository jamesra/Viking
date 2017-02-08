using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using connectomes.utah.edu.XSD.BookmarkSchema.xsd;
using Viking.Common;

namespace LocalBookmarks
{
    [Viking.Common.MenuAttribute("Bookmarks")]
    internal class BookMarkMenuFactory : Viking.Common.IMenuFactory
    {
        private static readonly string ShowBookmarksString = "Show Bookmarks";
        private static readonly string HideBookmarksString = "Hide Bookmarks";

        #region IMenuFactory Members

        System.Windows.Forms.ToolStripItem Viking.Common.IMenuFactory.CreateMenuItem()
        {
            //Create a menu containing each of our bookmarks
            ToolStripMenuItem Parent = new ToolStripMenuItem("Bookmarks");

            //Create the option to hide bookmarks on the display
            ToolStripMenuItem HideBookmarksMenu = new ToolStripMenuItem(Global.BookmarksVisible ? HideBookmarksString : ShowBookmarksString);
            HideBookmarksMenu.Click += OnHideBookmarksClick;

            ToolStripMenuItem UndoBookmarkMenu = new ToolStripMenuItem("Undo Bookmark Change");
            UndoBookmarkMenu.Click += OnUndoBookmarkChange; 
            
            Parent.DropDownItems.Add(HideBookmarksMenu);
            Parent.DropDownItems.Add(UndoBookmarkMenu); 

            return Parent as ToolStripItem; 
        }

        static void OnHideBookmarksClick(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                Global.BookmarksVisible = !Global.BookmarksVisible;

                menuItem.Text = Global.BookmarksVisible ? HideBookmarksString : ShowBookmarksString;
                Viking.UI.State.InvalidateViewerControl();
            }
        }

        static void OnUndoBookmarkChange(object sender, EventArgs e)
        {
            Global.Undo(); 
        }
        
        #endregion

    }
}

using System;
using System.Windows.Forms;

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
            ToolStripMenuItem Parent = new("Bookmarks");

            //Create the option to hide bookmarks on the display
            ToolStripMenuItem HideBookmarksMenu = new(Global.BookmarksVisible ? HideBookmarksString : ShowBookmarksString);
            HideBookmarksMenu.Click += OnHideBookmarksClick;

            ToolStripMenuItem UndoBookmarkMenu = new("Undo Bookmark Change");
            UndoBookmarkMenu.Click += OnUndoBookmarkChange;

            Parent.DropDownItems.Add(HideBookmarksMenu);
            Parent.DropDownItems.Add(UndoBookmarkMenu);

            return Parent as ToolStripItem;
        }

        static void OnHideBookmarksClick(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem)
            {
                Global.BookmarksVisible = !Global.BookmarksVisible;

                menuItem.Text = Global.BookmarksVisible ? HideBookmarksString : ShowBookmarksString;
                Viking.UI.State.InvalidateViewerControl();
            }
        }

        static void OnUndoBookmarkChange(object sender, EventArgs e) => Global.Undo();

        #endregion

    }
}

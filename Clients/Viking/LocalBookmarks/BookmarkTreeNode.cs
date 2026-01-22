namespace LocalBookmarks
{
    class BookmarkTreeNode(BookmarkUIObj folder) : Viking.UI.Controls.GenericTreeNode(folder)
    {
        public BookmarkUIObj? bookmark => this.Tag as BookmarkUIObj;

        public override void OnDoubleClick()
        {
            if (bookmark != null)
                Viking.UI.State.ViewerControl.GoToLocation(new Microsoft.Xna.Framework.Vector2(
                                                                      (float)bookmark.X, (float)bookmark.Y)
                                                                      , bookmark.Z, bookmark.Downsample);
        }
    }
}

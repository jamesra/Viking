namespace LocalBookmarks
{
    class BookmarkTreeNode : Viking.UI.Controls.GenericTreeNode
    {
        public BookmarkUIObj bookmark
        {
            get
            {
                return this.Tag as BookmarkUIObj;
            }
        }


        public BookmarkTreeNode(BookmarkUIObj folder)
            : base(folder)
        {
        }

        public override void OnDoubleClick()
        {
            Viking.UI.State.ViewerControl.GoToLocation(new Microsoft.Xna.Framework.Vector2(
                                                                      (float)bookmark.X, (float)bookmark.Y)
                                                                      , bookmark.Z, bookmark.Downsample);
        }
    }
}

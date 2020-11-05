namespace LocalBookmarks
{
    [Viking.Common.PropertyPage(typeof(BookmarkUIObj))]
    public partial class BookmarkGeneralPage : Viking.UI.BaseClasses.PropertyPageBase
    {
        BookmarkUIObj bookmark;

        public BookmarkGeneralPage()
        {
            InitializeComponent();

            textName.Focus();
        }

        protected override void OnShowObject(object Object)
        {
            bookmark = Object as BookmarkUIObj;
            textName.Text = bookmark.Name;
            richComment.Text = bookmark.Comment;
        }

        protected override void OnInitPage()
        {
            textName.Text = bookmark.Name;
            richComment.Text = bookmark.Comment;
        }

        protected override void OnSaveChanges()
        {
            bookmark.Name = textName.Text;
            bookmark.Comment = richComment.Text;
        }

        protected override void OnCancelChanges()
        {
            return;
        }
    }
}

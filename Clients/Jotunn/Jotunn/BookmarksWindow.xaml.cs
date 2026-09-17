using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Jotunn
{
    public partial class BookmarksWindow : Window
    {
        readonly string _volumeName;
        readonly List<BookmarkEntry> _bookmarks;

        public BookmarksWindow(string volumeName, List<BookmarkEntry> bookmarks)
        {
            InitializeComponent();
            _volumeName = volumeName;
            _bookmarks = bookmarks;
            BookmarkList.ItemsSource = _bookmarks;
        }

        public BookmarkEntry Selected => BookmarkList.SelectedItem as BookmarkEntry;

        public event System.Action<BookmarkEntry> GoToBookmark;

        public event System.Func<BookmarkEntry> RequestCurrentView;

        void OnGo(object sender, RoutedEventArgs e) => Go();

        void OnGo(object sender, MouseButtonEventArgs e) => Go();

        void Go()
        {
            if (Selected != null)
                GoToBookmark?.Invoke(Selected);
        }

        void OnAdd(object sender, RoutedEventArgs e)
        {
            BookmarkEntry entry = RequestCurrentView?.Invoke();
            if (entry == null)
                return;
            _bookmarks.Add(entry);
            BookmarkList.Items.Refresh();
            BookmarkStore.Save(_volumeName, _bookmarks);
        }

        void OnDelete(object sender, RoutedEventArgs e)
        {
            if (Selected == null)
                return;
            _bookmarks.Remove(Selected);
            BookmarkList.Items.Refresh();
            BookmarkStore.Save(_volumeName, _bookmarks);
        }
    }
}

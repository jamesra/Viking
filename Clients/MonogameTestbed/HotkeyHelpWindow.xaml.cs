using System.Collections.Generic;
using System.Windows;

namespace MonogameTestbed
{
    public partial class HotkeyHelpWindow : Window
    {
        public HotkeyHelpWindow(string testTitle, IReadOnlyList<HotkeyHelpSection> sections)
        {
            InitializeComponent();
            Title = $"MonogameTestbed — {testTitle}";
            SubtitleText.Text = $"Keyboard shortcuts for {testTitle}. Global keys switch tests from any mode.";
            SectionsList.ItemsSource = sections;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}

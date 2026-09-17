using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viking;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;
using SectionVM = Viking.VolumeViewModel.SectionViewModel;

namespace Viking.VolumeView
{
    /// <summary>
    /// Sections tab. The root is already a TabItem — add this instance to TabControl.Items.
    /// Wrapping it in another TabItem hides the DataGrid (nested TabItem as content).
    /// </summary>
    public partial class SectionList : TabItem
    {
        public SectionList()
        {
            InitializeComponent();
            DataContextChanged += OnVolumeDataContextChanged;
        }

        private void OnVolumeDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            VolumeVM volume = e.NewValue as VolumeVM;
            if (volume != null)
                SectionsList.ItemsSource = volume.SectionViewModels.Values;
        }

        /// <summary>
        /// Jump the volume view to the double-clicked section, matching Viking's section list.
        /// Channel ComboBox clicks are ignored so channel changes do not retarget the view.
        /// </summary>
        void OnSectionRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<ComboBox>(e.OriginalSource as DependencyObject) != null)
                return;

            DataGridRow row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            SectionVM section = row?.Item as SectionVM ?? SectionsList.SelectedItem as SectionVM;
            if (section == null)
                return;

            VolumeVM volume = DataContext as VolumeVM;
            if (volume == null || !volume.SectionViewModels.ContainsKey(section.Number))
                return;

            volume.CenterIndex = volume.SectionViewModels.IndexOfKey(section.Number);
        }

        void OnViewSettingChanged(object sender, SelectionChangedEventArgs e)
        {
            TileLoadEnvironment.RequestRender?.Invoke();
        }

        static T FindAncestor<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                    return match;
                source = VisualTreeHelper.GetParent(source);
            }
            return null;
        }
    }
}

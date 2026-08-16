using System.Windows;
using System.Windows.Controls;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

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
    }
}

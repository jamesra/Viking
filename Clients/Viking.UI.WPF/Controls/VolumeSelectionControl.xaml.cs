using System.Windows;
using System.Windows.Controls;
using Viking.UI.WPF.Models;
using Viking.UI.WPF.ViewModels;

namespace Viking.UI.WPF.Controls
{
    public partial class VolumeSelectionControl : UserControl
    {
        public VolumeSelectionControl()
        {
            InitializeComponent();
        }

        private void treeVolumes_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is VolumeSelectionViewModel viewModel && e.NewValue is VolumeTreeNode node)
            {
                viewModel.SelectedVolume = node;
            }
        }

        private void lstRecentVolumes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is VolumeSelectionViewModel viewModel && lstRecentVolumes.SelectedItem is VolumeInfo volumeInfo)
            {
                // Create a tree node from the recent volume for selection
                var node = new VolumeTreeNode
                {
                    Volume = volumeInfo,
                    Name = volumeInfo.Name,
                    IsOrganization = false
                };
                viewModel.SelectedVolume = node;
            }
        }

        private void treeVolumes_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is VolumeSelectionViewModel viewModel && viewModel.SelectedVolume != null)
            {
                // Only trigger selection if it's a volume node (not an organization node)
                if (!viewModel.SelectedVolume.IsOrganization && viewModel.SelectedVolume.Volume != null)
                {
                    // Execute the SelectCommand to trigger the volume selection
                    if (viewModel.SelectCommand.CanExecute(null))
                    {
                        viewModel.SelectCommand.Execute(null);
                    }
                }
            }
        }

        private void lstRecentVolumes_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is VolumeSelectionViewModel viewModel && viewModel.SelectedVolume != null)
            {
                // Execute the SelectCommand to trigger the volume selection
                if (viewModel.SelectCommand.CanExecute(null))
                {
                    viewModel.SelectCommand.Execute(null);
                }
            }
        }
    }
}


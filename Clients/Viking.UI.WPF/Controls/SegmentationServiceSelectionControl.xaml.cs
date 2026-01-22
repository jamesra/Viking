using System.Windows;
using System.Windows.Controls;
using Viking.UI.WPF.Models;
using Viking.UI.WPF.ViewModels;

namespace Viking.UI.WPF.Controls
{
    public partial class SegmentationServiceSelectionControl : UserControl
    {
        public SegmentationServiceSelectionControl()
        {
            InitializeComponent();
        }

        private void treeServices_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is SegmentationServiceSelectionViewModel viewModel)
            {
                if (e.NewValue is SegmentationServiceTreeNode node)
                {
                    // Only set SelectedService if the node has a Service property (not a category node)
                    if (node.Service != null)
                    {
                        viewModel.SelectedService = node;
                    }
                    else
                    {
                        // Clear selection if a category node is selected
                        viewModel.SelectedService = null;
                    }
                }
                else if (e.NewValue is null)
                {
                    // Clear selection when deselected
                    viewModel.SelectedService = null;
                }
            }
        }

        private void lstRecentServices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SegmentationServiceSelectionViewModel viewModel && lstRecentServices.SelectedItem is SegmentationServiceInfo serviceInfo)
            {
                SegmentationServiceTreeNode node = new()
                {
                    Service = serviceInfo,
                    Name = serviceInfo.Name,
                    IsCategory = false
                };

                viewModel.SelectedService = node;
            }
        }

        private void treeServices_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is SegmentationServiceSelectionViewModel viewModel && viewModel.SelectedService != null)
            {
                if (!viewModel.SelectedService.IsCategory && viewModel.SelectedService.Service != null)
                {
                    if (viewModel.SelectCommand.CanExecute(null))
                    {
                        viewModel.SelectCommand.Execute(null);
                    }
                }
            }
        }

        private void lstRecentServices_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is SegmentationServiceSelectionViewModel viewModel && viewModel.SelectedService != null)
            {
                if (viewModel.SelectCommand.CanExecute(null))
                {
                    viewModel.SelectCommand.Execute(null);
                }
            }
        }
    }
}



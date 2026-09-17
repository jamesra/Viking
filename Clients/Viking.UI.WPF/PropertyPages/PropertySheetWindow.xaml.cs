using System;
using System.Windows;

namespace Viking.UI.WPF.PropertyPages
{
    public partial class PropertySheetWindow : Window
    {
        public PropertySheetWindow()
        {
            InitializeComponent();
        }

        private PropertySheetViewModel ViewModel => (PropertySheetViewModel)DataContext;

        public void Initialize(object target)
        {
            ViewModel.Initialize(target);
            Title = $"{target} Properties";
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            if (!ApplyInternal())
            {
                return;
            }

            DialogResult = true;
            Close();
        }

        private void ApplyClick(object sender, RoutedEventArgs e) => ApplyInternal();

        private bool ApplyInternal()
        {
            try
            {
                if (!ViewModel.ValidateAll())
                {
                    return false;
                }

                ViewModel.SaveAll();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Unable to save changes:\n{ex.Message}",
                    "Property Sheet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            ViewModel.CancelAll();
            DialogResult = false;
            Close();
        }
    }
}


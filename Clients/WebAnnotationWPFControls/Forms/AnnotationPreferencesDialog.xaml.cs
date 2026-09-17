using System;
using System.Windows;

namespace WebAnnotation.WPF.Forms
{
    /// <summary>
    /// Interaction logic for AnnotationPreferencesDialog.xaml
    /// </summary>
    public partial class AnnotationPreferencesDialog : Window
    {
        public AnnotationPreferencesDialogViewModel ViewModel { get; private set; }
        public bool IsClosed { get; private set; }

        // Event to notify when Apply is clicked
        public event EventHandler ApplyClicked;
        public event EventHandler OkClicked;
        public event EventHandler CancelClicked;

        /// <summary>
        /// Raised when the user confirms Reset to Defaults. The subscriber should call Global.AnnotationSettings.ResetToDefaults() and then reload the ViewModel from Global.
        /// </summary>
        public event EventHandler ResetToDefaultsRequested;

        public AnnotationPreferencesDialog()
        {
            InitializeComponent();
            ViewModel = new AnnotationPreferencesDialogViewModel();
            DataContext = ViewModel;
            IsClosed = false;
        }

        public AnnotationPreferencesDialog(AnnotationPreferencesDialogViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
            IsClosed = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsClosed = true;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyClicked?.Invoke(this, EventArgs.Empty);
            ViewModel.Apply();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            OkClicked?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RevertToOriginal();
            CancelClicked?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void ResetToDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "Reset all annotation settings to their default values?",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ResetToDefaultsRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}


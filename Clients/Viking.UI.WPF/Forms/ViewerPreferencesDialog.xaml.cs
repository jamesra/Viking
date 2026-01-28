using System;
using System.Windows;

namespace Viking.UI.WPF.Forms
{
    /// <summary>
    /// Interaction logic for ViewerPreferencesDialog.xaml
    /// </summary>
    public partial class ViewerPreferencesDialog : Window
    {
        public ViewerPreferencesDialogViewModel ViewModel { get; private set; }
        public bool IsClosed { get; private set; }

        /// <summary>
        /// Event to notify when Apply is clicked
        /// </summary>
        public event EventHandler ApplyClicked;

        /// <summary>
        /// Event to notify when OK is clicked
        /// </summary>
        public event EventHandler OkClicked;

        /// <summary>
        /// Event to notify when Cancel is clicked
        /// </summary>
        public event EventHandler CancelClicked;

        public ViewerPreferencesDialog()
        {
            InitializeComponent();
            ViewModel = new ViewerPreferencesDialogViewModel();
            DataContext = ViewModel;
            IsClosed = false;
        }

        public ViewerPreferencesDialog(ViewerPreferencesDialogViewModel viewModel)
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
    }
}

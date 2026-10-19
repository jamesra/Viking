using System;
using System.ComponentModel;
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
        /// Set once OK or Cancel has decided how this dialog ends, so closing the window afterwards does not
        /// run the Cancel path again. Apply leaves it unset: the dialog stays open and can still be cancelled.
        /// </summary>
        private bool _resolved;

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

        /// <summary>
        /// Closing the window without OK or Cancel (title bar X, Alt+F4, or the app closing it) cancels, so the live
        /// preview is reverted to the values from opening or the last Apply, exactly as the Cancel button does.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
                CancelOnce();
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
            _resolved = true;
            OkClicked?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelOnce();
            Close();
        }

        /// <summary>
        /// Reverts the view model to its last snapshot and raises <see cref="CancelClicked"/> so the host restores
        /// its live preview. Runs at most once per dialog; does nothing after OK.
        /// </summary>
        private void CancelOnce()
        {
            if (_resolved)
                return;

            _resolved = true;
            ViewModel.RevertToOriginal();
            CancelClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}

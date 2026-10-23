using System;
using System.ComponentModel;
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

        /// <summary>
        /// Set once OK or Cancel has decided how this dialog ends, so closing the window afterwards does not
        /// run the Cancel path again. Apply leaves it unset: the dialog stays open and can still be cancelled.
        /// </summary>
        private bool _resolved;

        // Event to notify when Apply is clicked
        public event EventHandler ApplyClicked;
        public event EventHandler OkClicked;
        public event EventHandler CancelClicked;

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

        private bool _capturingBarrelButton;

        /// <summary>Arms the capture: the next pen button pressed over the button becomes the barrel button.</summary>
        private void CaptureBarrelButton_Click(object sender, RoutedEventArgs e)
        {
            _capturingBarrelButton = true;
            CaptureBarrelButton.Content = "Press the pen button now...";
        }

        /// <summary>Takes the pen button that was pressed while capturing as the barrel button.</summary>
        private void CaptureBarrelButton_StylusButtonDown(object sender, System.Windows.Input.StylusButtonEventArgs e)
        {
            if (!_capturingBarrelButton)
                return;

            string name = StylusButtonCapture.NameFor(e.StylusButton.Guid);
            if (name is null)
                return;

            ViewModel.BarrelButton = name;
            _capturingBarrelButton = false;
            CaptureBarrelButton.Content = "Click your pen button...";
            e.Handled = true;
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


#nullable enable
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using Duende.IdentityModel.Client;
using Viking.UI.WPF.ViewModels;

namespace Viking.UI.WPF
{
    /// <summary>
    /// Modal host for the login-stage segmentation picker so Annotation can re-select a server mid-session.
    /// Select and None set <see cref="DialogResult"/> true; Back or the window close box leaves it false.
    /// </summary>
    public partial class SegmentationServiceSelectionWindow : Window
    {
        private readonly SegmentationServiceSelectionViewModel _viewModel;

        /// <summary>
        /// Endpoint chosen with Select, or null when the user chose None.
        /// Meaningful only when <see cref="Window.DialogResult"/> is true.
        /// </summary>
        public string? SelectedEndpoint { get; private set; }

        public SegmentationServiceSelectionWindow(
            TokenResponse bearerToken,
            string identityServerUrl,
            string currentEndpoint,
            IEnumerable<string> recentEndpoints)
        {
            InitializeComponent();

            _viewModel = new SegmentationServiceSelectionViewModel(
                bearerToken,
                identityServerUrl,
                currentEndpoint,
                preloadedServices: null);

            if (recentEndpoints != null)
            {
                foreach (string endpoint in recentEndpoints)
                    _viewModel.AddRecentService(endpoint, null);
            }

            if (!string.IsNullOrWhiteSpace(currentEndpoint))
                _viewModel.PreselectService(currentEndpoint);
            else
                _viewModel.SelectMostRecentServiceIfAvailable();

            _viewModel.SegmentationServiceSelected += OnServiceSelected;
            _viewModel.SegmentationSelectionSkipped += OnSelectionSkipped;
            _viewModel.SelectionCancelled += OnSelectionCancelled;
            Closed += OnWindowClosed;

            segmentationSelectionControl.DataContext = _viewModel;
        }

        /// <summary>
        /// Shows the picker owned by a WinForms handle. True means Select or None; false means cancel.
        /// </summary>
        public static bool? ShowDialog(
            IntPtr ownerHandle,
            TokenResponse bearerToken,
            string identityServerUrl,
            string currentEndpoint,
            IEnumerable<string> recentEndpoints,
            out string? selectedEndpoint)
        {
            SegmentationServiceSelectionWindow window = new(
                bearerToken,
                identityServerUrl,
                currentEndpoint,
                recentEndpoints);

            if (ownerHandle != IntPtr.Zero)
                new WindowInteropHelper(window).Owner = ownerHandle;

            bool? result = window.ShowDialog();
            selectedEndpoint = result == true ? window.SelectedEndpoint : null;
            return result;
        }

        private void OnServiceSelected(object sender, SegmentationServiceSelectedEventArgs e)
        {
            SelectedEndpoint = e.Endpoint;
            DialogResult = true;
        }

        private void OnSelectionSkipped(object sender, EventArgs e)
        {
            SelectedEndpoint = null;
            DialogResult = true;
        }

        private void OnSelectionCancelled(object sender, EventArgs e)
        {
            DialogResult = false;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            _viewModel.SegmentationServiceSelected -= OnServiceSelected;
            _viewModel.SegmentationSelectionSkipped -= OnSelectionSkipped;
            _viewModel.SelectionCancelled -= OnSelectionCancelled;
            Closed -= OnWindowClosed;
        }
    }
}

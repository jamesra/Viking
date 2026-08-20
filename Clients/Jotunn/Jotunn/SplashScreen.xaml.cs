using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Viking.Common;

namespace Jotunn
{
    public partial class SplashScreen : Window
    {
        /// <summary>
        /// Set by App.OnStartup so Cancel stops volume and annotation load instead of racing Shutdown.
        /// </summary>
        public CancellationTokenSource LoadCancellation { get; set; }

        public SplashScreen()
        {
            InitializeComponent();
        }

        public void Report(ProgressInfo info)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Report(info)));
                return;
            }

            if (info.MaxProgress > 0)
                progressBar.Value = (info.Progress / info.MaxProgress) * 100.0;
            else
                progressBar.Value = info.Progress;

            TextProgress.Text = info.Message ?? string.Empty;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            LoadCancellation?.Cancel();
            Close();
        }

        private void OnChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                return;

            if (buttonCancel.IsMouseOver)
                return;

            DragMove();
        }
    }
}

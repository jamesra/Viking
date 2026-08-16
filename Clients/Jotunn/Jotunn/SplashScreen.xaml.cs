using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Viking.Common;

namespace Jotunn
{
    public partial class SplashScreen : Window
    {
        public BackgroundWorker InitializeWorker;

        public SplashScreen()
        {
            InitializeComponent();
            InitializeWorker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            InitializeWorker.ProgressChanged += InitializeWorker_ProgressChanged;
        }

        public void Report(ProgressInfo info)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Report(info));
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
            Close();
            Application.Current.Shutdown();
        }

        private void InitializeWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
            TextProgress.Text = e.UserState as string ?? string.Empty;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}

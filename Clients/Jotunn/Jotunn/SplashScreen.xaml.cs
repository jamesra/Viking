using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;

namespace Jotunn
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        [Export("InitializeBackgroundWorker")]
        public System.ComponentModel.BackgroundWorker InitializeWorker = null;

        public SplashScreen()
        {
            InitializeComponent();
            InitializeWorker = new System.ComponentModel.BackgroundWorker();
            InitializeWorker.WorkerReportsProgress = true;
            InitializeWorker.WorkerSupportsCancellation = true;
            InitializeWorker.ProgressChanged += new ProgressChangedEventHandler(InitializeWorker_ProgressChanged);
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        private void InitializeWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.progressBar.Value = e.ProgressPercentage;
            string updateString = e.UserState as string;
            if (updateString == null)
                updateString = "";

            TextProgress.Text = updateString;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
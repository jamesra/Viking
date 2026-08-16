using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows;
using Viking;
using Viking.Common;
using Viking.UI.WPF;
using Viking.VolumeModel;
using VolumeGlobal = Viking.VolumeViewModel.Global;
using VolumeVM = Viking.VolumeViewModel.VolumeViewModel;

namespace Jotunn
{
    public partial class App : Application
    {
        public static string SegmentationServiceUrl { get; private set; }

        public static NetworkCredential UserCredentials { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Global.Initialize();

            LoginWindow login = new LoginWindow
            {
                InitialVolumeUrl = ShellParameterService.FirstVolumeUrlFromArgs(e.Args)
                    ?? ShellParameterService.DefaultVolumeUrl
            };

            bool? loginResult = login.ShowDialog();
            if (loginResult != true)
            {
                Shutdown();
                return;
            }

            UserCredentials = login.Credentials;
            SegmentationServiceUrl = login.SegmentationServiceUrl;

            SplashScreen splash = new SplashScreen();
            splash.Show();

            try
            {
                ShellParameterService shellParameters = ShellParameterService.FromVolumeUrl(login.VolumeURL);
                Progress<ProgressInfo> progress = new Progress<ProgressInfo>(info => splash.Report(info));

                Volume volume = new Volume(shellParameters.HostPath, VolumeGlobal.CachePath, shellParameters.Xml, progress);
                if (login.Credentials != null)
                    volume.UserCredentials = login.Credentials;

                await volume.Initialize(CancellationToken.None, progress).ConfigureAwait(true);

                TileLoadEnvironment.BindVolume(volume);
                VolumeGlobal.Volume = volume;
                VolumeVM volumeViewModel = new VolumeVM(volume);

                MainWindow mainWindow = new MainWindow();
                mainWindow.AttachVolume(volumeViewModel);
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                splash.Close();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                splash.Close();
                Trace.WriteLine(ex);
                MessageBox.Show(ex.Message, "Jotunn", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

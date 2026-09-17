using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows;
using Viking;
using Viking.Common;
using Viking.Tokens;
using Viking.UI.WPF;
using Viking.VolumeModel;
using WebAnnotationModel;
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
            };

            bool? loginResult = login.ShowDialog();
            if (loginResult != true)
            {
                Shutdown();
                return;
            }

            UserCredentials = login.Credentials;
            SegmentationServiceUrl = login.SegmentationServiceUrl;
            if (login.BearerToken != null)
            {
                TokenStore.BearerToken = login.BearerToken;
                if (!string.IsNullOrEmpty(login.IdentityServerUrl))
                    TokenStore.BearerTokenAuthority = login.IdentityServerUrl;
            }

            CancellationTokenSource loadCts = new CancellationTokenSource();
            SplashScreen splash = new SplashScreen { LoadCancellation = loadCts };
            splash.Show();

            try
            {
                Progress<ProgressInfo> progressReporter = new Progress<ProgressInfo>(info => splash.Report(info));
                IProgress<ProgressInfo> progress = progressReporter;
                ShellParameterService shellParameters = await ShellParameterService.FromVolumeUrlAsync(
                    login.VolumeURL,
                    login.Credentials,
                    loadCts.Token,
                    progress).ConfigureAwait(true);
                if (loadCts.IsCancellationRequested)
                {
                    Shutdown();
                    return;
                }

                Volume volume = new Volume(shellParameters.HostPath, VolumeGlobal.CachePath, shellParameters.Xml, progress);
                if (login.Credentials != null)
                    volume.UserCredentials = login.Credentials;

                await volume.Initialize(loadCts.Token, progress).ConfigureAwait(true);
                if (loadCts.IsCancellationRequested)
                {
                    Shutdown();
                    return;
                }

                TileLoadEnvironment.UiDispatcher = Dispatcher;
                TileLoadEnvironment.BindVolume(volume);
                TileLoadEnvironment.StartTexturePipeline();
                VolumeGlobal.Volume = volume;

                VolumeVM volumeViewModel = new VolumeVM(volume);
                MainWindow mainWindow = new MainWindow();
                mainWindow.AttachVolume(volumeViewModel);
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                splash.Close();
                mainWindow.Show();

                bool annotationsReady = await WebAnnotation.AnnotationBootstrap.TryInitializeAsync(
                    volume,
                    UserCredentials,
                    SegmentationServiceUrl,
                    loadCts.Token).ConfigureAwait(true);
                if (loadCts.IsCancellationRequested)
                {
                    Shutdown();
                    return;
                }

                if (!annotationsReady || !Store.IsInitialized)
                {
                    MessageBox.Show(
                        "Annotations could not be loaded. The volume will open without annotation tools.",
                        "Jotunn",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    mainWindow.TryAttachAnnotations();
                }
            }
            catch (OperationCanceledException)
            {
                Shutdown();
            }
            catch (Exception ex)
            {
                if (splash.IsVisible)
                    splash.Close();
                Trace.WriteLine(ex);
                MessageBox.Show(ex.Message, "Jotunn", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

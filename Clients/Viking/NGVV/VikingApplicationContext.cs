using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using Viking.UI.Forms;
using Microsoft.Xna.Framework;

namespace Viking
{
    /// <summary>
    /// Application context that shows the splash screen, initializes caches and modules, and then shows main Viking form
    /// </summary>

    public class VikingApplicationContext : ApplicationContext
    {
        private readonly ApplicationSettings _settings;

        public VikingApplicationContext(ApplicationSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(_settings.VolumeURL))
            {
                throw new ArgumentException("VolumeURL must be provided", nameof(settings));
            }

            UI.State.MainThreadDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            PreventWpfLastWindowFromShuttingDownDispatcher();
        }

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(_settings.VolumeURL))
                throw new ArgumentNullException(nameof(_settings.VolumeURL));
            using SplashForm Splash = new();
            Splash.TrackedTask = System.Threading.Tasks.Task.Run(() => BackgroundLoading(_settings.VolumeURL, Splash.progressReporter, ApplicationShutdown.Token));

            //The splash dialog will run until the Volume is initialized 
            Splash.ShowDialog();

            DialogResult splashResult = Splash.Result;

            Splash.Close();

            if (splashResult == DialogResult.Cancel)
            {
                Trace.WriteLine($"Viking launch cancelled by user");
                ExitThread();
                return;
            }

            if (Splash.TrackedTask.IsFaulted)
            {
                Trace.WriteLine($"Viking launch cancelled after exception:\n {Splash.TrackedTask.Exception}");

                // Unwrap to the innermost exception for the clearest message.
                Exception? rootCause = Splash.TrackedTask.Exception?.Flatten().InnerException
                                       ?? Splash.TrackedTask.Exception;

                string friendlyMessage =
                    $"Could not load the volume from:\n{_settings.VolumeURL}\n\n" +
                    $"Reason: {rootCause?.Message ?? "Unknown error"}\n\n" +
                    "Please check that the server is reachable and the URL is correct.";

                MessageBox.Show(friendlyMessage, "Volume Load Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitThread();
                return;
            }

            var stosFailures = UI.State.volume?.StosLoadFailures;
            if (stosFailures is { Count: > 0 })
            {
                Trace.WriteLine($"{stosFailures.Count} .stos transforms did not load:" + Environment.NewLine + string.Join(Environment.NewLine, stosFailures));
                MessageBox.Show(
                    $"{stosFailures.Count} section transforms (.stos) could not be loaded from the server, so those sections " +
                    "are shown without their transform. The list is in the Viking log. Reopening the volume usually fixes it." + Environment.NewLine + Environment.NewLine +
                    $"First: {stosFailures[0]}",
                    "Transforms Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Trace.WriteLine($"Showing VikingMain window");
            UI.State.Appwindow = new VikingMain();
            this.MainForm = UI.State.Appwindow;
            this.MainForm.Show();
        }

        protected override void OnMainFormClosed(object sender, EventArgs e)
        {
            // The window is already gone (ApplicationContext reacts to HandleDestroyed), so arm the
            // watchdog before teardown that can block: cancellation callbacks, and closing the viewer or a
            // WPF window, which runs their handlers.
            StartExitWatchdog();

            // Cancels in-flight gRPC calls so the channel shutdown after the message loop does not wait on them.
            ApplicationShutdown.Begin();
            ShutdownRuntime();
            base.OnMainFormClosed(sender, e);
        }

        /// <summary>
        /// Login and preferences are WPF. Default <see cref="System.Windows.ShutdownMode.OnLastWindowClose"/>
        /// would shut down the shared dispatcher when those windows close, which can deadlock
        /// WinForms <c>FormClosed</c> if we also call <c>Application.Shutdown()</c>.
        /// </summary>
        private static void PreventWpfLastWindowFromShuttingDownDispatcher()
        {
            var app = System.Windows.Application.Current;
            if (app is null)
                return;

            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        }

        /// <summary>
        /// Stops timers, texture workers, and leftover WPF windows so Application.Run can return.
        /// Must not call <c>Application.Current.Shutdown()</c> here: that Invoke-shuts the dispatcher
        /// on this same thread and deadlocks when a WPF window is still open.
        /// </summary>
        internal static void ShutdownRuntime()
        {
            PreventWpfLastWindowFromShuttingDownDispatcher();

            try
            {
                PendingTextureQueue.Stop();
                TextureRequestQueue.StopWorkers();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error stopping texture queues: {ex.Message}");
            }

            try
            {
                Global.HttpClient.CancelPendingRequests();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error cancelling HTTP requests: {ex.Message}");
            }

            try
            {
                UI.State.ViewerForm?.Close();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error closing viewer form: {ex.Message}");
            }

            CloseRemainingWpfWindows();

            var dispatcher = UI.State.MainThreadDispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                try
                {
                    dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error shutting down dispatcher: {ex.Message}");
                }
            }
        }

        private static void CloseRemainingWpfWindows()
        {
            var app = System.Windows.Application.Current;
            if (app is null)
                return;

            System.Windows.Window[] windows;
            try
            {
                windows = [.. app.Windows.Cast<System.Windows.Window>()];
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error listing WPF windows: {ex.Message}");
                return;
            }

            foreach (System.Windows.Window window in windows)
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Error closing WPF window: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// If teardown has not finished 3 seconds after the main window is destroyed, write an
        /// <see cref="ExitStallDump"/> and kill the process. Covers <see cref="ShutdownRuntime"/>, a WinForms loop that
        /// never returns (dispatcher or gRPC still pumping), and <see cref="ShutdownAfterMessageLoop"/>. Uses
        /// <see cref="ProcessTermination"/>, not <see cref="Environment.Exit(int)"/>: Exit runs Grpc.Core's ProcessExit
        /// wait and stalled here. Background so a clean exit does not wait out the delay.
        /// </summary>
        private static void StartExitWatchdog()
        {
            Thread watchdog = new(() =>
            {
                Thread.Sleep(3000);
                TryTrace("Viking exit watchdog: teardown did not finish in 3 s; writing exit-stall dump");
                string? dumpPath = ExitStallDump.TryCapture();
                TryTrace(dumpPath is null
                    ? "Viking exit watchdog: no dump written; terminating process"
                    : $"Viking exit watchdog: dump written to {dumpPath}; terminating process");
                ProcessTermination.Terminate(0);
            })
            {
                IsBackground = true,
                Name = "VikingExitWatchdog"
            };
            watchdog.Start();
        }

        /// <summary>
        /// Writes a trace line from the exit watchdog. Main may have closed the log writer just before its own
        /// termination, so a write failure is ignored.
        /// </summary>
        private static void TryTrace(string message)
        {
            try
            {
                Trace.WriteLine(message);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Tears down DI and Grpc.Core native threads after the WinForms message loop has exited.
        /// Do not call this from the UI thread during FormClosed — channel shutdown Waits.
        /// </summary>
        public static void ShutdownAfterMessageLoop()
        {
            try
            {
                if (ServiceLocator.IsInitialized)
                    ServiceLocator.Reset();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error resetting ServiceLocator: {ex.Message}");
            }

            GrpcChannelManager.ShutdownEnvironment();
        }

        private async Task BackgroundLoading(string VolumeURL, Viking.Common.IProgressReporter progressReporter, CancellationToken token)
        {
            if (VolumeURL is null)
                throw new ArgumentNullException(nameof(VolumeURL));

            DateTime startVolume = DateTime.UtcNow;
            //The constructor populates attributes of the volume element.  Then initialize needs to be called to collect more
            var Volume = await Viking.VolumeModel.Volume.CreateAsync(VolumeURL, UI.State.CachePath, progressReporter, token);
            //new Viking.VolumeModel.Volume(VolumeURL, UI.State.CachePath, progressReporter);

            //Start loading textures, this does not need to be done before launching the main app.
            DateTime TextureCacheLoadStart = DateTime.UtcNow;
            var textureCacheTask = Global.TextureCache.PopulateCache(UI.State.GetVolumeCachePath(Volume.Name), token);

            DateTime stopVolume = DateTime.UtcNow;
            var elapsedTime = stopVolume - startVolume;
            Trace.WriteLine("Volume Load Time: " + elapsedTime.ToString());

            await Volume.Initialize(token, progressReporter);
            TextureReaderV2.ApplyMaxConcurrentRequestPreference(
                Viking.Properties.Settings.Default.MaxConcurrentTextureRequests,
                Volume.DefaultTileWidth);

            UI.State.volume = new Viking.ViewModels.VolumeViewModel(Volume);

            DateTime startExtensions = DateTime.UtcNow;
            Viking.Common.ExtensionManager.LoadExtensions(progressReporter);
            DateTime stopExtensions = DateTime.UtcNow;
            var elapsedExtensionTime = stopExtensions - startExtensions;
            Trace.WriteLine("Extension Load Time: " + elapsedExtensionTime.ToString());

            ServiceCollection services = new();
            services.AddSingleton(_settings);
            services.AddSingleton(Volume);
            services.AddSingleton(UI.State.volume);

            Viking.Common.ExtensionManager.RegisterModuleServices(services);

            if (ServiceLocator.IsInitialized)
            {
                ServiceLocator.Reset();
            }

            var serviceProvider = services.BuildServiceProvider();
            ServiceLocator.Initialize(serviceProvider, services);

            await Viking.Common.ExtensionManager.InitializeModulesAsync(ServiceLocator.ServiceProvider, token).ConfigureAwait(false);

            await textureCacheTask;
            DateTime TextureCacheLoadStop = DateTime.UtcNow;
            var elapsedTextureCacheLoadTime = TextureCacheLoadStop - TextureCacheLoadStart;
            Trace.WriteLine("Texture cache load: " + elapsedTextureCacheLoadTime.ToString());
            return;
        }
    }
}

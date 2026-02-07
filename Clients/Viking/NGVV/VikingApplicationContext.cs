using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Viking.DependencyInjection;
using Viking.UI.Forms;
using Microsoft.Xna.Framework;

namespace Viking
{
    /// <summary>
    /// Application context that shows the splash screen, initializes caches and modules, and then shows main Viking form
    /// </summary>

    public class VikingApplicationContext : ApplicationContext
    {
        public CancellationTokenSource cancellationTokenSource = new();

        private readonly ApplicationSettings _settings;

        public VikingApplicationContext(ApplicationSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(_settings.VolumeURL))
            {
                throw new ArgumentException("VolumeURL must be provided", nameof(settings));
            }

            UI.State.MainThreadDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            //Microsoft.Xna.Framework.Content.RootDirectory = "Content";
        }

        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(_settings.VolumeURL))
                throw new ArgumentNullException(nameof(_settings.VolumeURL));
            //var cancellationTokenSource = new CancellationTokenSource();

            using SplashForm Splash = new();
            Splash.TrackedTask = System.Threading.Tasks.Task.Run(() => BackgroundLoading(_settings.VolumeURL, Splash.progressReporter, cancellationTokenSource.Token));

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
                MessageBox.Show($"Viking launch cancelled after exception:\n {Splash.TrackedTask.Exception}");
                ExitThread();
                return;
            }

            Trace.WriteLine($"Showing VikingMain window");
            UI.State.Appwindow = new VikingMain();
            this.MainForm = UI.State.Appwindow;
            this.MainForm.Show();
        }

        protected override void OnMainFormClosed(object sender, EventArgs e)
        {
            base.OnMainFormClosed(sender, e);
            Global.HttpClient.CancelPendingRequests();
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
            int pref = Viking.Properties.Settings.Default.MaxConcurrentTextureRequests;
            if (pref > 0)
                TextureReaderV2.SetMaxConcurrentRequestLimit(pref);
            else
                TextureReaderV2.SetMaxConcurrentRequestLimit(Viking.UI.WPF.Forms.ViewerPreferencesDialogViewModel.DefaultMaxConcurrentTextureRequests);

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

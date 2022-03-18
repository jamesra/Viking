﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Viking.UI.Forms;

namespace Viking
{
    /// <summary>
    /// Application context that shows the splash screen, initializes caches and modules, and then shows main Viking form
    /// </summary>

    public class VikingApplicationContext : ApplicationContext
    {
        public CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public VikingApplicationContext(string VolumeURL)
        {
            UI.State.MainThreadDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        }

        public void Initialize(string VolumeURL)
        {
            if (VolumeURL is null)
                throw new ArgumentNullException(nameof(VolumeURL));
            //var cancellationTokenSource = new CancellationTokenSource();

            using (SplashForm Splash = new SplashForm())
            {
                Splash.TrackedTask = System.Threading.Tasks.Task.Run(() => BackgroundLoading(VolumeURL, Splash.ProgressReporter, cancellationTokenSource.Token));

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
            }

            Trace.WriteLine($"Showing VikingMain window");
            UI.State.Appwindow = new VikingMain();
            this.MainForm = UI.State.Appwindow;
            this.MainForm.Show();
        }

        private async Task BackgroundLoading(string VolumeURL, Viking.Common.IProgressReporter progressReporter, CancellationToken token)
        {
            if (VolumeURL is null)
                throw new ArgumentNullException(nameof(VolumeURL));

            DateTime startVolume = DateTime.UtcNow;
            //The constructor populates attributes of the volume element.  Then initialize needs to be called to collect more
            var Volume = new Viking.VolumeModel.Volume(VolumeURL, UI.State.CachePath, progressReporter);
            
            //Start loading textures, this does not need to be done before launching the main app.
            DateTime TextureCacheLoadStart = DateTime.UtcNow;
            var textureCacheTask = Global.TextureCache.PopulateCache(UI.State.GetVolumeCachePath(Volume.Name), token);

            await Volume.Initialize(token, progressReporter);
            DateTime stopVolume = DateTime.UtcNow;
            var elapsedTime = stopVolume - startVolume;
            Trace.WriteLine("Volume Load Time: " + elapsedTime.ToString());

            UI.State.volume = new Viking.ViewModels.VolumeViewModel(Volume);

            DateTime startExtensions = DateTime.UtcNow;
            Viking.Common.ExtensionManager.LoadExtensions(progressReporter);
            DateTime stopExtensions = DateTime.UtcNow;
            var elapsedExtensionTime = stopExtensions - startExtensions;
            Trace.WriteLine("Extension Load Time: " + elapsedExtensionTime.ToString());

            await textureCacheTask;
            DateTime TextureCacheLoadStop = DateTime.UtcNow;
            var elapsedTextureCacheLoadTime = TextureCacheLoadStop - TextureCacheLoadStart;
            Trace.WriteLine("Texture cache load: " + elapsedTextureCacheLoadTime.ToString());
            return;
        }
    }
}

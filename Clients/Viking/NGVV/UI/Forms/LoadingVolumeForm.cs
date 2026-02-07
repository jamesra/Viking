using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Viking.Common;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Viking.ViewModels;
using Viking.VolumeModel;
using VolumeModel;

namespace Viking.UI.Forms
{
    public partial class LoadingVolumeForm : Form
    {
        private int Progress = 0;
        private int MaxProgress = 100;
        private DateTime startTime;
        private readonly DateTime endLoadTime;
        private readonly string VolumePath;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _loadingTask;

        /// <summary>
        /// Using the built-in Dialog result always seems to return DialogResult.Cancel
        /// </summary>
        public DialogResult Result = DialogResult.Cancel;

        public LoadingVolumeForm(string path)
        {
            VolumePath = path;
            _cancellationTokenSource = new CancellationTokenSource();
            InitializeComponent();
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            startTime = DateTime.Now;

            // Start the async loading task
            _loadingTask = LoadVolumeAsync(VolumePath, _cancellationTokenSource.Token);

            // Add-on Module list initialization
            foreach (string AddonName in Viking.Common.ExtensionManager.ExtensionNames)
            {
                if (AddonName.Length > 0)
                    ListModules.Items.Add(AddonName);
            }
        }

        private async Task LoadVolumeAsync(string volumeUrl, CancellationToken token)
        {
            try
            {
                // Create progress reporter for UI updates
                Progress<ProgressInfo> progress = new(UpdateProgress);

                ProgressReporter progressReporter = new(progress);

                // Run volume loading on background thread
                var volume = await Volume.CreateAsync(volumeUrl, UI.State.CachePath, progressReporter, token);
                int pref = Viking.Properties.Settings.Default.MaxConcurrentTextureRequests;
                if (pref > 0)
                    TextureReaderV2.SetMaxConcurrentRequestLimit(pref);
                else
                    TextureReaderV2.SetMaxConcurrentRequestLimit(Viking.UI.WPF.Forms.ViewerPreferencesDialogViewModel.DefaultMaxConcurrentTextureRequests);

                // Create view model on UI thread
                await Task.Factory.StartNew(() =>
                {
                    State.volume = new VolumeViewModel(volume);
                }, token, TaskCreationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());

                progressReporter.Report(new ProgressInfo("Loading extensions...", 85, 100));

                // Load extensions
                ExtensionManager.LoadExtensions(progressReporter);

                progressReporter.Report(new ProgressInfo("Complete!", 100, 100));

                // Success - close form
                await Task.Factory.StartNew(() =>
                {
                    this.Result = DialogResult.OK;
                    this.Close();
                }, token, TaskCreationOptions.None,
                   TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (OperationCanceledException)
            {
                // User cancelled
                await Task.Factory.StartNew(() =>
                {
                    this.Result = DialogResult.Cancel;
                    this.Close();
                }, CancellationToken.None, TaskCreationOptions.None,
                   TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                // Handle errors
                await Task.Factory.StartNew(() =>
                {
                    MessageBox.Show($"Error loading volume: {ex.Message}",
                        "Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Result = DialogResult.Cancel;
                    this.Close();
                }, CancellationToken.None, TaskCreationOptions.None,
                   TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void UpdateProgress(ProgressInfo info)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ProgressInfo>(UpdateProgress), info);
                return;
            }

            this.LabelInfo.Text = info.Message;
            this.Progress = (int)info.Progress;
            this.MaxProgress = (int)info.MaxProgress;
            PanelProgress.Invalidate();
        }

        private void PanelProgress_Paint(object sender, PaintEventArgs e)
        {
            using SolidBrush FillBrush = new(Color.Blue);
            RectangleF Rect = new Rectangle(new Point(0, 0), PanelProgress.Size);
            Rect.Width = Rect.Width * (float)(Progress / (float)MaxProgress);
            e.Graphics.Clear(Color.LightGray);
            e.Graphics.FillRectangle(FillBrush, Rect);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cancel the loading operation if form is closing
            _cancellationTokenSource?.Cancel();
            base.OnFormClosing(e);
        }

        private void btnCancel_Click(object sender, EventArgs e) => _cancellationTokenSource?.Cancel();


    }
}

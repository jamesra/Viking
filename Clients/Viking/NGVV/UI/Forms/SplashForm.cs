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
using Viking.ViewModels; 


namespace Viking.UI.Forms
{
    

    public partial class SplashForm : Form
    {
        int Progress = 0;
        int MaxProgress = 100;
        DateTime startTime;
        DateTime endVolumeLoadTime;
        DateTime endExtensionLoadTime;

        readonly string VolumePath; 

        /// <summary>
        /// Using the built-in Dialog result always seems to return DialogResult.Cancel
        /// </summary>
        public DialogResult Result = DialogResult.Cancel;

        public SplashForm(string path)
        {
            VolumePath = path; 
            InitializeComponent();
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            startTime = DateTime.Now;

            LoadVolumeWorker.RunWorkerAsync();

            // Add-on Module list initialization

            foreach (string AddonName in Viking.Common.ExtensionManager.ExtensionNames)
            {
                if (AddonName.Length > 0)
                    ListModules.Items.Add(AddonName);
            }
        }

        /*
        private void OnStoreProgressEvent(object sender, LoadProgressEventArgs e)
        {
            this.LabelInfo.Text = e.Info as String;
            this.Progress = e.Progress;
            this.MaxProgress = e.MaxProgress;

            PanelProgress.Invalidate();

            if (Progress > MaxProgress)
            {
                this.Result = DialogResult.OK;
                this.Close();
            }
        }
        */

        private void PanelProgress_Paint(object sender, PaintEventArgs e)
        {
            using (SolidBrush FillBrush = new SolidBrush(Color.Blue))
            {
                RectangleF Rect = new Rectangle(new Point(0, 0), PanelProgress.Size);
                Rect.Width = Rect.Width * (float)(Progress / (float)MaxProgress);
                e.Graphics.Clear(Color.LightGray);
                e.Graphics.FillRectangle(FillBrush, Rect);
            }
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundThreadProgressReporter progressReporter = new BackgroundThreadProgressReporter(this.LoadVolumeWorker);
            Viking.VolumeModel.Volume _Volume = new Viking.VolumeModel.Volume(this.VolumePath, UI.State.CachePath, progressReporter);
            State.volume = new VolumeViewModel(_Volume); 
        }   

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.LabelInfo.Text = e.UserState as String;
            this.Progress = e.ProgressPercentage;
            this.MaxProgress = 100;

 //           Trace.WriteLine(e.UserState as String); 

            PanelProgress.Invalidate();
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {  
            endVolumeLoadTime = DateTime.Now;

            TimeSpan elapsedTime = endVolumeLoadTime.Subtract(startTime);
            Trace.WriteLine("Volume Load Time: " + elapsedTime.ToString());

            //OK, basic info about volume is in place.  Time to extensions if they want to load
            LoadExtensionsWorker.RunWorkerAsync(); 
        }

        private void LoadExtensionsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.BeginInvoke( (Action) delegate () {LabelInfo.Text = "Loading extensions";} ); 

            Viking.Common.ExtensionManager.LoadExtensions(this.LoadExtensionsWorker);
        }
        
        private void LoadExtensionsWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Result = DialogResult.OK;
            this.Close();

            endExtensionLoadTime = DateTime.Now;
            TimeSpan elapsedTime = endExtensionLoadTime.Subtract(endVolumeLoadTime);
            Trace.WriteLine("Extension Load Time: " + elapsedTime.ToString());
        }

        
    }
}

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
    public partial class LoadingVolumeForm : Form
    {
        int Progress = 0;
        int MaxProgress = 100;
        DateTime startTime;
        DateTime endLoadTime; 

        readonly string VolumePath; 

        /// <summary>
        /// Using the built-in Dialog result always seems to return DialogResult.Cancel
        /// </summary>
        public DialogResult Result = DialogResult.Cancel;

        public LoadingVolumeForm(string path)
        {
            VolumePath = path; 
            InitializeComponent();
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            startTime = DateTime.Now;

            backgroundWorker.RunWorkerAsync(null);

            // Add-on Module list initialization

            foreach (string AddonName in Viking.Common.ExtensionManager.ExtensionNames)
            {
                if (AddonName.Length > 0)
                    ListModules.Items.Add(AddonName);
            }
        }

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

        private void PanelProgress_Paint(object sender, PaintEventArgs e)
        {
            SolidBrush FillBrush = new SolidBrush(Color.Blue);
            RectangleF Rect = new Rectangle(new Point(0, 0), PanelProgress.Size);
            Rect.Width = Rect.Width * (float)(Progress / (float)MaxProgress);
            e.Graphics.Clear(Color.LightGray);
            e.Graphics.FillRectangle(FillBrush, Rect);
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {   
            State.volume = new VolumeViewModel(this.VolumePath, UI.State.CachePath, this.backgroundWorker);
        }   

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.LabelInfo.Text = e.UserState as String;
            this.Progress = e.ProgressPercentage;
            this.MaxProgress = 100;

            PanelProgress.Invalidate();
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Result = DialogResult.OK;
            this.Close();

            endLoadTime = DateTime.Now;

            TimeSpan elapsedTime = endLoadTime.Subtract(startTime);
            Trace.WriteLine("Total Load Time: " + elapsedTime.ToString()); 
        }

        
    }
}

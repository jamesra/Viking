using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        private Task _Task = null;

        /// <summary>
        /// The task we are reporting on until it finishes
        /// </summary>
        public Task TrackedTask
        {
            get => _Task;
            set
            {
                _Task = value;
                if(_Task != null)
                {
                    LoadVolumeWorker.RunWorkerAsync();
                }
            } }

        public readonly BackgroundThreadProgressReporter ProgressReporter;

        /// <summary>
        /// Using the built-in Dialog result always seems to return DialogResult.Cancel
        /// </summary>
        public DialogResult Result = DialogResult.Cancel;

        public SplashForm()
        {
            InitializeComponent();
             
            ProgressReporter = new BackgroundThreadProgressReporter(this.LoadVolumeWorker);
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            startTime = DateTime.Now;

            //LoadVolumeWorker.RunWorkerAsync();

            // Add-on Module list initialization

            foreach (string AddonName in Viking.Common.ExtensionManager.ExtensionNames)
            {
                if (AddonName.Length > 0)
                    ListModules.Items.Add(AddonName);
            }
        }

        /*
        private void OnStoreProgressEvent(object sender, LoadProgressEventArgs e)Could not load file or assembly 'file:///C:\src\git\Viking\Clients\Viking\Viking
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
            //Wait for the volume to initialize
            if(TrackedTask != null)
            { 
                while(TrackedTask.Wait(500) == false)
                { 
                    Application.DoEvents();
                }
            }
            else
            {
                throw new ArgumentException("Running background worker without a task to wait on");
            }
            
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
            this.LabelInfo.Text = "Task completed";
            this.Progress = 100;
            this.MaxProgress = 100;

            PanelProgress.Invalidate();

            this.Result = DialogResult.OK;
            this.Close();

            //endVolumeLoadTime = DateTime.Now;

            //TimeSpan elapsedTime = endVolumeLoadTime.Subtract(startTime);
            //Trace.WriteLine("Volume Load Time: " + elapsedTime.ToString());

            //OK, basic info about volume is in place.  Time to extensions if they want to load
            //LoadExtensionsWorker.RunWorkerAsync();
        }
        /*
        private void LoadExtensionsWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.BeginInvoke((Action)delegate () { LabelInfo.Text = "Loading extensions"; });

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
        */
    }
}

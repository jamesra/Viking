using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Viking.Common;
using Viking.UI;
using Viking.UI.Forms;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace Viking
{
    public partial class VikingMain : Form
    {

        public VikingMain()
        {
            InitializeComponent();

            TabsModules.TabCategory = TABCATEGORY.ACTION; 
        }


        /// <summary>
        /// I only have this in case Asynch file IO completes, in which case I want the screen to refresh
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer_Tick(object sender, EventArgs e)
        {
            //this.Refresh();
        }

        private void VikingMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            Global.PrintAllocatedTextures();
            Global.PrintAllocatedTextureReaders();
        }

        Thread DiskCleanupThread = null;
        Thread TileViewModelThread = null;
        Thread TileThread = null;

        private Thread CreateThread(string name, ThreadStart ThreadStartingFunction)
        {
            Thread T = new Thread(ThreadStartingFunction);
            T.Name = name;
            T.IsBackground = true;
            T.Priority = ThreadPriority.BelowNormal;

            return T;
        }

        private void FreeThread(Thread T)
        {

        }

        private void CacheCleaningTimer_Tick(object sender, EventArgs e)
        {
            //Fire off a thread to clean the disk

            ThreadPool.QueueUserWorkItem(Global.TextureCache.ReduceCacheFootprint, null);
            ThreadPool.QueueUserWorkItem(Global.TileViewModelCache.ReduceCacheFootprint, null);
            ThreadPool.QueueUserWorkItem(Viking.VolumeModel.Global.TileCache.ReduceCacheFootprint, null);

            if (Viking.UI.State.volume != null)
                ThreadPool.QueueUserWorkItem(Viking.UI.State.volume.ReduceCacheFootprint, null);


            /*
            if (DiskCleanupThread == null)
            {
                DiskCleanupThread = CreateThread("Disk Texture Cleanup", new ThreadStart(Global.TextureCache.ReduceCacheFootprint));
                DiskCleanupThread.Start(); 
            }
            else
            {
                if (DiskCleanupThread.IsAlive == false)
                {
                    DiskCleanupThread = CreateThread("Disk Texture Cleanup", new ThreadStart(Global.TextureCache.ReduceCacheFootprint)); 
                    DiskCleanupThread.Start(); 
                }
            }
            
            
            //Fire off a thread to clean tiles
            if (TileViewModelThread == null)
            {
                TileViewModelThread = CreateThread("TileViewModel Cleanup", new ThreadStart(Global.TileViewModelCache.ReduceCacheFootprint));
                TileViewModelThread.Start();
            }
            else
            {
                if (TileViewModelThread.IsAlive == false)
                {
                    TileViewModelThread = CreateThread("TileViewModel Cleanup", new ThreadStart(Global.TileViewModelCache.ReduceCacheFootprint));
                    TileViewModelThread.Start();
                }
            }
            
            
            //Fire off a thread to clean tiles
            if (TileThread == null)
            {
                TileThread = CreateThread("Tile Cleanup", new ThreadStart(Viking.VolumeModel.Global.TileCache.ReduceCacheFootprint));
                TileThread.Start();
            }
            else
            {
                if (TileThread.IsAlive == false)
                {
                    TileThread = CreateThread("Tile Cleanup", new ThreadStart(Viking.VolumeModel.Global.TileCache.ReduceCacheFootprint));
                    TileThread.Start();
                }
            }
             */

        }


        /// <summary>
        /// Load the first section by default
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VikingMain_Load(object sender, EventArgs e)
        {
            if (UI.State.volume == null)
            {
                return;
            }

            bool GestureConfigured = GestureSupport.ConfigureDefaultGestures(this.Handle);
            Trace.WriteLine($"Gesture support configuration: { (GestureConfigured ? "Successful" : "Failed") } ");
             
            //bool RegisteredTouch = Touch.RegisterTouchWindow(this.Handle, TouchRegisterOptions.None);

            //
            //bool RegisteredTouchHitTesting = Touch.RegisterTouchHitTestingWindow(this.Handle, TouchHitTesting.Client);

            //Calling EnableMouseInPointer routes mouse inpt to WM_POINT* events.  It was simpler to only have pen inputs send those events.  
            //However, for some reason setting MouseInPointer allowed the pen buttons to properly set the SecondButtonUp/Down flags in the pointer state.
            //
            bool MouseInPointerEnabled = WinMsgInput.EnableMouseInPointer(true);
             
            this.Text = UI.State.volume.Name;
             
            /* PORT
            if (UI.State.volume.Sections == null)
                return;

            if (UI.State.volume.Sections.Length == 0)
                return;
             */

            SectionViewModel DefaultSection = UI.State.volume.SectionViewModels[UI.State.volume.DefaultSectionNumber];

            SectionViewerForm SectionViewer = SectionViewerForm.Show(DefaultSection);

            bool UseDefaultPosition = true;

            //Check if we have startup arguments to send us to a specific location
            if (UI.State.StartupArguments != null)
            {
                try
                {
                    string strX = UI.State.StartupArguments["X"];
                    string strY = UI.State.StartupArguments["Y"];
                    string strZ = UI.State.StartupArguments["Z"];
                    
                    if (strX == null || strY == null || strZ == null)
                        UseDefaultPosition = true;
                    else
                    {
                        UseDefaultPosition = false;
                        float X = System.Convert.ToSingle(strX);
                        float Y = System.Convert.ToSingle(strY);
                        int Z = System.Convert.ToInt32(strZ);

                        SectionViewer.GoToLocation(new Vector2(X, Y), Z, false);
                    }
                }
                catch (Exception)
                {
                    //Oh well, just go to the default

                    Trace.WriteLine("Unable to restore view position from application settings.");
                    UseDefaultPosition = true;
                }
            }

            //Adjust the downsample level
            try
            {
                string strDownsample = UI.State.StartupArguments["DS"];
                if (strDownsample != null)
                {
                    float Downsample = System.Convert.ToSingle(strDownsample);
                    SectionViewer.CameraDownsample = Downsample;
                }
            }
            catch (Exception)
            {
                Trace.WriteLine("Unable to restore downsample level from application settings.");
            }

            if (UseDefaultPosition)
            {
                //default to centering the viewer on startup 
                MappingBase map = Viking.UI.State.volume.GetTileMapping(Viking.UI.State.volume.DefaultVolumeTransform, DefaultSection.Number, null, null);
                if (map != null)
                {
                    Geometry.GridVector2 Center = map.ControlBounds.Center;
                    SectionViewer.GoToLocation(new Vector2((float)Center.X, (float)Center.Y), DefaultSection.Number, true);
                    SectionViewer.CameraDownsample = Math.Max(map.ControlBounds.Width / SectionViewer.Width, map.ControlBounds.Height / SectionViewer.Height);
                }
            }
        }


        private void vikingHomepageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (System.Diagnostics.Process WebBrowser = new System.Diagnostics.Process())
            {
                WebBrowser.StartInfo.FileName = "http://connectomes.utah.edu/";
                WebBrowser.Start();
            }
        }

        private void versionInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutBox aboutBox = new AboutBox())
            {
                aboutBox.ShowDialog();
            }

            return;
        }
    }
}
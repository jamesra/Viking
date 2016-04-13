using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VikingXNA;
using System.Diagnostics;
using Viking.UI.Forms;
using Viking.UI;
using Viking.Common;
using System.Reflection;
using Viking.VolumeModel;
using Viking.ViewModels; 

namespace Viking
{
    public partial class VikingMain : Form
    {

        public VikingMain()
        {
            

            InitializeComponent();
            
            TabsModules.TabCategory = TABCATEGORY.ACTION;

            State.Appwindow = this;

            State.MainThreadDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher; 
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

            this.Text = UI.State.volume.Name;

            Global.TextureCache.PopulateCache(UI.State.VolumeCachePath); 

            /* PORT
            if (UI.State.volume.Sections == null)
                return;

            if (UI.State.volume.Sections.Length == 0)
                return;
             */

            SectionViewModel DefaultSection = UI.State.volume.SectionViewModels[UI.State.volume.DefaultSectionNumber];

            SectionViewerForm SectionViewer = SectionViewerForm.Show(DefaultSection);

            bool UseDefaultPosition = false; 

            //Check if we have startup arguments to send us to a specific location
            try
            {
                string strX = UI.State.StartupArguments["X"];
                string strY = UI.State.StartupArguments["Y"];
                string strZ = UI.State.StartupArguments["Z"];

                if (strX == null || strY == null || strZ == null)
                    UseDefaultPosition = true;
                else
                {
                    float X = System.Convert.ToSingle(strX);
                    float Y = System.Convert.ToSingle(strY);
                    int Z = System.Convert.ToInt32(strZ);

                    SectionViewer.GoToLocation(new Vector2(X, Y), Z, false);
                }
            }
            catch (Exception )
            {
                //Oh well, just go to the default
                UseDefaultPosition = true; 
            }

            //Adjust the downsample level
            try
            {
                string strDownsample = UI.State.StartupArguments["DS"];
                if(strDownsample != null)
                {
                    float Downsample = System.Convert.ToSingle(strDownsample);
                    SectionViewer.CameraDownsample = Downsample; 
                }
            }
            catch (Exception )
            {
            }

            if (UseDefaultPosition)
            {
                //default to centering the viewer on startup 
                MappingBase map = Viking.UI.State.volume.GetTileMapping(null, DefaultSection.Number, null, null);
                if (map != null)
                {
                    Geometry.GridVector2 Center = map.Bounds.Center;
                    SectionViewer.GoToLocation(new Vector2((float)Center.X, (float)Center.Y), DefaultSection.Number, true);
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
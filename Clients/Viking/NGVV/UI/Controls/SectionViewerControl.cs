using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Threading;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Commands;
using Viking.UI.Forms;
using Viking.Common;
using System.Diagnostics;
using Viking.VolumeModel;
using Viking.ViewModels;
using VikingXNA;


namespace Viking.UI.Controls
{
    public partial class SectionViewerControl : VikingXNA.ViewerControl
    {
        Viking.UI.Commands.Command _CurrentCommand;
        public Viking.UI.Commands.Command CurrentCommand
        {
            get { return _CurrentCommand; }
            set
            {

                if (_CurrentCommand != value && _CurrentCommand != null)
                {
                    _CurrentCommand.OnCommandCompleteHandler -= this.OnCommandCompleteHandler;
                    _CurrentCommand.UnsubscribeToInterfaceEvents();
                }

                _CurrentCommand = value;

                if (_CurrentCommand != null)
                {
                    _CurrentCommand.OnCommandCompleteHandler += OnCommandCompleteHandler;
                    _CurrentCommand.SubscribeToInterfaceEvents();
                    Trace.WriteLine("Set current command: " + _CurrentCommand.GetType().ToString(), "Command");
                    //TODO: Make these consistent with the extension commands. 
                    _CurrentCommand.OnActivate();
                }
                else
                {
                    Trace.WriteLine("Set current command: Null", "Command");
                }
            }
        }

        static short[] indicies = { 0, 1, 2, 2, 1, 3 };

        CommandCompleteEventHandler OnCommandCompleteHandler;

        ISectionOverlayExtension[] listOverlays = null;

        public VertexDeclaration VertexPositionColorDeclaration;

        /// <summary>
        /// The tile cache checkpoints aborts unwanted requests, but since we find out if tiles are
        /// wanted during draw calls we don't want to run a checkpoint unless there has been a draw
        /// call since the last checkpoint
        /// </summary>
        private bool DrawCallSinceTileCacheCheckpoint = false;

        /// <summary>
        /// When set to true Commands and ISectionOverlayExtension draw methods are called
        /// </summary>
        public bool ShowOverlays = true;

        public bool ColorizeTiles
        {
            get { return menuColorizeTiles.Checked; }
            set { menuColorizeTiles.Checked = value; }
        }


        //A friendlier way of setting camera distance
        public override double Downsample
        {
            get { return base.Downsample; }
            set
            {
                if (value < 0.01)
                {
                    value = 0.01;
                }
                StatusMagnification = value;
                base.Downsample = value;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            StatusMagnification = base.Downsample;
            //StatusMagnification = ProjectionBounds.Width / (double)ClientSize.Width;
        }

        #region Status Bar
        private System.Windows.Forms.StatusStrip StatusBar;

        protected System.Windows.Forms.ToolStripItem tsPosition;
        protected System.Windows.Forms.ToolStripItem tsSection;
        protected System.Windows.Forms.ToolStripItem tsMagnification;
        protected System.Windows.Forms.ToolStripItem tsChannels;

        private GridVector2 _StatusPosition;

        public GridVector2 StatusPosition
        {
            get
            {
                return _StatusPosition;
            }
            set
            {
                _StatusPosition = value;
                tsPosition.Text = "X: " + value.X.ToString("F2") + " Y: " + value.Y.ToString("F2"); ;
            }
        }

        public int StatusSection
        {
            set
            {
                tsSection.Text = "Section: " + value.ToString();
            }
        }

        private double _Magnification = 0;

        public double StatusMagnification
        {
            get
            { return _Magnification; }

            set
            {
                _Magnification = value;
                tsMagnification.Text = "Magnification: " + value.ToString("F2");
            }
        }

        private List<ToolStripItem> _StatusChannels = new List<ToolStripItem>();
        internal ChannelInfo[] StatusChannels
        {
            set
            {
                if (value == null || value.Length == 0)
                {
                    value = new ChannelInfo[] { new ChannelInfo() };
                }

                //Update the channels we have
                for (int i = 0; i < value.Length; i++)
                {
                    string channelName = value[i].ChannelName;
                    if (String.IsNullOrEmpty(channelName))
                    {
                        channelName = this.CurrentChannel;
                    }

                    ToolStripItem tsChannelItem = null;
                    if (_StatusChannels.Count > i)
                    {
                        tsChannelItem = _StatusChannels[i];
                    }
                    else
                    {
                        tsChannelItem = new ToolStripLabel();
                        StatusBar.Items.Add(tsChannelItem);
                        _StatusChannels.Add(tsChannelItem);
                    }

                    tsChannelItem.Text = channelName;
                    System.Drawing.Color color = value[i].FormColor;
                    //If the color is white, draw black
                    if (color.R == 255 &&
                        color.G == 255 &&
                        color.B == 255)
                        tsChannelItem.ForeColor = System.Drawing.Color.Black;
                    else
                        tsChannelItem.ForeColor = value[i].FormColor;
                }

                //Remove extra channel labels
                for (int i = _StatusChannels.Count - 1; i >= value.Length; i--)
                {
                    ToolStripItem tsChannelItem = _StatusChannels[i];
                    StatusBar.Items.Remove(tsChannelItem);
                    _StatusChannels.RemoveAt(i);
                }

            }
        }

        #endregion

        private SectionViewModel _Section;

        /// <summary>
        /// The section we are currently viewing
        /// </summary>
        public SectionViewModel Section
        {
            get { return _Section; }
            set
            {
                if (_Section == value)
                    return;

                SectionViewModel OldSection = _Section;
                string oldtransform = null;
                if (_Section != null)
                    oldtransform = this.CurrentTransform;

                if (value != null)
                {

                    Trace.WriteLine("Open Section: " + value.Number.ToString(), "UI");
                    StatusSection = value.Number;
                }

                if (OldSection != null)
                {
                    OldSection.OnReferenceSectionChanged -= this.InternalReferenceSectionChanged;
                }

                _Section = value;
                if (_Section != null)
                {
                    //NOTE: We have to update the section before we ask for the reference section
                    if (State.UseSectionSpecificTransform == false && oldtransform != null)
                        this.CurrentTransform = oldtransform;

                    this.StatusSection = _Section.Number;

                    this._Section.OnReferenceSectionChanged += this.InternalReferenceSectionChanged;

                    //Figure out if the new section supports the current channel
                    if (_Section.Channels.Contains(this.CurrentChannel) == false)
                        CurrentChannel = _Section.DefaultChannel;
                }

                ///Find the adjacent sections and request them to warp into volume space if they haven't already    
                if (State.UseSectionSpecificTransform == false && oldtransform != null)
                {
                    SortedList<int, SectionViewModel> sections = UI.State.volume.SectionViewModels;
                    int iSection = sections.IndexOfKey(this._Section.Number);
                    int iSectionAbove = iSection + 1;
                    int iSectionBelow = iSection - 1;

                    if (iSectionAbove < sections.Count)
                    {
                        sections.Values[iSectionAbove].PrepareTransform(oldtransform);
                    }

                    if (iSectionBelow >= 0)
                    {
                        sections.Values[iSectionBelow].PrepareTransform(oldtransform);
                    }
                }



                this.Invalidate();

                //Let listeners know if we changed sections
                if (OnSectionChanged != null)
                    OnSectionChanged(this, new SectionChangedEventArgs(_Section, OldSection));
            }
        }

        /// <summary>
        /// Currently selected tileset 
        /// </summary>
        protected string _CurrentChannel;
        public string CurrentChannel
        {
            get { return _CurrentChannel; }
            set
            {
                bool Invalidate = value != _CurrentChannel;

                _CurrentChannel = value;
                if (Invalidate)
                    this.Invalidate();
            }
        }

        /// <summary>
        /// Determines which transform should be used when rendering the section
        /// </summary>
        protected string _CurrentTransform;
        public string CurrentTransform
        {
            get { return _CurrentTransform; }
            set
            {
                bool NewValue = _CurrentVolumeTransform != value;
                string OldTransform = _CurrentVolumeTransform;
                _CurrentTransform = value;
                if (OnSectionTransformChanged != null && NewValue)
                {
                    OnSectionTransformChanged(this, new TransformChangedEventArgs(_CurrentTransform, OldTransform));
                }
            }
        }

        public bool UsingVolumeTransform
        {
            get
            {
                return this.CurrentVolumeTransform.ToLower() != "none";
            }
        }


        protected string _CurrentVolumeTransform;
        public string CurrentVolumeTransform
        {
            get { return _CurrentVolumeTransform; }
            set
            {

                bool NewValue = value != _CurrentChannel;

                if (NewValue)
                {

                    string OldTransform = _CurrentVolumeTransform;
                    _CurrentVolumeTransform = value;
                    _CurrentTransformID = new int?();

                    if (OnVolumeTransformChanged != null)
                    {
                        OnVolumeTransformChanged(this, new TransformChangedEventArgs(_CurrentTransform, OldTransform));
                    }

                    this.Invalidate();
                }
            }
        }

        private int? _CurrentTransformID= new int?(); 
        /// <summary>
        /// Return an unique ID for the current transform being used so we can quickly check if we need to recalculate positions
        /// </summary>
        public int CurrentTransformUniqueID
        {
            get
            {
                if (!_CurrentTransformID.HasValue) 
                {
                    this._CurrentTransformID = this.CurrentVolumeTransform.GetHashCode();
                }

                return this._CurrentTransformID.Value;
            }
        }

        /// <summary>
        /// Fires when the transform used to render the section changes
        /// </summary>
        public event TransformChangedEventHandler OnSectionTransformChanged;

        /// <summary>
        /// Fires when the transform used to place the section into the volume changes
        /// </summary>
        public event TransformChangedEventHandler OnVolumeTransformChanged;

        #region Section/Volume mapping

        /// <summary>
        /// Maps a point from volume space into the section space for the currently selected section and transform
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public GridVector2 VolumeToSection(GridVector2 P)
        {
            return VolumeToSection(P, this.Section.section);
        }

        public GridVector2[] VolumeToSection(GridVector2[] P)
        {
            return P.Select(p => VolumeToSection(p, this.Section.section)).ToArray();
        }

        /// <summary>
        /// Maps a point from volume space into the section space for the currently selected transform
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public GridVector2 VolumeToSection(GridVector2 P, Section section)
        {
            MappingBase map = MappingManager.GetMapping(this.CurrentVolumeTransform, section, this.CurrentChannel, this.CurrentTransform);
            if (map != null)
            {
                GridVector2 TransformedPoint = map.VolumeToSection(new GridVector2(P.X, P.Y));
                return TransformedPoint;
            }
            else
            {
                throw new System.ArgumentException("Cannot map requested point from Volume to Section");
            }
        }

        public bool TryVolumeToSection(GridVector2 P, SectionViewModel section, out GridVector2 transformedP)
        {
            transformedP = new GridVector2();

            MappingBase map = MappingManager.GetMapping(this.CurrentVolumeTransform, section.section, this.CurrentChannel, this.CurrentTransform);
            if (map != null)
            {
                return map.TryVolumeToSection(new GridVector2(P.X, P.Y), out transformedP);
            }

            return false;
        }

        /// <summary>
        /// Maps a point from section space into volume space for the currently selected section and transform
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public GridVector2 SectionToVolume(GridVector2 P)
        {
            return SectionToVolume(P, this.Section.section);
        }

        public GridVector2[] SectionToVolume(GridVector2[] P)
        {
            return P.Select(sp => SectionToVolume(sp, this.Section.section)).ToArray();
        }

        /// <summary>
        /// Maps a point from section space into volume space for the currently selected transform
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public GridVector2 SectionToVolume(GridVector2 P, Section section)
        {
            MappingBase map = MappingManager.GetMapping(this.CurrentVolumeTransform, section, this.CurrentChannel, this.CurrentTransform);
            if (map != null)
            {
                GridVector2 TransformedPoint = map.SectionToVolume(new GridVector2(P.X, P.Y));
                return TransformedPoint;
            }
            else
            {
                throw new System.ArgumentException("Cannot map requested point from Volume to Section");
            }
        }

        /// <summary>
        /// Maps a point from section space into volume space for the currently selected transform
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public bool TrySectionToVolume(GridVector2 P, Section section, out GridVector2 transformedP)
        {
            transformedP = new GridVector2();
            MappingBase map = MappingManager.GetMapping(this.CurrentVolumeTransform, section, this.CurrentChannel, this.CurrentTransform);
            if (map != null)
            {
                return map.TrySectionToVolume(new GridVector2(P.X, P.Y), out  transformedP);
            }

            return false;
        }

        /// <summary>
        /// Get the boundaries for a section given the current transforms
        /// </summary>
        /// <param name="section"></param>
        /// <returns></returns>
        public GridRectangle SectionBounds(Section section)
        {
            MappingBase map = MappingManager.GetMapping(this.CurrentVolumeTransform, section, this.CurrentChannel, this.CurrentTransform);
            if (map != null)
            {
                return map.Bounds;
            }

            throw new System.ArgumentException("Cannot find boundaries for section");
        }

        #endregion

        public SectionViewerControl()
        {
            InitializeComponent();

            StatusBar = new System.Windows.Forms.StatusStrip();
            StatusBar.Parent = this;
            StatusBar.Dock = System.Windows.Forms.DockStyle.Bottom;
             
            tsSection = new System.Windows.Forms.ToolStripLabel("Section: ");
            tsPosition = new System.Windows.Forms.ToolStripLabel("Position: ");
            tsMagnification = new System.Windows.Forms.ToolStripLabel("Zoom: ");
            tsChannels = new System.Windows.Forms.ToolStripLabel("Channels: ");
             
            StatusBar.Items.Add(tsSection);
            StatusBar.Items.Add(tsPosition);
            StatusBar.Items.Add(tsMagnification);
            StatusBar.Items.Add(tsChannels);

            ObjectSelectedHandler = new Viking.Common.ObjectSelectedEventHandler(this.OnSelectedItemChanged);
            InternalReferenceSectionChanged = new EventHandler(this.OnInternalReferenceSectionChanged);
            State.ItemSelected += ObjectSelectedHandler;

            ExtensionManager.AddMenuItems(this.menuStrip);
        }

        protected override void Initialize()
        {
            if (!DesignMode)
            {
                this.menuStrip.Parent = this.Parent;

                OnCommandCompleteHandler = new Viking.Common.CommandCompleteEventHandler(this.OnCommandCompleted);

                if (State.SelectedObject != null)
                {
                    CurrentCommand = Command.CreateFor(this, State.SelectedObject.GetType());
                }
                else
                {
                    CurrentCommand = Command.CreateFor(this, null);
                }

                this.CurrentVolumeTransform = UI.State.volume.DefaultVolumeTransform;
                this.CurrentChannel = Section.DefaultChannel;

                this.listOverlays = ExtensionManager.CreateSectionOverlays(this);
            }

            base.Initialize();
        }


        #region Event Handlers

        /// <summary>
        /// Fired when an object is selected in the UI
        /// </summary>
        Viking.Common.ObjectSelectedEventHandler ObjectSelectedHandler;

        /// <summary>
        /// Fires when a different section is displayed
        /// </summary>
        public event SectionChangedEventHandler OnSectionChanged;

        /// <summary>
        /// Fires when one of the reference sections has changed
        /// </summary>
        public event EventHandler OnReferenceSectionChanged;

        /// <summary>
        /// Called when the reference section for the current section has changed. 
        /// Fires our public ReferenceSectionChanged event
        /// </summary>
        private EventHandler InternalReferenceSectionChanged;

        private void OnSelectedItemChanged(object sender, Viking.Common.ObjectSelectedEventArgs e)
        {
            if (CurrentCommand != null)
                CurrentCommand.Deactivated = true;

            this.Invalidate();
        }

        /// <summary>
        /// Recieves the event from the section when the reference has changed and fires an event to any listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInternalReferenceSectionChanged(object sender, EventArgs e)
        {
            if (OnReferenceSectionChanged != null)
            {
                OnReferenceSectionChanged(sender, e);
            }
        }

        /*
        private void OnSelectedDesignItemChanged(object sender, PlantMap.Common.ObjectSelectedEventArgs e)
        {
            //Reset our color mapped cursor
            if (CurrentCommand != null)
                CurrentCommand.Deactivated = true;

            this.Invalidate();
        }
        */

        private void OnCommandCompleted(object sender, System.EventArgs e)
        {
            this.Cursor = Cursors.Default;
            CurrentCommand = Command.CreateFor(this, UI.State.SelectedObject);
            this.Invalidate();
        }

        #endregion

        public string[] ExtensionOverlayTitles()
        {
            if (this.listOverlays == null)
                return new string[0];

            List<string> names = new List<string>(this.listOverlays.Count());

            foreach(ISectionOverlayExtension IOverlay in this.listOverlays)
            {
                string name = IOverlay.Name();
                if(name == null)
                    continue;
                if(name.Length == 0)
                    continue;

                names.Add(name);
            }

            return names.ToArray();
        }

        /// <summary>
        /// Swaps the current section for the section above it
        /// </summary>
        public void StepUpNSections(int nSections)
        {
            SortedList<int, SectionViewModel> sections = UI.State.volume.SectionViewModels;

            /* find the next section */
            int iStart = this.Section.Number + nSections;
            while (iStart <= sections.Keys.Max())
            {
                if (sections.ContainsKey(iStart))
                {
                    this.Section = sections[iStart];
                    break;
                }
                iStart++;
            }
        }

        public void StepDownNSections(int nSections)
        {
            SortedList<int, SectionViewModel> sections = UI.State.volume.SectionViewModels;

            /* find the next section */
            int iStart = this.Section.Number - nSections;
            while (iStart >= sections.Keys.Min())
            {
                if (sections.ContainsKey(iStart))
                {
                    this.Section = sections[iStart];
                    break;
                }
                iStart--;
            }
        }

        public void ExportImage(string Filename, GridRectangle MyRect, double Downsample, bool IncludeOverlays)
        {
            Debug.Assert(MyRect.Left < MyRect.Right);
            Debug.Assert(MyRect.Bottom < MyRect.Top);

            bool OriginalOverlays = this.ShowOverlays;
            bool AsynchTextureLoad = this.AsynchTextureLoad;

            this.ShowOverlays = IncludeOverlays;
            this.AsynchTextureLoad = false;

            //Image Dimensions
            int RequestedWorldX = (int)Math.Floor(MyRect.Center.X);
            int RequestedWorldY = (int)Math.Floor(MyRect.Center.Y);
            int RequestedWorldWidth = (int)Math.Round(MyRect.Width);
            int RequestedWorldHeight = (int)Math.Round(MyRect.Height);

            //Image dimensions on screen
            int CapturedTileSizeX = (int)(Math.Round(MyRect.Width / Downsample));
            int CapturedTileSizeY = (int)(Math.Round(MyRect.Height / Downsample));

            int FinalImageWidth = CapturedTileSizeX;
            int FinalImageHeight = CapturedTileSizeY;

            int AdjustedWorldX = RequestedWorldX;
            int AdjustedWorldY = RequestedWorldY;
            int AdjustedWorldWidth = RequestedWorldWidth;
            int AdjustedWorldHeight = RequestedWorldHeight;

            int WorldTileSizeX = RequestedWorldWidth;
            int WorldTileSizeY = RequestedWorldHeight;

            int numTilesX = 1;
            int numTilesY = 1;

            Camera camera = new Camera();
            camera.Downsample = Downsample;

            //Figure out if we can do the entire shot at once or have to divide it up
            if (CapturedTileSizeX <= 2048 && CapturedTileSizeX <= 2048)
            {

            }
            else
            {
                //The dimensions of a single cell in our captureg rid
                int TileCaptureMaxSize = 1024;

                //Find out how many tiles we'll have to capture using a buffer smaller than the current screen size
                numTilesX = (int)Math.Ceiling((double)CapturedTileSizeX / (double)TileCaptureMaxSize);
                numTilesY = (int)Math.Ceiling((double)CapturedTileSizeY / (double)TileCaptureMaxSize);

                WorldTileSizeX = RequestedWorldWidth / numTilesX;
                WorldTileSizeY = RequestedWorldHeight / numTilesY;

                CapturedTileSizeX = (int)Math.Round(WorldTileSizeX / Downsample);
                CapturedTileSizeY = (int)Math.Round(WorldTileSizeY / Downsample);

                FinalImageWidth = CapturedTileSizeX * numTilesX;
                FinalImageHeight = CapturedTileSizeY * numTilesY;

                //CaptureWidth = TileSizeX * numTilesX;
                //CaptureHeight = TileSizeY * numTilesY;                

                AdjustedWorldWidth = WorldTileSizeX * numTilesX;
                AdjustedWorldHeight = WorldTileSizeY * numTilesY;

                int OffsetX = RequestedWorldWidth - AdjustedWorldWidth;
                int OffsetY = RequestedWorldHeight - AdjustedWorldHeight;

                AdjustedWorldX = RequestedWorldX + (OffsetX / 2);
                AdjustedWorldY = RequestedWorldY + (OffsetY / 2);
            }

            using(VikingXNA.Scene TileScene = new Scene(new Viewport(0, 0, CapturedTileSizeX, CapturedTileSizeY), camera))
            {

                using (System.Drawing.Bitmap bmp = new Bitmap(FinalImageWidth, FinalImageHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {

                    // int numCols = PixelSpaceWidth / PixelSpaceCellDim;
                    // int numRows = PixelSpaceHeight / PixelSpaceCellDim;


                    //            VikingXNA.BitmapFile bmpFile = new VikingXNA.BitmapFile(Filename, ImageWidth, ImageHeight, 8);
                    GraphicsDevice graphicsDevice = this.graphicsDeviceService.GraphicsDevice;
                    
                    //Figure out how to cut the rectangle into 512x512 cells and take the screenshots
                    for (int iRow = 0; iRow < numTilesY; iRow++)
                    {
                        double Y = AdjustedWorldY + (iRow * WorldTileSizeY);

                        for (int iCol = 0; iCol < numTilesX; iCol++)
                        {
                            //Figure out the rectangle we need to capture at this location
                            double X = AdjustedWorldX + (iCol * WorldTileSizeX);


                            BitmapData lockedBmpData = null;
                            try
                            {
                                TileScene.Camera.LookAt = new Vector2((float)X, (float)Y);

                                //PORT XNA 4
                                /*GridRectangle CaptureArea = new GridRectangle(new GridVector2(X, Y),
                                                                              WorldTileSizeX,
                                                                              WorldTileSizeY);
                                */

                                Byte[] byteArray = null; 
                                using (RenderTarget2D renderTargetTile = new RenderTarget2D(graphicsDevice, CapturedTileSizeX, CapturedTileSizeY, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents))
                                {

                                    Draw(TileScene, renderTargetTile);

                                    //Draw the scene to the renderTargetTile
                                    //Draw(this.graphicsDeviceService.GraphicsDevice, CaptureArea, Downsample, renderTargetTile);

                                    //Obtain texture from renderTarget
                                    graphicsDevice.SetRenderTarget(null);

                                    Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[] Data = new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[CapturedTileSizeX * CapturedTileSizeY];

                                    renderTargetTile.GetData<Microsoft.Xna.Framework.Graphics.PackedVector.Byte4>(Data);

                                    byteArray = new Byte[Data.Length * 4];
                                    int iByte = 0;
                                    for (int iData = 0; iData < Data.Length; iData++, iByte += 4)
                                    {
                                        byteArray[iByte] = (Byte)(Data[iData].PackedValue >> 16);
                                        byteArray[iByte + 1] = (Byte)(Data[iData].PackedValue >> 8);
                                        byteArray[iByte + 2] = (Byte)(Data[iData].PackedValue >> 0);
                                        byteArray[iByte + 3] = (Byte)(Data[iData].PackedValue >> 24);
                                    }
                                }

                                System.Drawing.Rectangle bmpRect = new System.Drawing.Rectangle((int)(iCol * CapturedTileSizeX),
                                                                                                (int)(((numTilesY - 1) - iRow) * CapturedTileSizeY),
                                                                                                CapturedTileSizeX,
                                                                                                CapturedTileSizeY);

                                //Write the Data into the bitmap
                                //bmpFile.WriteRectangle(bmpRect, byteArray);

                                lockedBmpData = bmp.LockBits(bmpRect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                                //                      PixelSize = data.Stride / data.Width;
                                //                        int TotalBytes = lockedBmpData.Stride * lockedBmpData.Height;
                                //                        rgbValues = new Byte[TotalBytes];

                                //The easy case...
                                if (lockedBmpData.Stride == lockedBmpData.Width * 4)
                                    System.Runtime.InteropServices.Marshal.Copy(byteArray, 0, lockedBmpData.Scan0, byteArray.Length);
                                else
                                {
                                    for (int iBmpRow = 0; iBmpRow < lockedBmpData.Height; iBmpRow++)
                                    {
                                        System.Runtime.InteropServices.Marshal.Copy(byteArray, iBmpRow * 4 * CapturedTileSizeX, lockedBmpData.Scan0 + (lockedBmpData.Stride * iBmpRow), 4 * CapturedTileSizeX);
                                    }
                                }

                                bmp.UnlockBits(lockedBmpData);
                                lockedBmpData = null; 
                                
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show("An exception occured during capture.  The only suggestion is to update graphics drivers." + e.ToString(), "Screenshot Error");
                                break;
                            }
                            finally
                            {
                                if (lockedBmpData != null)
                                {
                                    bmp.UnlockBits(lockedBmpData);
                                    lockedBmpData = null;
                                }

                                graphicsDevice.SetRenderTarget(null);  
                            }
                        }
                    }

                    bmp.Save(Filename);
                } 
            }

            //            System.GC.Collect();

            this.ShowOverlays = OriginalOverlays;
            this.AsynchTextureLoad = AsynchTextureLoad;

             
            //bmpFile.Close();
        }

        protected void InitGraphicsDeviceForDraw(GraphicsDevice graphicsDevice)
        {

        }

        private DepthStencilState _defaultDepthState = null;
        public DepthStencilState defaultDepthState
        {
            get
            {
                if (_defaultDepthState == null || _defaultDepthState.IsDisposed)
                {
                    _defaultDepthState = new DepthStencilState();
                    _defaultDepthState.DepthBufferEnable = true;
                    _defaultDepthState.DepthBufferFunction = CompareFunction.LessEqual;
                    _defaultDepthState.DepthBufferWriteEnable = true;
                    _defaultDepthState.StencilEnable = false;
                }

                return _defaultDepthState;
            }
        }

        private DepthStencilState _OverlayBackgroundDepthState = null;
        public DepthStencilState OverlayBackgroundDepthState
        {
            get
            {
                if (_OverlayBackgroundDepthState == null || _OverlayBackgroundDepthState.IsDisposed)
                {
                    _OverlayBackgroundDepthState = new DepthStencilState();
                    _OverlayBackgroundDepthState.DepthBufferEnable = false;
                    _OverlayBackgroundDepthState.DepthBufferWriteEnable = true;
                    _OverlayBackgroundDepthState.DepthBufferFunction = CompareFunction.LessEqual;

                    _OverlayBackgroundDepthState.StencilEnable = true;
                    _OverlayBackgroundDepthState.StencilFunction = CompareFunction.Greater;
                    _OverlayBackgroundDepthState.ReferenceStencil = 1;
                }

                return _OverlayBackgroundDepthState;
            }
        }

        private DepthStencilState _OverlayDepthState = null;

        protected DepthStencilState CreateDepthStateForOverlay(int StencilValue, bool DepthEnabled = true)
        {
            if (_OverlayDepthState != null && !_OverlayDepthState.IsDisposed)
            {
                _OverlayDepthState.Dispose();
                _OverlayDepthState = null;
            }

            if (_OverlayDepthState == null)
            {
                _OverlayDepthState = new DepthStencilState();
                _OverlayDepthState.DepthBufferEnable = DepthEnabled;
                _OverlayDepthState.DepthBufferWriteEnable = true;
                _OverlayDepthState.DepthBufferFunction = CompareFunction.LessEqual;

                _OverlayDepthState.StencilEnable = true;
                _OverlayDepthState.StencilFunction = CompareFunction.Greater;
                _OverlayDepthState.ReferenceStencil = StencilValue;
                _OverlayDepthState.StencilPass = StencilOperation.Replace;
            }

            return _OverlayDepthState;
        }

        private DepthStencilState _DrawSectionDepthState = null;
        protected DepthStencilState CreateDepthStateForDownsampleLevel(int StencilValue)
        {
            if (_DrawSectionDepthState != null && !_DrawSectionDepthState.IsDisposed)
            {
                _DrawSectionDepthState.Dispose();
                _DrawSectionDepthState = null;
            }

            if (_DrawSectionDepthState == null)
            {
                _DrawSectionDepthState = new DepthStencilState();
                _DrawSectionDepthState.DepthBufferEnable = true;
                _DrawSectionDepthState.DepthBufferWriteEnable = true;
                _DrawSectionDepthState.DepthBufferFunction = CompareFunction.LessEqual;

                _DrawSectionDepthState.StencilEnable = true;
                _DrawSectionDepthState.StencilFunction = CompareFunction.GreaterEqual;
                _DrawSectionDepthState.ReferenceStencil = StencilValue;
                _DrawSectionDepthState.StencilPass = StencilOperation.Replace;
            }

            return _DrawSectionDepthState;
        }

        private DepthStencilState _DepthDisabledState = null;
        protected DepthStencilState DepthDisabledState
        {
            get
            {
                if (_DepthDisabledState == null || _DepthDisabledState.IsDisposed)
                {
                    _DepthDisabledState = new DepthStencilState();
                    _DepthDisabledState.DepthBufferEnable = false;
                }
                return _DepthDisabledState;
            }
        }
        protected override void Draw(Scene scene)
        {
            //graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            if (Section == null)
                return;

            GraphicsDevice graphicsDevice = Device;
            RenderTargetBinding[] originalRenderTargets = Device.GetRenderTargets();
            GridRectangle Bounds = scene.VisibleWorldBounds;

            basicEffect.Alpha = 1.0f;
            basicEffect.AmbientLightColor = new Microsoft.Xna.Framework.Vector3(1, 1, 1);

            graphicsDevice.DepthStencilState = defaultDepthState;

            BlendState OriginalBlendState = graphicsDevice.BlendState;

            DrawCallSinceTileCacheCheckpoint = true;

            double HalfWidth = Bounds.Width / 2;
            double HalfHeight = Bounds.Height / 2;
            GridVector2 BotLeft = new GridVector2(Bounds.Center.X - HalfWidth, Bounds.Center.Y + HalfHeight);
            GridVector2 TopRight = new GridVector2(Bounds.Center.X + HalfWidth, Bounds.Center.Y - HalfHeight);

            VertexPositionNormalTexture[] visibleAreaMesh = {
                new VertexPositionNormalTexture( new Vector3((float)BotLeft.X, (float)BotLeft.Y, 0), Vector3.UnitZ, new Vector2(0,0)),
                new VertexPositionNormalTexture( new Vector3((float)TopRight.X, (float)BotLeft.Y, 0), Vector3.UnitZ,  new Vector2(1,0)),
                new VertexPositionNormalTexture( new Vector3((float)BotLeft.X, (float)TopRight.Y, 0), Vector3.UnitZ,   new Vector2(0,1)),
                new VertexPositionNormalTexture( new Vector3((float)TopRight.X, (float)TopRight.Y, 0), Vector3.UnitZ, new Vector2(1,1))};

            //OK, figure out if we are rendering channels or not.
            //The section channel settings are checked first.  If they
            //are not found we use the global channel settings.
            ChannelInfo[] Channelset = Section.ChannelInfoArray;
            if (Channelset.Length == 0)
            {
                //See if there are any global channel settings
                Channelset = Section.VolumeViewModel.DefaultChannels;
            }

            StatusChannels = Channelset;
            State.CurrentMode = this.CurrentChannel;

            Texture backgroundSection = null;
            Texture ChannelOverlay = null;
            if (Channelset.Length == 0)
            {
                tileLayoutEffect.TileColor = new Microsoft.Xna.Framework.Color(1f, 1f, 1f, 1);
                tileLayoutEffect.RenderToGreyscale();

                backgroundSection = DrawSection(graphicsDevice, this.Section.section, this.CurrentChannel, scene);
            }
            else
            {
                //Walk through each channel and draw the section
                backgroundSection = DrawSectionsWithChannels(graphicsDevice, Channelset, scene, out ChannelOverlay);
            }

            //OK, enable stencil buffer.  
            graphicsDevice.SetRenderTargets(originalRenderTargets);

            this.channelOverlayEffect.SetEffectTextures(backgroundSection, ChannelOverlay);

            //this.channelOverlayEffect.BackgroundTexture = backgroundSection;
            //this.channelOverlayEffect.OverlayTexture = ChannelOverlay;

            int NextStencilValue = 0;

            graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Microsoft.Xna.Framework.Color.Black, float.MaxValue, NextStencilValue++);

            //Set a standard starting state for all overlay modules
            graphicsDevice.DepthStencilState = OverlayBackgroundDepthState;
            graphicsDevice.ReferenceStencil = 1;


            //OK, blend the overlay with the underlying greyscale image
            foreach (EffectPass pass in channelOverlayEffect.effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                                                                                  visibleAreaMesh, 0, visibleAreaMesh.Length,
                                                                                  indicies, 0, indicies.Length / 3);
            }

            //Draw the tiles without annotations, allow them to overwrite existing data
            //            List<RenderTarget2D> OverlayList = new List<RenderTarget2D>(listOverlays.Length);
            if (ShowOverlays)
            {
                //                List<Vector4> listChannelColors = new List<Vector4>();
                //Vector4 white = new Microsoft.Xna.Framework.Color(1, 1, 1, 0).ToVector4(); //Use alpha=0 so we blend color with background by default
                //                Vector4 white = Microsoft.Xna.Framework.Color.White.ToVector4();

                for (int i = 0; i < listOverlays.Length; i++)
                {
                    ++NextStencilValue;
                    graphicsDevice.DepthStencilState = CreateDepthStateForOverlay(++NextStencilValue);
                    graphicsDevice.ReferenceStencil = NextStencilValue;

                    graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, float.MaxValue, 0);

                    ISectionOverlayExtension overlayObj = listOverlays[i];
                    overlayObj.Draw(graphicsDevice, scene, backgroundSection, ChannelOverlay, ref NextStencilValue);
                }

                ///This is a bad way to know if we are capturing a screenshot, but works for now
                if (AsynchTextureLoad)
                {
                    try
                    {
                        if (CurrentCommand != null)
                        {
                            graphicsDevice.DepthStencilState = CreateDepthStateForOverlay(++NextStencilValue, false);
                            graphicsDevice.ReferenceStencil = NextStencilValue;

                            CurrentCommand.OnDraw(graphicsDevice, scene, basicEffect);
                        }

                    }
                    catch (InvalidOperationException)
                    {
                        Trace.WriteLine("Could not create render target for channels", "UI");
                    }
                }
            }

            graphicsDevice.Textures[0] = null;
            graphicsDevice.Textures[1] = null;
            graphicsDevice.Textures[2] = null;
            graphicsDevice.Textures[3] = null;
            graphicsDevice.Textures[4] = null;
            graphicsDevice.Textures[5] = null;
            graphicsDevice.Textures[6] = null;
            graphicsDevice.Textures[7] = null;

            if (backgroundSection != null)
            {
                backgroundSection.Dispose();
                backgroundSection = null;
            }

            if (ChannelOverlay != null)
            {
                ChannelOverlay.Dispose();
                ChannelOverlay = null;
            }

            graphicsDevice.BlendState = OriginalBlendState;
        }

        public static string TileCacheFullPath(Section section, string TextureFileName)
        {
            return System.IO.Path.Combine(new string[] { State.TextureCachePath, section.SectionSubPath, TextureFileName });
        }

        protected Texture DrawSection(GraphicsDevice graphicsDevice, Section section, string channel, Scene scene)
        {
            //           Microsoft.Xna.Framework.Color[] ColorWheel = new Microsoft.Xna.Framework.Color[] { new Microsoft.Xna.Framework.Color(1f,0,0), 
            //                                             new Microsoft.Xna.Framework.Color(0,1f,0),
            //                                          new Microsoft.Xna.Framework.Color(0,0,1f)};

            MappingBase Mapping = MappingManager.GetMapping(this.CurrentVolumeTransform,
                                                               section,
                                                               channel,
                                                               this.CurrentTransform);

            if (Mapping == null)
                return null;

            double downsample = scene.Camera.Downsample;

            int roundedDownsample = Mapping.NearestAvailableLevel(downsample);

            //Get all of the visible tiles
            TilePyramid visibleTiles = Mapping.VisibleTiles(scene.VisibleWorldBounds, downsample);

            //Find the index of the requested downsample level
            List<int> DownsamplesToRender = new List<int>(Mapping.AvailableLevels.Length);

            //Render every other downsample level starting with the requested level
            //Render downsample levels that require more than one tile to cover the screen;
            //            int ScreenArea = graphicsDevice.Viewport.Width * graphicsDevice.Viewport.Height; 

            //            int iStartingDownsampleLevel = 0;
            for (int i = 0; i < Mapping.AvailableLevels.Length; i++)
            {
                if (roundedDownsample == Mapping.AvailableLevels[i])
                {
                    //   iStartingDownsampleLevel = i;
                    DownsamplesToRender.Add(i);

                }
                else if (roundedDownsample < Mapping.AvailableLevels[i])
                {
                    //Don't bother loading other textures if we are loading them synchronously
                    if (AsynchTextureLoad)
                    {
                        DownsamplesToRender.Add(i);
                    }
                }
            }

            RenderTarget2D renderTarget = new RenderTarget2D(graphicsDevice,
                                              scene.Viewport.Width,
                                              scene.Viewport.Height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        


            //        Debug.Assert(graphicsDevice.Viewport.Width == ClientRectangle.Width); 

            graphicsDevice.SetRenderTarget(renderTarget);
            //       graphicsDevice.SetRenderTarget(null);

            //Clear the stencil buffer before we begin
            graphicsDevice.Clear(ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, float.MaxValue, 0);
            DepthStencilState originalDepthState = graphicsDevice.DepthStencilState;

            //Textures are fetched in the order they are asked for.  So we should ask for low-res textures before high-res textures.  However if high-res textures are available we shouldn't bother
            //with asking for low res textures.
            DownsamplesToRender.Reverse();

            List<TileViewModel> MeshTiles = new List<TileViewModel>();

            //We only request textures for every other downsample level if they aren't loaded
            for (int iLevel = 0; iLevel < DownsamplesToRender.Count; iLevel++)
            {
                int level = Mapping.AvailableLevels[DownsamplesToRender[iLevel]];

                //Clear the depth buffer before we begin this level, we only want to compare to tiles in our level
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Blue, float.MaxValue, int.MaxValue);

                //Use a stencil buffer to prevent lower-res textures from overwriting higer-res textures
                graphicsDevice.ReferenceStencil = iLevel;
                graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(iLevel);

                SortedDictionary<string, Tile> tileList = visibleTiles.GetTilesForLevel(level);

                //Trace.WriteLine(tileList.Count.ToString() + " tiles found for level " + level.ToString());
                int iColor = 0;
                //     bool AllTilesDrawn = true; //The first time all tiles draw successfully we can skip the remaining levels
                foreach (Tile t in tileList.Values)
                {
                    //Don't bother with huge tiles
                    string tileFileName = t.TextureFullPath;
                    //Calculate the path of the tile
                    if (!(t.TextureFullPath.StartsWith(System.Uri.UriSchemeHttps) ||
                        t.TextureFullPath.StartsWith(System.Uri.UriSchemeHttp)))
                    {
                        tileFileName = section.Path + System.IO.Path.DirectorySeparatorChar + tileFileName;
                    }
                    //Create a TileViewModel if it doesn't exist and draw it

                    TileViewModel tileViewModel = Global.TileViewModelCache.FetchOrConstructTile(t,
                                                                                                    tileFileName,
                                                                                                    SectionViewerControl.TileCacheFullPath(section, t.TextureCacheFilePath),
                                                                                                    Mapping.Name,
                                                                                                    0);

                    //Don't request and draw a bunch of levels that cover the entire screen.  Saves time if we are at high magnification
                    if (tileViewModel.HasTexture == false && tileViewModel.Downsample > Downsample * 8 && iLevel < DownsamplesToRender.Count - 1)
                        continue;

                    //         AllTilesDrawn = AllTilesDrawn & tileViewModel.HasTexture; 
                    tileViewModel.Draw(graphicsDevice, tileLayoutEffect, AsynchTextureLoad, ColorizeTiles);

                    if (iLevel == DownsamplesToRender.Count - 1 && Viking.UI.State.ShowTileMesh)
                    {
                        tileViewModel.DrawMesh(graphicsDevice, basicEffect);

                        tileViewModel.DrawLabels(this);
                    }

                    iColor++;
                }

                //     if (AllTilesDrawn)
                //         break; 
            }


            if (Viking.UI.State.ShowStosMesh)
            {
                Geometry.Transforms.TriangulationTransform transform = null;

                SectionToVolumeMapping StosMapping = Mapping as SectionToVolumeMapping;
                if (StosMapping != null)
                {
                    transform = StosMapping.VolumeTransform;
                }
                else
                {
                    TileGridToVolumeMapping TGStosMapping = Mapping as TileGridToVolumeMapping;
                    if (TGStosMapping != null)
                    {
                        transform = TGStosMapping.VolumeTransform;
                    }
                }

                if (transform != null)
                {
                    graphicsDevice.ReferenceStencil = int.MaxValue;
                    graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(int.MaxValue);

                    using (TriangulationViewModel stosMeshViewModel = new TriangulationViewModel(transform))
                    {
                        stosMeshViewModel.DrawMesh(graphicsDevice, basicEffect);
                        stosMeshViewModel.DrawLabels(this);
                    }
                }
            }

            tileLayoutEffect.TileColor = new Microsoft.Xna.Framework.Color(1, 1, 1);
            /*
            //Draw the tiles
            
            foreach (Tile tile in TilesToDraw)
            {
                if (tile.HasTexture)
                    tile.Draw(graphicsDevice, DownSample, channelEffect, AsynchTextureLoad);
                else
                {
                    if (AllowedDownsamplesList.Contains(tile.Downsample))
                        tile.Draw(graphicsDevice, DownSample, channelEffect, AsynchTextureLoad);
                }
            }
            
            */
            /*
            if (Viking.UI.State.ShowMesh)
            {
                SectionToVolumeMapping VolMap = Mapping as SectionToVolumeMapping;

                if (VolMap != null)
                {
                    //       VolMap.VolumeTransform.Draw(graphicsDevice, basicEffect); 
                }

                foreach (Tile tile in TilesToDraw)
                {
                    
//                    tile.DrawMesh(graphicsDevice, basicEffect as BasicEffect);
                }

                
            }
                */

            RenderTargetBinding[] renderedTargets = graphicsDevice.GetRenderTargets();

            graphicsDevice.DepthStencilState = originalDepthState;

            graphicsDevice.Textures[0] = null;
            graphicsDevice.SetRenderTargets(null);

            if (renderedTargets == null)
                return null;

            if (renderedTargets.Length > 0)
                return renderedTargets[0].RenderTarget;


            return null;
            
        }

        private Texture DrawSectionsWithChannels(GraphicsDevice graphicsDevice, ChannelInfo[] Channelset, Scene scene, out Texture ChannelOverlay)
        {
            Texture backgroundSection = null;

            List<Texture> renderedSections = new List<Texture>(Channelset.Length - 1);
            List<ChannelInfo> renderedChannels = new List<ChannelInfo>(Channelset.Length - 1);
            //            List<float> renderedAlphas = new List<float>(Channelset.Length - 1);
            //            List<float> renderedBetas = new List<float>(Channelset.Length - 1);
            List<Vector4> renderedChannelColors = new List<Vector4>(Channelset.Length - 1);

            //            int DisplayWidth = graphicsDevice.Viewport.Width;
            //            int DisplayHeight = graphicsDevice.Viewport.Height;

            //            Viewport oldViewport = graphicsDevice.Viewport;
            //            RenderTargetBinding[] oldRenderTargets = graphicsDevice.GetRenderTargets();

            ChannelOverlay = null;

            /*
            BlendState OriginalBlendState = graphicsDevice.BlendState;
            BlendState OverlayBlendState = new BlendState();

            OverlayBlendState.ColorBlendFunction = BlendFunction.Add;
            OverlayBlendState.AlphaBlendFunction = BlendFunction.Add;

            OverlayBlendState.AlphaSourceBlend = Blend.One;
            OverlayBlendState.AlphaDestinationBlend = Blend.Zero;

            OverlayBlendState.ColorSourceBlend = Blend.One;
            OverlayBlendState.ColorDestinationBlend = Blend.Zero;
            */

            string oldMode = State.CurrentMode;

            //Walk through each channel and draw the section
            foreach (ChannelInfo channel in Channelset)
            {
                //Figure out which section we need to load
                Section sectionToDraw = null;
                switch (channel.SectionSource)
                {
                    case ChannelInfo.SectionInfo.SELECTED:
                        sectionToDraw = this.Section.section;
                        break;
                    case ChannelInfo.SectionInfo.ABOVE:
                        sectionToDraw = this.Section.ReferenceSectionAbove;
                        break;
                    case ChannelInfo.SectionInfo.BELOW:
                        sectionToDraw = this.Section.ReferenceSectionBelow;
                        break;
                    case ChannelInfo.SectionInfo.FIXED:
                        int SectionNumber = channel.FixedSectionNumber.Value;
                        if (false == UI.State.volume.SectionViewModels.ContainsKey(SectionNumber))
                            sectionToDraw = null;
                        else
                            sectionToDraw = UI.State.volume.SectionViewModels[SectionNumber].section;

                        break;
                }

                //Can't draw if the section doesn't exist
                if (sectionToDraw == null)
                    continue;

                string ChannelName = channel.ChannelName;
                if (ChannelName.Length == 0)
                {
                    ChannelName = this.CurrentChannel;
                }

                //Find the mapping to use
                MappingBase mapping = MappingManager.GetMapping(this.CurrentVolumeTransform,
                                                                sectionToDraw,
                                                                ChannelName,
                                                                Section.DefaultPyramidTransform);

                if (mapping == null)
                    continue;

                //Change the transform if we need to, but restore it when we are done

                State.CurrentMode = ChannelName;

                //if (channel.Greyscale)
                //{
                tileLayoutEffect.RenderToGreyscale();
                //}
                //else
                //{
                //    tileLayoutEffect.RenderToHSV();
                //Set the color to render with
                //    tileLayoutEffect.TileColor = new Microsoft.Xna.Framework.Color(channel.Color.R,
                //                                                                            channel.Color.G,
                //                                                                            channel.Color.B,
                //                                                                            channel.Color.A);
                //}



                Texture renderTarget = null;
                //                GridRectangle renderTargetBounds = scene.VisibleWorldBounds;

                renderTarget = DrawSection(graphicsDevice, sectionToDraw, channel.ChannelName, scene);

                if (channel.Greyscale)
                {
                    backgroundSection = renderTarget;
                }
                else
                {
                    renderedSections.Add(renderTarget);
                    renderedChannels.Add(channel);
                    renderedChannelColors.Add(new Vector4((float)channel.Color.R / 255f,
                                                          (float)channel.Color.G / 255f,
                                                          (float)channel.Color.B / 255f,
                                                          (float)channel.Color.A / 255f));

                    //  SaveTexture(renderTarget, "D:\\Temp\\" + ChannelName + ".png");
                }
            }

            State.CurrentMode = oldMode;



            graphicsDevice.DepthStencilState = DepthDisabledState;

            //I only support four channels for blending, but I could support eight
            //Merge the rendered channels to a signle RGB image
            Trace.WriteLineIf(renderedChannels.Count > this.mergeHSVImagesEffect.MaxChannels, "Too many channels being rendered, only using the first " + renderedChannels.Count.ToString());

            ChannelOverlay = MergeRGBImages(graphicsDevice, scene, renderedSections.ToArray(), renderedChannelColors.ToArray());

            //Free the textures from the channels
            foreach (RenderTarget2D renderedSection in renderedSections)
            {
                renderedSection.Dispose();
            }

            renderedSections.Clear();
            renderedSections = null;
            /*
            graphicsDevice.BlendState = OriginalBlendState;

            if (OverlayBlendState != null)
            {
                OverlayBlendState.Dispose();
                OverlayBlendState = null; 
            }
            */

            return backgroundSection;
        }

        static BlendState MergeRGBBlendState = null;

        private RenderTarget2D MergeRGBImages(GraphicsDevice graphicsDevice, Scene scene, Texture[] channels, Microsoft.Xna.Framework.Vector4[] Colors)
        {

            this.mergeHSVImagesEffect.MergeRGBImages(channels, Colors);

            //this.mergeHSVImagesEffect.Textures = renderedSections.ToArray();
            //this.mergeHSVImagesEffect.HueAlpha = renderedAlphas.ToArray();
            //this.mergeHSVImagesEffect.HueBeta = renderedBetas.ToArray();

            RenderTarget2D renderOverlayTarget = null;

            BlendState oldBlendState = graphicsDevice.BlendState;

            try
            {
                if (MergeRGBBlendState == null || MergeRGBBlendState.IsDisposed)
                {
                    MergeRGBBlendState = new BlendState();
                    MergeRGBBlendState.AlphaBlendFunction = BlendFunction.Add;
                    MergeRGBBlendState.ColorBlendFunction = BlendFunction.Add;
                    MergeRGBBlendState.AlphaSourceBlend = Blend.One;
                    MergeRGBBlendState.AlphaDestinationBlend = Blend.Zero;
                    MergeRGBBlendState.ColorSourceBlend = Blend.One;
                    MergeRGBBlendState.ColorDestinationBlend = Blend.Zero;
                }

                graphicsDevice.BlendState = MergeRGBBlendState;

                //       AfterFirstTextureBlendState.ColorSourceBlend = Blend.One;
                //       AfterFirstTextureBlendState.ColorDestinationBlend = Blend.One;

                try
                {
                    renderOverlayTarget = new RenderTarget2D(graphicsDevice,
                                                             graphicsDevice.Viewport.Width,
                                                             graphicsDevice.Viewport.Height, false, SurfaceFormat.Rgba64, DepthFormat.None);

                }
                catch (InvalidOperationException)
                {
                    Trace.WriteLine("Could not create render target for channels", "UI");

                    return null;
                }

                //            GridRectangle renderTargetBounds = scene.VisibleWorldBounds;

                //          Debug.Assert(graphicsDevice.Viewport.Width == ClientRectangle.Width); 

                //Create a basic mesh to blend the textures onto the screen

                //  TopRight.X = System.Math.Ceiling(TopRight.X);
                //  TopRight.Y = System.Math.Ceiling(TopRight.Y);
                GridRectangle Bounds = scene.VisibleWorldBounds;
                double HalfWidth = Bounds.Width / 2;
                double HalfHeight = Bounds.Height / 2;
                GridVector2 BotLeft = new GridVector2(Bounds.Center.X - HalfWidth, Bounds.Center.Y + HalfHeight);
                GridVector2 TopRight = new GridVector2(Bounds.Center.X + HalfWidth, Bounds.Center.Y - HalfHeight);
                VertexPositionNormalTexture[] mesh = {
                           new VertexPositionNormalTexture( new Vector3((float)BotLeft.X, (float)BotLeft.Y, 0), Vector3.UnitZ, new Vector2(0,0)),
                           new VertexPositionNormalTexture( new Vector3((float)TopRight.X, (float)BotLeft.Y, 0), Vector3.UnitZ,  new Vector2(1,0)),
                           new VertexPositionNormalTexture( new Vector3((float)BotLeft.X, (float)TopRight.Y, 0), Vector3.UnitZ,   new Vector2(0,1)),
                           new VertexPositionNormalTexture( new Vector3((float)TopRight.X, (float)TopRight.Y, 0), Vector3.UnitZ, new Vector2(1,1))};

                graphicsDevice.SetRenderTargets(renderOverlayTarget);

                graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(0, 0, 0, 0));

                foreach (EffectPass pass in mergeHSVImagesEffect.effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                                                                            mesh, 0, mesh.Length,
                                                                            indicies, 0, indicies.Length / 3);
                }

                graphicsDevice.SetRenderTargets(null);
                //graphicsDevice.Viewport = oldViewport;
                graphicsDevice.Textures[0] = null;
            }
            finally
            {
                graphicsDevice.BlendState = oldBlendState;
            }

            //       SaveTexture(renderOverlayTarget, "D:\\Temp\\MergeRGB.png");

            return renderOverlayTarget;
        }

        private void SaveTexture(Texture2D texture, string filename)
        {
            if (texture == null)
                return;

            System.IO.FileStream saveFile = null;
            try
            {
                saveFile = System.IO.File.OpenWrite(filename);
                texture.SaveAsPng(saveFile, texture.Width, texture.Height);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (saveFile != null)
                    saveFile.Close();

                saveFile = null;
            }
        }


        private void timer_Tick(object sender, EventArgs e)
        {
            //   if (Global.TexturesLoadedNeedRefresh) //TEMP: REMOVE FOR ANIMATIONS
            this.Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.C:
                    if (e.Control == true)
                    {
                        if (Section == null)
                            break;

                        //On Ctrl+C, copy current mouse position to keyboard
                        GridVector2 Pos = StatusPosition;
                        string PosText = Util.CoordinatesToCopyPaste(Pos.X, Pos.Y, Section.Number, Downsample);
                        Clipboard.SetText(PosText);

                    }
                    break;
                case Keys.Space:
                    this.ShowOverlays = false;
                    this.Invalidate();
                    break;
            }

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space:
                    ShowOverlays = true;

                    this.Invalidate();
                    break;
            }

            base.OnKeyUp(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            //Escape cancels the current command and sets the selected item to null
            if (e.KeyChar == (char)Keys.PrintScreen || e.KeyChar == 'z')
            {
                this.CurrentCommand = new Viking.UI.Commands.ScreenCaptureCommand(this);
                e.Handled = true;
            }
        }

        private void SectionViewerControl_MouseDown(object sender, MouseEventArgs e)
        {
            this.Focus();
        }

        private void timerTileCacheCheckpoint_Tick(object sender, EventArgs e)
        {
            if (DrawCallSinceTileCacheCheckpoint)
            {
                //This needs to run on the main thread, otherwise we could delete valid requests because
                //a draw call is in process
                //PORT: Global.TileCache.Checkpoint();
                //Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate() { Global.TileViewModelCache.Checkpoint(); }), null);
                //Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate() { Viking.VolumeModel.Global.TileCache.Checkpoint(); }), null); 

                Action TileViewModelCacheCheckpointAction = Global.TileViewModelCache.Checkpoint;
                Action TileCacheCheckpointAction = Viking.VolumeModel.Global.TileCache.Checkpoint;

                TileViewModelCacheCheckpointAction.BeginInvoke(null, null);
                TileCacheCheckpointAction.BeginInvoke(null, null);
                
                //Global.TileViewModelCache.Checkpoint();
                //Viking.VolumeModel.Global.TileCache.Checkpoint();

                //DrawCallSinceTileCacheCheckpoint = false;
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate() { DrawCallSinceTileCacheCheckpoint = false; }), DispatcherPriority.Background, null);
            }
        }


        private void menuSection_DropDownOpening(object sender, EventArgs e)
        {
            menuSectionUseSpecific.Checked = State.UseSectionSpecificTransform;
            menuSectionShowMesh.Checked = State.ShowStosMesh;
            menuSectionShowTileMesh.Checked = State.ShowTileMesh;

            if (State.volume == null)
                return;

            List<ToolStripItem> items = new List<ToolStripItem>();

            foreach (string t in Section.TilesetNames)
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem(t, null, OnSectionTilesetClick);
                if (t == CurrentTransform)
                    menuItem.Checked = true;

                items.Add(menuItem);
            }

            foreach (string t in Section.ImagePyramids.Keys.ToArray())
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem(t);
                if (t == CurrentTransform)
                    menuItem.Checked = true;

                AddTransformsToSectionChannelPyramidMenuItem(menuItem);
                items.Add(menuItem);
            }

            menuSectionChannel.DropDownItems.Clear();
            menuSectionChannel.DropDownItems.AddRange(items.ToArray());


        }

        private void menuSectionTransform_DropDownOpening(object sender, EventArgs e)
        {

        }

        private void AddTransformsToSectionChannelPyramidMenuItem(ToolStripMenuItem menuChannelPyramid)
        {
            //List all of the pyramid transforms available
            if (Section.PyramidTransformNames.Count < 2)
            {
                //There are no options, or only the default.  Do nothing
                return;
            }
            else
            {
                Debug.Assert(menuChannelPyramid != null);
                if (menuChannelPyramid == null)
                    return;

                List<ToolStripItem> items = new List<ToolStripItem>();
                items.Clear();
                menuChannelPyramid.DropDownItems.Clear();

                foreach (string t in Section.PyramidTransformNames)
                {
                    ToolStripMenuItem menuItem = new ToolStripMenuItem(t, null, OnSectionPyramidTransformClick);
                    if (t == CurrentTransform)
                        menuItem.Checked = true;

                    items.Add(menuItem);
                }

                menuChannelPyramid.DropDownItems.AddRange(items.ToArray());
            }
        }

        /// <summary>
        /// Sets the Section's channel without changing the transform
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSectionChannelPyramidClick(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = sender as ToolStripMenuItem;
            Debug.Assert(menu != null);
            this.CurrentChannel = menu.Text;
            this.CurrentTransform = Section.DefaultPyramidTransform;
        }

        private void OnSectionPyramidTransformClick(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            if (menuItem == null)
                return;

            //Get the parent menu to find the pyramid name
            ToolStripItem menuSectionPyramid = menuItem.OwnerItem;

            CurrentChannel = menuSectionPyramid.Text;
            CurrentTransform = menuItem.Text;
            UI.State.CurrentMode = menuSectionPyramid.Text;

            this.Refresh();
        }

        private void OnSectionTilesetClick(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            if (menuItem == null)
                return;

            CurrentChannel = menuItem.Text;
            CurrentTransform = menuItem.Text;

            this.Refresh();
        }

        private void useSectionSpecificTransformsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            State.UseSectionSpecificTransform = !State.UseSectionSpecificTransform;
        }

        private void menuSectionShowMesh_Click_1(object sender, EventArgs e)
        {
            State.ShowStosMesh = !State.ShowStosMesh;
        }

        private void menuExportFrames_Click(object sender, EventArgs e)
        {
            using (Viking.UI.Forms.FrameCapturesForm form = new FrameCapturesForm())
            {  
                DialogResult result = form.ShowDialog();
                if (result == DialogResult.Cancel)
                    return;

                //Capture each of the requested frames
                using (GenericProgressForm progressForm = new GenericProgressForm())
                {
                    progressForm.Show();

                    //Request the UI to capture each frame
                    for (int i = 0; i < form.Frames.Length; i++)
                    {
                        FrameCapture frame = form.Frames[i];
                        int Z = (int)Math.Round(frame.Z);
                        if (State.volume.SectionViewModels.ContainsKey(Z) == false)
                            continue;
                        // {
                        //   DialogResult mbResult = MessageBox.Show("Could not location section #" + Z.ToString() + " in volume, skipping", "Info", MessageBoxButtons.OKCancel);
                        //   if (mbResult == DialogResult.Cancel)
                        //       break;
                        //   else
                        //       continue;
                        //  }

                        //Keep memory to a minimum when switching sections
                        SectionViewModel S = State.volume.SectionViewModels[Z];
                        this.Section = S;

                        this.ExportImage(frame.Filename, frame.Rect, frame.downsample, frame.IncludeOverlay);
                        progressForm.ShowProgress("Exported frame: " + frame.Filename, (double)i / (double)form.Frames.Length);
                        //System.Windows.Forms.Application.DoEvents();
                        if (progressForm.DialogResult == DialogResult.Cancel)
                            break;
                    }

                    progressForm.Close();
                }
            }
        }

        private void ExportTiles(string ExportPath, int FirstSection, int LastSection, int Downsample = 1)
        {
            //Make sure sections are in order
            if (FirstSection > LastSection)
            {
                int temp = FirstSection;
                FirstSection = LastSection;
                LastSection = temp;
            }

            ExportPath = ExportPath + "/";

            //Capture each of the requested frames
            GenericProgressForm progressForm = new GenericProgressForm();
            progressForm.Show();

            this.AsynchTextureLoad = false;

            bool OriginalOverlay = this.ShowOverlays;
            this.ShowOverlays = false;

            //long OldCacheSize = Global.TextureCache.MaxCacheSize; 
            //Global.TextureCache.MaxCacheSize = (1 << 30);

            //            string OldVolumeTransform = this.CurrentVolumeTransform;

            //            this.CurrentVolumeTransform = null; 

            Scene originalScene = this.Scene;

            foreach (SectionViewModel S in State.volume.SectionViewModels.Values)
            {
                if (S.Number < FirstSection || S.Number > LastSection)
                    continue;

                string Path = ExportPath + S.VolumeViewModel.Name + "/" + S.Number.ToString("D3") + "/Tiles/" + Downsample.ToString("D3") + "/";

                System.IO.DirectoryInfo dirInfo;
                if (System.IO.Directory.Exists(Path) == false)
                    dirInfo = System.IO.Directory.CreateDirectory(Path);
                else
                    dirInfo = new System.IO.DirectoryInfo(Path);

                dirInfo.Attributes = dirInfo.Attributes & ~System.IO.FileAttributes.ReadOnly;

                this.Section = S;

                //                GC.Collect();

                //Get the boundaries of the section
                MappingBase mapping = MappingManager.GetMapping(this.CurrentVolumeTransform, this.Section.section, this.CurrentChannel, this.CurrentTransform);

                //Figure out how much we need to capture
                Size TileImageSize = new Size(256, 256);

                Size TileWorldSize = new Size(TileImageSize.Width * Downsample,
                                              TileImageSize.Height * Downsample);

                //Figure out how many tiles to expect
                Size TileDim = new Size((int)Math.Ceiling(mapping.Bounds.Width / (TileImageSize.Width * Downsample)),
                                        (int)Math.Ceiling(mapping.Bounds.Height / (TileImageSize.Height * Downsample)));

                Scene TileScene = new VikingXNA.Scene(new Viewport(0, 0, TileImageSize.Width, TileImageSize.Height), new Camera());
                TileScene.Camera.Downsample = Downsample;
                this.Scene = TileScene;

                int numTiles = TileDim.Width * TileDim.Height;
                int iTile = 0;
                int MemoryFreeInterval = 1000;
                int EventInterval = (int)Math.Pow(TileDim.Width * TileDim.Height, 1 / 3.0);
                int ExistingTileUpdateInterval = 10000;
                int LoopCounter = 0;
                int NumTilesQueued = 256;

                BmpWriter writer = new BmpWriter(TileImageSize.Width, TileImageSize.Height);
                writer.imageFormat = System.Drawing.Imaging.ImageFormat.Png;

                System.Threading.Tasks.TaskFactory taskFactory = new System.Threading.Tasks.TaskFactory();

                Queue<System.Threading.Tasks.Task> listTasks = new Queue<System.Threading.Tasks.Task>(NumTilesQueued);

                for (int iX = 0; iX < TileDim.Width; iX++)
                {
                    double X = (iX * TileWorldSize.Width) + (TileWorldSize.Width / 2);

                    for (int iY = 0; iY < TileDim.Height; iY++, iTile++)
                    {
                        LoopCounter++;
                        double Y = (iY * TileWorldSize.Height) + (TileWorldSize.Height / 2);

                        TileScene.Camera.LookAt = new Vector2((float)X, (float)Y);

                        string Filename = Path + string.Format("X{0}_Y{1}.png", iX.ToString("D3"), iY.ToString("D3"));

                        //Assume images already on disk are good
                        if (System.IO.File.Exists(Filename))
                        {
                            if (LoopCounter % ExistingTileUpdateInterval == 0)
                            {
                                progressForm.ShowProgress("Section " + S.Name + "\nFrame ID: " + Filename, (double)iTile / (double)numTiles);
                                Application.DoEvents();
                            }

                            continue;
                        }

                        RenderTarget2D renderTargetTile = new RenderTarget2D(Device, TileImageSize.Width, TileImageSize.Height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);

                        this.Draw(TileScene, renderTargetTile);

                        Device.SetRenderTarget(null);

                        System.Threading.Tasks.Task T = taskFactory.StartNew(() =>
                        {
                            //BmpWriter bmpwriter = new BmpWriter(TileImageSize.Width, TileImageSize.Height);
                            //bmpwriter.imageFormat = ImageFormat.Png;
                            //bmpwriter.TextureToBmp(renderTargetTile, Filename); 

                            //renderTargetTile.Dispose();
                            //renderTargetTile = null;
                            BmpWriter.TextureToBmpAsync(renderTargetTile, Filename);
                        });

                        listTasks.Enqueue(T);

                        bool ContinueRemove = false;
                        while (listTasks.Count > NumTilesQueued || ContinueRemove)
                        {
                            ContinueRemove = false;
                            System.Threading.Tasks.Task topTask = listTasks.Dequeue();
                            if (topTask.IsCompleted)
                            {
                                ContinueRemove = true;
                                continue;
                            }
                            else
                            {
                                topTask.Wait();
                            }
                        }

                        //                      BmpWriter.TextureToBmpAsync(renderTargetTile, Filename);

                        if (progressForm.DialogResult == DialogResult.Cancel)
                            break;

                        //Do events once in a while
                        if (LoopCounter % EventInterval == 0)
                        {
                            Application.DoEvents();

                            progressForm.ShowProgress("Section " + S.Name + "\nFrame ID: " + Filename, (double)iTile / (double)numTiles);

                            //System.Windows.Forms.Application.DoEvents();
                            if (progressForm.DialogResult == DialogResult.Cancel)
                                break;
                        }

                        if (LoopCounter >= MemoryFreeInterval)
                        {
                            Trace.WriteLine(Filename);

                            //Global.TextureCache.Checkpoint();
                            // Global.TileViewModelCache.Checkpoint();

                            // Viking.VolumeModel.Global.TileCache.Checkpoint();

                            Global.TextureCache.ReduceCacheFootprint(null);
                            Global.TileViewModelCache.ReduceCacheFootprint(null);
                            Viking.VolumeModel.Global.TileCache.ReduceCacheFootprint(null);

                            //                            GC.Collect();
                            LoopCounter = -1;
                        }
                    }

                    if (progressForm.DialogResult == DialogResult.Cancel)
                        break;

                }

                /*if(renderTargetTile != null)
                {
                    renderTargetTile.Dispose();
                    renderTargetTile = null;
                }
                */
                System.IO.StreamWriter stream = null;
                try
                {
                    string XMLString = string.Format("<?xml version=\"1.0\"?>\n<Level FilePostfix=\".png\" FilePrefix=\"\" Downsample=\"{0}\" TileYDim=\"256\" TileXDim=\"256\" GridDimY=\"{1}\" GridDimX=\"{2}\"/>", Downsample.ToString(), TileDim.Height.ToString(), TileDim.Width.ToString());
                    string XMLPath = Path + string.Format("{0}.xml", S.Number.ToString("D4"));
                    stream = System.IO.File.CreateText(XMLPath);
                    stream.Write(XMLString);
                }
                finally
                {
                    if (stream != null)
                    {
                        stream.Close();
                        stream = null;
                    }

                    this.Scene = originalScene;
                }

                if (progressForm.DialogResult == DialogResult.Cancel)
                    break;

            }

            //Global.TextureCache.MaxCacheSize = OldCacheSize;


            progressForm.Close();

            this.AsynchTextureLoad = true;
            this.ShowOverlays = OriginalOverlay;
        }

        private void menuGoToLocation_Click(object sender, EventArgs e)
        {
            ShowGoToLocationForm();

        }

        protected void ShowGoToLocationForm()
        {
            Viking.UI.Forms.GoToLocationForm form = null;
            try
            {
                form = new GoToLocationForm();
                form.X = Camera.LookAt.X;
                form.Y = Camera.LookAt.Y;

                if (Section != null)
                    form.Z = Section.Number;

                form.Downsample = Downsample;

                DialogResult result = form.ShowDialog();
                if (result == DialogResult.Cancel)
                    return;

                GoToLocation(new Vector2(form.X, form.Y), form.Z, form.Downsample);
            }
            finally
            {
                if (form != null)
                {
                    form.Dispose();
                    form = null;
                }
            }
        }

        public void GoToLocation(Vector2 location, int Z)
        {
            GoToLocation(location, Z, false, this.Downsample);
        }

        public void GoToLocation(Vector2 location, int Z, double newDownsample)
        {
            GoToLocation(location, Z, false, newDownsample);
        }

        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace, double newDownsample)
        {
            if (UI.State.volume.SectionViewModels.ContainsKey(Z) == false)
            {
                MessageBox.Show("There is no section # " + Z.ToString() + " in the volume.", "Error", MessageBoxButtons.OK);
                return;
            }

            SectionViewModel newSection = UI.State.volume.SectionViewModels[Z];
            this.Section = newSection;

            if (InputInSectionSpace)
            {
                MappingBase map = MappingManager.GetMapping(this.CurrentVolumeTransform, this.Section.section, this.CurrentChannel, this.CurrentTransform);
                if (map != null)
                {
                    GridVector2 TransformedPoint;
                    bool Mapped = map.TrySectionToVolume(new GridVector2(location.X, location.Y), out TransformedPoint);

                    if (Mapped)
                    {
                        location.X = (float)TransformedPoint.X;
                        location.Y = (float)TransformedPoint.Y;
                    }
                    else
                    {
                        //MessageBox.Show(this,"The requested point could not be mapped with the current transform.");
                        return;
                    }
                }
            }

            this.Camera.LookAt = new Vector2(location.X, location.Y);
            this.Downsample = (float)newDownsample;
            this.StatusPosition = new GridVector2(location.X, location.Y);
            this.StatusSection = this.Section.Number;
            this.StatusMagnification = newDownsample;

            //Redraw since we are at a new location
            this.Refresh();
        }

        private void menuCaptureScreen_Click(object sender, EventArgs e)
        {
            this.CurrentCommand = new Viking.UI.Commands.ScreenCaptureCommand(this);
        }

        private void SectionViewerControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.G)
            {
                ShowGoToLocationForm();
                e.Handled = true;
            }
        }

        private void menuSetupChannels_Click(object sender, EventArgs e)
        {
            using (SetupChannelsForm ChannelSetup = new SetupChannelsForm(this.Section.VolumeViewModel.DefaultChannels, this.Section.VolumeViewModel.ChannelNames))
            { 
                if (ChannelSetup.ShowDialog() == DialogResult.OK)
                {
                    this.Section.VolumeViewModel.DefaultChannels = ChannelSetup.ChannelInfo;
                    this.Invalidate();
                }
            }

        }

        private void OnVolumeTransformClicked(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            CurrentVolumeTransform = menuItem.Text;
        }

        private void menuVolume_DropDownOpening(object sender, EventArgs e)
        {
            menuVolumeTransforms.DropDownItems.Clear();

            ToolStripMenuItem menuNoneItem = new ToolStripMenuItem("None", null, OnVolumeTransformClicked);
            menuVolumeTransforms.DropDownItems.Add(menuNoneItem);

            foreach (string VolumeTransform in Viking.UI.State.volume.TransformNames)
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem(VolumeTransform, null, OnVolumeTransformClicked);
                menuItem.Checked = CurrentVolumeTransform == VolumeTransform;
                menuVolumeTransforms.DropDownItems.Add(menuItem);
            }
        }

        private void menuColorizeTiles_Click(object sender, EventArgs e)
        {
            menuColorizeTiles.Checked = !menuColorizeTiles.Checked;
        }

        private void menuExportTiles_Click(object sender, EventArgs e)
        {
            using (TileExportForm exportProperties = new TileExportForm())
            {
                if (exportProperties.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                int FirstExportSection;
                int LastExportSection;

                if (exportProperties.ExportAll)
                {
                    FirstExportSection = this.Section.VolumeViewModel.SectionViewModels.First().Key;
                    LastExportSection = this.Section.VolumeViewModel.SectionViewModels.Last().Key;
                }
                else
                {
                    FirstExportSection = exportProperties.FirstSectionInExport;
                    LastExportSection = exportProperties.LastSectionInExport;
                }

                ExportTiles(exportProperties.ExportPath, FirstExportSection, LastExportSection, exportProperties.Downsample);
            }
        }
        
        private void menuSectionShowTileMesh_Click(object sender, EventArgs e)
        {
            State.ShowTileMesh = !State.ShowTileMesh;
        }

        private void menuClearTextureCache_Click(object sender, EventArgs e)
        {
            Cursor originalCursor = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                State.ClearVolumeTextureCache();
                MessageBox.Show(this, "Texture cache was cleared at\n" + State.TextureCachePath, "Success", MessageBoxButtons.OK);
            }
            catch (Exception clearException)
            {
                MessageBox.Show(this, "An exception occurred deleting the cache at\n" + State.TextureCachePath + "\nYou may continue to use Viking but cached files may remain intact.\n" + clearException.Message, "Exception clearing cache", MessageBoxButtons.OK);
            }
            finally
            {
                this.Cursor = originalCursor;
            }
            
        }




    }
}

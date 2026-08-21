using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using Viking.Common;
using Viking.UI.Commands;
using Viking.UI.Forms;
using Viking.ViewModels;
using Viking.VolumeModel;
using VikingXNA;
using VikingXNAGraphics;
using VikingXNAGraphics.Controls;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;


namespace Viking.UI.Controls
{
    public partial class SectionViewerControl : VikingXNAWinForms.ViewerControl, IHelpStrings, IPenEvents, IGestureEvents
    {
        Viking.UI.Commands.Command? _CurrentCommand;
        public Viking.UI.Commands.Command? CurrentCommand
        {
            get => _CurrentCommand;
            set
            {
                if (_CurrentCommand != null)
                {
                    _CurrentCommand.OnCommandCompleteHandler -= this.OnCommandCompleteHandler;
                    _CurrentCommand.UnsubscribeToInterfaceEvents();
                }

                _CurrentCommand = value;
                if (_CurrentCommand as IObservableHelpStrings != null && commandHelpText != null)
                {
                    commandHelpText.DataContext = _CurrentCommand as IObservableHelpStrings;
                    commandHelpText.TextArray = ((IObservableHelpStrings)_CurrentCommand).ObservableHelpStrings;
                    //commandHelpText.TextArrayIndex = 0;
                    //IHelpStrings obj = _CurrentCommand as IHelpStrings;
                    //commandHelpText.TextArray = obj.HelpStrings;
                    //    commandHelpText.DataContext = _CurrentCommand as IHelpStrings;
                }

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

        public Viking.UI.Commands.CommandQueue CommandQueue = new();

        static readonly short[] indicies = [0, 1, 2, 2, 1, 3];

        CommandCompleteEventHandler OnCommandCompleteHandler;

        ISectionOverlayExtension[] listOverlays = [];

        /// <summary>
        /// Overlay that displays section numbers with smooth scrolling animation
        /// </summary>
        private SectionNumberOverlayView? sectionNumberOverlay;

        public VertexDeclaration VertexPositionColorDeclaration;

        /// <summary>
        /// The tile cache checkpoints aborts unwanted requests, but since we find out if tiles are
        /// wanted during draw calls we don't want to run a checkpoint unless there has been a draw
        /// call since the last checkpoint
        /// </summary>
        private bool DrawCallSinceTileCacheCheckpoint = false;

        /// <summary>
        /// Host form whose WindowState drives paint-timer pause while minimized.
        /// </summary>
        private Form? _hostForm;

        /// <summary>
        /// When set to true Commands and ISectionOverlayExtension draw methods are called
        /// </summary>
        public bool ShowOverlays = true;

        /// <summary>
        /// When true, only the overlays are rendered and section images are hidden
        /// </summary>
        public bool ShowOnlyOverlays = false;


        public bool ColorizeTiles
        {
            get => menuColorizeTiles.Checked;
            set => menuColorizeTiles.Checked = value;
        }


        //A friendlier way of setting camera distance
        public override double Downsample
        {
            get => base.Downsample;
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
        private readonly System.Windows.Forms.StatusStrip StatusBar;

        protected System.Windows.Forms.ToolStripItem tsPosition;
        protected System.Windows.Forms.ToolStripItem tsSection;
        protected System.Windows.Forms.ToolStripItem tsMagnification;
        protected System.Windows.Forms.ToolStripItem tsChannels;

        private Geometry.Vector2 _StatusPosition;

        public Geometry.Vector2 StatusPosition
        {
            get => _StatusPosition;
            set
            {
                if (value.Round(0) != _StatusPosition.Round(0))
                    tsPosition.Text = $"X: {value.X:F0} Y: {value.Y:F0}";

                _StatusPosition = value;
            }
        }

        public int StatusSection
        {
            set => tsSection.Text = "Section: " + value.ToString();
        }

        private double _Magnification = 0;

        public double StatusMagnification
        {
            get => _Magnification;

            set
            {
                _Magnification = value;
                if (tsMagnification != null)
                    tsMagnification.Text = "Magnification: " + value.ToString("F2");
            }
        }

        private readonly List<ToolStripItem> _StatusChannels = [];
        internal ChannelInfo[] StatusChannels
        {
            set
            {
                if (value is null || value.Length == 0)
                {
                    value = [new()];
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
                    tsChannelItem.ForeColor = color.R == 255 &&
                        color.G == 255 &&
                        color.B == 255
                        ? System.Drawing.Color.Black
                        : value[i].FormColor;
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

        public VolumeViewModel Volume => _Section is null ? throw new InvalidOperationException("Section is not set.") : _Section.VolumeViewModel;

        private SectionViewModel? _Section;

        /// <summary>
        /// The section we are currently viewing
        /// </summary>
        public SectionViewModel Section
        {
            get => _Section;
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
                    OldSection.VolumeViewModel.TransformChanged -= this.OnVolumeTransformChanged;
                    OldSection.TransformChanged -= this.OnSectionTransformChanged;
                    OldSection.PropertyChanged -= this.OnSectionPropertyChanged;
                }

                _Section = value;
                InvalidateSectionTextureCache();
                if (_Section != null)
                {
                    //NOTE: We have to update the section before we ask for the reference section
                    if (State.UseSectionSpecificTransform == false && oldtransform != null)
                        this.CurrentTransform = oldtransform;

                    this.StatusSection = _Section.Number;

                    this._Section.OnReferenceSectionChanged += this.InternalReferenceSectionChanged;
                    this._Section.VolumeViewModel.TransformChanged += this.OnVolumeTransformChanged;
                    this._Section.TransformChanged += this.OnSectionTransformChanged;
                    this._Section.PropertyChanged += this.OnSectionPropertyChanged;

                    //Figure out if the new section supports the current channel
                    if (_Section.Channels.Contains(this.CurrentChannel) == false)
                        CurrentChannel = _Section.DefaultChannel;
                }

                if (_Section != null && State.volume != null)
                {
                ///Find the adjacent sections and request them to warp into volume space if they haven't already
                { 
                    SortedList<int, SectionViewModel> sections = UI.State.volume.SectionViewModels;
                    int iSection = sections.IndexOfKey(this._Section.Number);
                    int iSectionAbove = iSection + 1;
                    int iSectionBelow = iSection - 1;
                    if (State.UseSectionSpecificTransform == false && oldtransform != null)
                    { 
                        if (iSectionAbove < sections.Count)
                        {
                            _ = sections.Values[iSectionAbove].PrepareTransform(oldtransform);
                        }

                        if (iSectionBelow >= 0)
                        {
                            _ = sections.Values[iSectionBelow].PrepareTransform(oldtransform);
                        } 
                    }

                    if (Viking.Properties.Settings.Default.LoadAdjacentSectionTextures &&
                        this.Scene != null && this.graphicsDeviceService?.GraphicsDevice != null && State.volume != null)
                    {
                        var scene = this.Scene;
                        var token = CancellationToken.None;
                        _ = Task.Run(async () =>
                        {
                            if (iSectionAbove < sections.Count)
                            {
                                var secAbove = sections.Values[iSectionAbove];
                                await QueueTextureLoadsForSectionAsync(scene, secAbove.Number, highestResolutionOnly: true, token);
                            }
                            if (iSectionBelow >= 0)
                            {
                                var secBelow = sections.Values[iSectionBelow];
                                await QueueTextureLoadsForSectionAsync(scene, secBelow.Number, highestResolutionOnly: true, token);
                            }
                        });
                    }
                }

                if(!HavePaintInQueue())
                    this.Invalidate();

                // Update the section number overlay with the new section
                UpdateSectionNumberOverlay();

                // Cancel in-flight mapping initializations only for sections that are not the current section or adjacent to it (so adjacent sections keep loading)
                int currentSectionNumber = _Section.Number;
                int[] adjacentSectionNumbers = GetAdjacentSectionNumbers(currentSectionNumber);
                lock (_sectionMappingInitLock)
                {
                    List<int> toRemove = new();
                    foreach (var kv in _sectionMappingInitBySection)
                    {
                        if (Array.IndexOf(adjacentSectionNumbers, kv.Key) < 0)
                        {
                            kv.Value.Cancel();
                            kv.Value.Dispose();
                            toRemove.Add(kv.Key);
                        }
                    }
                    int cachedSection = Interlocked.CompareExchange(ref _lastInitSectionNumber, -1, -1);
                    foreach (int key in toRemove)
                    {
                        _sectionMappingInitBySection.Remove(key);
                        _sectionMappingInitTasks.Remove(key);
                        if (key == cachedSection)
                        {
                            Interlocked.Exchange(ref _lastInitSectionNumber, -1);
                            Interlocked.Exchange(ref _lastInitTask, null);
                        }
                    }

                    // Cancel texture-load tokens for sections not current or adjacent (semaphore waiters will be cancelled; in-flight loads continue)
                    List<int> textureLoadToRemove = new();
                    foreach (var kv in _sectionTextureLoadCts)
                    {
                        if (Array.IndexOf(adjacentSectionNumbers, kv.Key) < 0)
                        {
                            kv.Value.Cancel();
                            kv.Value.Dispose();
                            textureLoadToRemove.Add(kv.Key);
                        }
                    }
                    foreach (int key in textureLoadToRemove)
                        _sectionTextureLoadCts.Remove(key);

                    // Start initializing the new section's tile mapping immediately so the first draw has a chance to show content instead of staying black
                    if (State.volume != null)
                    {
                        MappingBase mapping = State.volume.GetTileMapping(_Section.Number, this.CurrentChannel, this.CurrentTransform);
                        if (mapping != null && !mapping.Initialized)
                        {
                            StartMappingInitIfNeeded(currentSectionNumber, mapping);
                        }
                    }
                }

                //Let listeners know if we changed sections
                if (OnSectionChangedEventInvokeTask is not null && !(OnSectionChangedEventInvokeTask.IsCompleted || OnSectionChangedEventInvokeTask.IsFaulted))
                {
                    OnSectionChangedEventCancellationTokenSource.Cancel();
                }

                OnSectionChangedEventCancellationTokenSource = new CancellationTokenSource();

                OnSectionChangedEventInvokeTask = Task.Run(() => OnSectionChanged?.Invoke(this, new SectionChangedEventArgs(_Section, OldSection), OnSectionChangedEventCancellationTokenSource.Token), OnSectionChangedEventCancellationTokenSource.Token);
                //OnSectionChanged?.(this, new SectionChangedEventArgs(_Section, OldSection));

                if (this.Scene != null && _Section != null)
                {
                    TextureRequestQueue.SortByPriority(this.Scene.VisibleWorldBounds, _Section.Number);
                    PendingTextureQueue.SortByVisibility(this.Scene.VisibleWorldBounds, _Section.Number);
                }
                }
            }
        }

        /// <summary>
        /// Currently selected tileset 
        /// </summary>
        [System.ComponentModel.Browsable(false)]
        public string CurrentChannel
        {
            get => Section?.ActiveChannel;
            set { if (Section is null) return; Section.ActiveChannel = value; }
        }

        [System.ComponentModel.Browsable(false)]
        public string CurrentTransform
        {
            get => Section?.ActiveTileTransform;
            set { if (Section is null) return; Section.ActiveTileTransform = value; }
        }

        public ChannelInfo[] CurrentChannelset
        {
            get
            {
                if (Section is null)
                    return [];

                ChannelInfo[] Channelset = Section.ChannelInfoArray;
                if (Channelset.Length == 0)
                {
                    //See if there are any global channel settings
                    Channelset = Section.VolumeViewModel.DefaultChannels;
                }

                return Channelset;
            }
        }

        private ElementHost commandHelpTextScrollerHost;
        private Viking.UI.WPF.StringArrayAutoScroller commandHelpText;

        private PenEventManager penEventManager;
        private GestureEventManager gestureEventManager;

        public SectionViewerControl()
        {
            InitializeComponent();

            CreateWPFControls();

            StatusBar = new System.Windows.Forms.StatusStrip
            {
                Parent = this,
                Dock = System.Windows.Forms.DockStyle.Bottom
            };

            tsSection = new System.Windows.Forms.ToolStripLabel("Section: ");
            tsPosition = new System.Windows.Forms.ToolStripLabel("Position: ");
            tsMagnification = new System.Windows.Forms.ToolStripLabel("Zoom: ");
            tsChannels = new System.Windows.Forms.ToolStripLabel("Channels: ");

            StatusBar.Items.Add(tsSection);
            StatusBar.Items.Add(tsPosition);
            StatusBar.Items.Add(tsMagnification);
            StatusBar.Items.Add(tsChannels);

            ObjectSelectedHandler = new Viking.Common.ObjectSelectedEventHandler(this.OnSelectedItemChanged);
            InternalReferenceSectionChanged = new ReferenceSectionChangedEventHandler(this.OnInternalReferenceSectionChanged);
            State.ItemSelected += ObjectSelectedHandler;

            ExtensionManager.AddMenuItems(this.menuStrip);
            CommandQueue.OnCommandInjected += this.OnCommandInjected;
            CommandQueue.OnQueueChanged += this.OnCommandQueueChanged;
            PendingTextureQueue.QueueBecameEmpty += this.OnPendingTextureQueueBecameEmpty;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SubscribeHostForm();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnsubscribeHostForm();
            base.OnHandleDestroyed(e);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            SubscribeHostForm();
        }

        private void SubscribeHostForm()
        {
            Form? form = FindForm();
            if (form == _hostForm)
                return;

            UnsubscribeHostForm();
            _hostForm = form;
            if (_hostForm != null)
                _hostForm.Resize += OnHostFormResize;

            UpdatePaintTimerForWindowState();
        }

        private void UnsubscribeHostForm()
        {
            if (_hostForm is null)
                return;

            _hostForm.Resize -= OnHostFormResize;
            _hostForm = null;
        }

        private void OnHostFormResize(object? sender, EventArgs e) => UpdatePaintTimerForWindowState();

        /// <summary>
        /// Pause continuous repaint while minimized; resume and invalidate when restored.
        /// </summary>
        private void UpdatePaintTimerForWindowState()
        {
            Form? form = _hostForm ?? FindForm();
            if (form is null)
                return;

            if (form.WindowState == FormWindowState.Minimized)
            {
                timer.Enabled = false;
                return;
            }

            if (!timer.Enabled)
            {
                timer.Enabled = true;
                Invalidate();
            }
        }

        /// <summary>
        /// Unsubscribe from events and cancel/dispose all section-related cancellation token sources. Call from Dispose to avoid callbacks after disposal.
        /// </summary>
        private void UnsubscribeAndCancelTokens()
        {
            State.ItemSelected -= ObjectSelectedHandler;
            CommandQueue.OnCommandInjected -= this.OnCommandInjected;
            CommandQueue.OnQueueChanged -= this.OnCommandQueueChanged;
            PendingTextureQueue.QueueBecameEmpty -= this.OnPendingTextureQueueBecameEmpty;
            UnsubscribeHostForm();

            OnSectionChangedEventCancellationTokenSource?.Cancel();
            OnSectionChangedEventCancellationTokenSource?.Dispose();
            OnSectionChangedEventCancellationTokenSource = null;

            lock (_sectionMappingInitLock)
            {
                foreach (var kv in _sectionMappingInitBySection)
                {
                    kv.Value.Cancel();
                    kv.Value.Dispose();
                }
                _sectionMappingInitBySection.Clear();
                _sectionMappingInitTasks.Clear();

                foreach (var kv in _sectionTextureLoadCts)
                {
                    kv.Value.Cancel();
                    kv.Value.Dispose();
                }
                _sectionTextureLoadCts.Clear();
            }

            Section = null;
        }

        private void CreateWPFControls()
        {
            commandHelpTextScrollerHost = new ElementHost
            {
                TabStop = false,
                Dock = DockStyle.Bottom,
                Visible = Viking.Properties.Settings.Default.ShowCommandHelp,
                Parent = this
            };
            menuShowCommandHelp.Checked = Viking.Properties.Settings.Default.ShowCommandHelp;
            timerHelpTextChange.Enabled = Viking.Properties.Settings.Default.ShowCommandHelp;

            this.Controls.Add(commandHelpTextScrollerHost);

            try
            {
                commandHelpText = new Viking.UI.WPF.StringArrayAutoScroller
                {
                    DataContext = this.CurrentCommand as IHelpStrings
                };

                //commandHelpText.TextArray = new String[] { "Hello", "world" };
                //commandHelpText.InitializeComponent();
                commandHelpTextScrollerHost.Child = commandHelpText;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Could not create command help text control: " + ex.Message, "UI");
            }

            commandHelpTextScrollerHost.Height /= 2;
        }
        /*
        public override bool PreProcessMessage(ref Message msg)
        {

            return base.PreProcessMessage(ref msg);
        }
        */

        protected override void WndProc(ref Message msg)
        {
            /*
            switch (msg.Msg)
            {
                case Touch.WM_TOUCHHITTESTING:
                    Touch.LogPenData(msg, "TouchHitTesting");
                    break;
                case Touch.WM_POINTERDEVICEINRANGE:
                    Touch.LogPenData(msg, "PointerDeviceInRange");
                    break;
                case Touch.WM_POINTERDEVICEOUTOFRANGE:
                    Touch.LogPenData(msg, "PointerDeviceOutOfRange");
                    break;
                case Touch.WM_POINTERUPDATE:
                    Touch.LogPenData(msg, "PointerUpdate");
                    break;
                case Touch.WM_POINTERDOWN:
                    Touch.LogPenData(msg, "PointerDown");
                    break;
                case Touch.WM_POINTERUP:
                    Touch.LogPenData(msg, "PointerUp");
                    break; 
                default:
                    break;
            }
            */

            if (penEventManager != null && penEventManager.ProcessPenMessages(ref msg))
            {
                uint pointerID = WinMsgInput.GetPointerID(msg.WParam);
                PointerMessageData pointerState = new(msg);
                WinMsgInput.GetPointerType((uint)pointerID, out PointerType type);
                //bool isPen = Touch.IsPenEvent(out uint pointerID);
                //if(isPen)
                if (type == PointerType.Pen)
                {
                    //Trace.WriteLine(string.Format("Pen Input {0}", pointerID));
                    return;
                }
                else
                {
                    //Trace.WriteLine(string.Format("Mouse Input {0}", pointerID));
                }
                //Returning here prevents mouse events being sent for pen actions... this is double edged.  Windows will still 
                //sends the events but they always appear to come from the mouse.  However duplicate events are not sent.      
                //return;
            }
            else
            {
                //Trace.WriteLine(string.Format("{0}", msg.Msg));
            }

            if (gestureEventManager != null && gestureEventManager.ProcessGestureMessages(ref msg)) // || msg.Msg == WinMsgInput.WM_GESTURENOTIFY)
            {
                return; //Message is handled
                /*
                GestureSupport.ProcessGestureMessage(ref msg, out GestureInfo info);
                if(info.Gesture == Gesture.Zoom)
                {

                }
                */
            }

            if (msg.Msg == WinMsgInput.WM_LBUTTONDOWN || msg.Msg == WinMsgInput.WM_RBUTTONDOWN)
            {
                //bool isPen = Touch.IsPenEvent(out uint pointerID);
                if (WinMsgInput.IsPenEvent(out uint PointerID))
                {
                    Trace.WriteLine($"Pen button down {PointerID}");
                }
                else
                {
                    Trace.WriteLine($"Mouse button down {PointerID}");
                }
            }

            /*if (msg.Msg == Touch.WM_MOUSEMOVE)
            {
                bool isPen = Touch.IsPenEvent(out uint pointerID);
                if (Touch.IsPenEvent(out uint PointerID))
                {
                    Trace.WriteLine(string.Format("Pen move {0}", PointerID));
                }
                else
                {
                    Trace.WriteLine(string.Format("Mouse move {0}", PointerID));
                }
            }*/


            base.WndProc(ref msg);
            return;
        }


        protected override void Initialize()
        {
            if (!DesignMode)
            {
                this.menuStrip.Parent = this.Parent;

                penEventManager = new PenEventManager(this);
                gestureEventManager = new GestureEventManager(this);

                OnCommandCompleteHandler = new Viking.Common.CommandCompleteEventHandler(this.OnCommandCompleted);

                ActivateNextCommandFromQueue();

                if (Section != null)
                    this.CurrentChannel = Section.DefaultChannel;

                this.listOverlays = ExtensionManager.CreateSectionOverlays(this);

                // Initialize the section number overlay
                InitializeSectionNumberOverlay();
            }

            base.Initialize();
        }


        #region Event Handlers

        /// <summary>
        /// Fired when an object is selected in the UI
        /// </summary>
        readonly Viking.Common.ObjectSelectedEventHandler ObjectSelectedHandler;

        /// <summary>
        /// Fires when a different section is displayed
        /// </summary>
        public event SectionChangedEventHandler OnSectionChanged;

        /// <summary>
        /// This token is used to cancel a previous section change notification if we change sections again before the first is done processing
        /// </summary>
        private CancellationTokenSource? OnSectionChangedEventCancellationTokenSource = null;

        private Task? OnSectionChangedEventInvokeTask = null;

        /// <summary>
        /// Per-section cancellation for tile mapping initialization. When Section changes we cancel only initializations for sections that are not the current section or adjacent to it.
        /// </summary>
        private readonly Dictionary<int, CancellationTokenSource> _sectionMappingInitBySection = new();
        private readonly object _sectionMappingInitLock = new();
        private readonly Dictionary<int, Task> _sectionMappingInitTasks = new();
        private int _lastInitSectionNumber = -1;
        private Task? _lastInitTask;

        /// <summary>
        /// Per-section cancellation for texture loading. When Section changes we cancel only texture-load tokens for sections that are not the current section or adjacent to it. Semaphore waiters are cancelled; in-flight loads continue.
        /// </summary>
        private readonly Dictionary<int, CancellationTokenSource> _sectionTextureLoadCts = new();

        /// <summary>
        /// Cache key for section texture. Cache is valid when current scene/section state matches this key.
        /// </summary>
        private readonly struct SectionTextureCacheKey : IEquatable<SectionTextureCacheKey>
        {
            public readonly int SectionNumber;
            public readonly Geometry.Rectangle VisibleWorldBounds;
            public readonly int ViewportWidth;
            public readonly int ViewportHeight;
            public readonly double CameraDownsample;
            public readonly string ChannelKey;
            public readonly string CurrentTransform;
            public readonly bool ColorizeTiles;
            public readonly bool ShowTileMesh;
            public readonly bool ShowStosMesh;

            public SectionTextureCacheKey(int sectionNumber, Geometry.Rectangle visibleWorldBounds, int viewportWidth, int viewportHeight,
                double cameraDownsample, string channelKey, string currentTransform, bool colorizeTiles, bool showTileMesh, bool showStosMesh)
            {
                SectionNumber = sectionNumber;
                VisibleWorldBounds = visibleWorldBounds;
                ViewportWidth = viewportWidth;
                ViewportHeight = viewportHeight;
                CameraDownsample = cameraDownsample;
                ChannelKey = channelKey ?? "";
                CurrentTransform = currentTransform ?? "";
                ColorizeTiles = colorizeTiles;
                ShowTileMesh = showTileMesh;
                ShowStosMesh = showStosMesh;
            }

            public bool Equals(SectionTextureCacheKey other) =>
                SectionNumber == other.SectionNumber &&
                VisibleWorldBounds.Equals(other.VisibleWorldBounds) &&
                ViewportWidth == other.ViewportWidth &&
                ViewportHeight == other.ViewportHeight &&
                CameraDownsample == other.CameraDownsample &&
                ChannelKey == other.ChannelKey &&
                CurrentTransform == other.CurrentTransform &&
                ColorizeTiles == other.ColorizeTiles &&
                ShowTileMesh == other.ShowTileMesh &&
                ShowStosMesh == other.ShowStosMesh;

            public override bool Equals(object obj) => obj is SectionTextureCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + SectionNumber;
                    hash = hash * 31 + VisibleWorldBounds.GetHashCode();
                    hash = hash * 31 + ViewportWidth;
                    hash = hash * 31 + ViewportHeight;
                    hash = hash * 31 + CameraDownsample.GetHashCode();
                    hash = hash * 31 + (ChannelKey?.GetHashCode() ?? 0);
                    hash = hash * 31 + (CurrentTransform?.GetHashCode() ?? 0);
                    hash = hash * 31 + ColorizeTiles.GetHashCode();
                    hash = hash * 31 + ShowTileMesh.GetHashCode();
                    hash = hash * 31 + ShowStosMesh.GetHashCode();
                    return hash;
                }
            }
        }

        private RenderTarget2D? _cachedSectionTexture;
        private Texture2D? _cachedChannelOverlay;
        private SectionTextureCacheKey _sectionTextureCacheKey;

        private static string BuildChannelKeyForCache(ChannelInfo[] channelset)
        {
            if (channelset is null || channelset.Length == 0)
                return "";
            return string.Join("|", channelset.Select(c => $"{c.ChannelName}:{c.Color.R},{c.Color.G},{c.Color.B},{c.Color.A}"));
        }

        private SectionTextureCacheKey BuildSectionTextureCacheKey(Scene scene, ChannelInfo[] channelset)
        {
            return new SectionTextureCacheKey(
                Section!.section.Number,
                scene.VisibleWorldBounds,
                scene.Viewport.Width,
                scene.Viewport.Height,
                scene.Camera.Downsample,
                channelset.Length == 1 ? (CurrentChannel ?? "") : BuildChannelKeyForCache(channelset),
                CurrentTransform ?? "",
                ColorizeTiles,
                Viking.UI.State.ShowTileMesh,
                Viking.UI.State.ShowStosMesh);
        }

        private void InvalidateSectionTextureCache()
        {
            _cachedSectionTexture?.Dispose();
            _cachedSectionTexture = null;
            _cachedChannelOverlay?.Dispose();
            _cachedChannelOverlay = null;
        }

        /// <summary>
        /// Returns the section numbers for the current section and up to two sections above and below in list order.
        /// Uses the volume's section list so adjacent sections are correct when section numbers have gaps.
        /// </summary>
        private int[] GetAdjacentSectionNumbers(int currentSectionNumber)
        {
            if (State.volume == null)
                return [currentSectionNumber];

            SortedList<int, SectionViewModel> sections = State.volume.SectionViewModels;
            int iSection = sections.IndexOfKey(currentSectionNumber);
            if (iSection < 0)
                return [currentSectionNumber];

            int iMin = Math.Max(0, iSection - 2);
            int iMax = Math.Min(sections.Count - 1, iSection + 2);
            int count = iMax - iMin + 1;
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = sections.Keys[iMin + i];
            return result;
        }

        /// <summary>
        /// Gets or creates a cancellation token for the given section's texture loading. Used from draw path; only non-adjacent section tokens are cancelled when Section changes.
        /// </summary>
        private CancellationToken GetOrCreateSectionTextureLoadToken(int sectionNumber)
        {
            lock (_sectionMappingInitLock)
            {
                if (_sectionTextureLoadCts.TryGetValue(sectionNumber, out var cts) && !cts.IsCancellationRequested)
                    return cts.Token;
                cts?.Dispose();
                var newCts = new CancellationTokenSource();
                _sectionTextureLoadCts[sectionNumber] = newCts;
                return newCts.Token;
            }
        }

        /// <summary>
        /// Gets or creates a cancellation token for the given section's mapping initialization. Used from draw path when starting init; only non-adjacent in-flight inits are cancelled when Section changes.
        /// </summary>
        private CancellationToken GetOrCreateSectionMappingInitToken(int sectionNumber)
        {
            lock (_sectionMappingInitLock)
            {
                if (_sectionMappingInitBySection.TryGetValue(sectionNumber, out var cts) && !cts.IsCancellationRequested)
                    return cts.Token;
                cts?.Dispose();
                var newCts = new CancellationTokenSource();
                _sectionMappingInitBySection[sectionNumber] = newCts;
                return newCts.Token;
            }
        }

        /// <summary>
        /// Starts mapping initialization for the given section if one is not already running.
        /// Uses the existing per-section CTS for cancellation.
        /// </summary>
        private void StartMappingInitIfNeeded(int sectionNumber, MappingBase mapping)
        {
            // Lock-free fast path: if init already in flight for this section, skip the lock.
            int cachedSection = Interlocked.CompareExchange(ref _lastInitSectionNumber, -1, -1);
            Task? cachedTask = Interlocked.CompareExchange(ref _lastInitTask, null, null);
            if (sectionNumber == cachedSection && cachedTask != null && !cachedTask.IsCompleted)
                return;

            lock (_sectionMappingInitLock)
            {
                if (_sectionMappingInitTasks.TryGetValue(sectionNumber, out var existing)
                    && !existing.IsCompleted)
                    return; // Already in flight

                var token = GetOrCreateSectionMappingInitToken(sectionNumber);
                Task task = Task.Run(() => mapping.Initialize(token), token);
                _sectionMappingInitTasks[sectionNumber] = task;
                Interlocked.Exchange(ref _lastInitSectionNumber, sectionNumber);
                Interlocked.Exchange(ref _lastInitTask, task);


                _ = task.ContinueWith(t =>
                {
                    bool initOk = mapping.Initialized;
                    bool cancelled = token.IsCancellationRequested;

                    if (initOk && !cancelled && !t.IsFaulted && !IsDisposed)
                    {
                        try
                        {
                            if (InvokeRequired)
                                BeginInvoke(new Action(() => { if (!IsDisposed) Invalidate(); }));
                            else
                                Invalidate();
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }
                }, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Fires when one of the reference sections has changed
        /// </summary>
        public event ReferenceSectionChangedEventHandler? OnReferenceSectionChanged;

        #region IPenEvents 
        public event PenEventHandler OnPenEnterRange
        {
            add => penEventManager.OnPenEnterRange += value;
            remove => penEventManager.OnPenEnterRange -= value;
        }
        public event PenEventHandler OnPenLeaveRange
        {
            add => penEventManager.OnPenLeaveRange += value;
            remove => penEventManager.OnPenLeaveRange -= value;
        }
        public event PenEventHandler OnPenContact
        {
            add => penEventManager.OnPenContact += value;
            remove => penEventManager.OnPenContact -= value;
        }
        public event PenEventHandler OnPenLeaveContact
        {
            add => penEventManager.OnPenLeaveContact += value;
            remove => penEventManager.OnPenLeaveContact -= value;
        }
        public event PenEventHandler OnPenMove
        {
            add => penEventManager.OnPenMove += value;
            remove => penEventManager.OnPenMove -= value;
        }
        #endregion

        #region IGestureEvents
        public event PanGestureEventHandler OnGesturePan
        {
            add => gestureEventManager.OnGesturePan += value;
            remove => gestureEventManager.OnGesturePan -= value;
        }
        public event ZoomGestureEventHandler OnGestureZoom
        {
            add => gestureEventManager.OnGestureZoom += value;
            remove => gestureEventManager.OnGestureZoom -= value;
        }
        public event BeginGestureEventHandler OnGestureBegin
        {
            add => gestureEventManager.OnGestureBegin += value;
            remove => gestureEventManager.OnGestureBegin -= value;
        }
        public event EndGestureEventHandler OnGestureEnd
        {
            add => gestureEventManager.OnGestureEnd += value;
            remove => gestureEventManager.OnGestureEnd -= value;
        }

        public event PenEventHandler OnPenButtonDown
        {
            add => ((IPenEvents)penEventManager).OnPenButtonDown += value;

            remove => ((IPenEvents)penEventManager).OnPenButtonDown -= value;
        }

        public event PenEventHandler OnPenButtonUp
        {
            add => ((IPenEvents)penEventManager).OnPenButtonUp += value;

            remove => ((IPenEvents)penEventManager).OnPenButtonUp -= value;
        }
        #endregion

        /// <summary>
        /// Called when the reference section for the current section has changed. 
        /// Fires our public ReferenceSectionChanged event
        /// </summary>
        private readonly ReferenceSectionChangedEventHandler InternalReferenceSectionChanged;

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
        private void OnInternalReferenceSectionChanged(object sender, ReferenceSectionChangedEventArgs e) => OnReferenceSectionChanged?.Invoke(sender, e);

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
            this.CurrentCommand = null;
            this.ActivateNextCommandFromQueue();
            this.Invalidate();
        }

        /// <summary>
        /// Activates when the user adds a command to the command queue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPendingTextureQueueBecameEmpty()
        {
            this.Invalidate();
        }

        private void OnCommandQueueChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                if (this.CurrentCommand is null || this.CurrentCommand is DefaultCommand)
                {
                    this.ActivateNextCommandFromQueue();
                }
            }
        }

        /// <summary>
        /// Activates when the user injects a new command to the front of the command queue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCommandInjected(object sender, CommandInjectedEventHandler e)
        {
            Command ActiveCommand = this.CurrentCommand;
            CurrentCommand = e.injectedCommand;

            if (e.SaveCurrentCommand == true && ActiveCommand is not DefaultCommand && ActiveCommand != null)
            {
                CommandQueue.Push(ActiveCommand);
            }
        }

        private void ActivateNextCommandFromQueue() => this.CurrentCommand = this.CommandQueue.Pop() ?? new DefaultCommand(this);

        #endregion

        /// <summary>
        /// Need to enable arrow keys as input keys
        /// </summary>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool IsInputKey(Keys keyData)
        {
            return keyData switch
            {
                Keys.Right or Keys.Left or Keys.Up or Keys.Down => true,
                Keys.Shift | Keys.Right or Keys.Shift | Keys.Left or Keys.Shift | Keys.Up or Keys.Shift | Keys.Down => true,
                _ => base.IsInputKey(keyData),
            };
        }

        public string[] ExtensionOverlayTitles()
        {
            if (this.listOverlays.Length == 0)
                return [];

            List<string> names = new(this.listOverlays.Length);

            foreach (ISectionOverlayExtension IOverlay in this.listOverlays)
            {
                string name = IOverlay.Name();
                if (name is null)
                    continue;
                if (name.Length == 0)
                    continue;

                names.Add(name);
            }

            return [.. names];
        }

        /// <summary>
        /// Swaps the current section for the section above it
        /// </summary>
        public void StepUpNSections(int nSections)
        {
            if (Section is null || State.volume == null)
                return;
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
            if (Section is null || State.volume == null)
                return;
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

        public async Task ExportImage(string Filename, Geometry.Rectangle MyRect, int Z, double Downsample, bool IncludeOverlays)
        {
            Debug.Assert(MyRect.Left < MyRect.Right);
            Debug.Assert(MyRect.Bottom < MyRect.Top);

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

            Camera camera = new()
            {
                Downsample = Downsample
            };

            //Figure out if we can do the entire shot at once or have to divide it up
            if (CapturedTileSizeX <= 2048 && CapturedTileSizeX <= 2048)
            {

            }
            else
            {
                //The dimensions of a single cell in our captureg rid
                int TileCaptureMaxSize = Device.Adapter.IsProfileSupported(GraphicsProfile.HiDef) ? 4096 : 2048;

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


            List<Task> listTasks = [];
            int MaxActiveExports = 2;
            {
                GraphicsDevice graphicsDevice = this.graphicsDeviceService.GraphicsDevice;

                //Figure out how to cut the rectangle into 512x512 cells and take the screenshots
                for (int iRow = 0; iRow < numTilesY; iRow++)
                {
                    double Y = AdjustedWorldY + (iRow * WorldTileSizeY);

                    for (int iCol = 0; iCol < numTilesX; iCol++)
                    {
                        //Figure out the rectangle we need to capture at this location
                        double X = AdjustedWorldX + (iCol * WorldTileSizeX);

                        VikingXNA.Scene TileScene = new(new Viewport(0, 0, CapturedTileSizeX, CapturedTileSizeY), camera);
                        TileScene.Camera.LookAt = new Vector2((float)X, (float)Y);
                        string tile_filename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Filename),
                            $"{System.IO.Path.GetFileNameWithoutExtension(Filename)}_Z{Z}_X{X}_Y{Y}_W{Width}_H{Height}_DS{Downsample}.png");

                        if (!System.IO.File.Exists(tile_filename))
                        {
                            listTasks.Add(ExportTileToFileAsync(TileScene, (float)X, (float)Y, Z, IncludeOverlays, false, tile_filename));
                        }

                        while (listTasks.Count > 0 && (listTasks.Count > MaxActiveExports))
                        {
                            var completedTask = await Task.WhenAny(listTasks);
                            listTasks.Remove(completedTask);
                        }


                    }

                    System.GC.Collect();
                }

                while (listTasks.Count > 0)
                {
                    var completedTask = await Task.WhenAny(listTasks);
                    listTasks.Remove(completedTask);
                }
            }
        }


        private async Task ExportTiles(string ExportPath, int FirstSection, int LastSection, int Downsample, CancellationToken token)
        {
            //Make sure sections are in order
            if (FirstSection > LastSection)
            {
                (FirstSection, LastSection) = (LastSection, FirstSection);
            }

            ExportPath += "/";

            //Capture each of the requested frames
            GenericProgressForm progressForm = new();
            progressForm.Show();

            using var userCancelCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, userCancelCts.Token);
            CancellationToken cancelToken = linkedCts.Token;

            //long OldCacheSize = Global.TextureCache.MaxCacheSize; 
            //Global.TextureCache.MaxCacheSize = (1 << 30);

            //            string OldVolumeTransform = this.CurrentVolumeTransform;

            //            this.CurrentVolumeTransform = null; 

            Scene originalScene = this.Scene;


            foreach (SectionViewModel S in State.volume.SectionViewModels.Values.Where(S => S.Number >= FirstSection && S.Number <= LastSection))
            {
                string path = ExportPath + S.VolumeViewModel.Name + "/" + S.Number.ToString("D3") + "/Tiles/" + Downsample.ToString("D3") + "/";

                System.IO.DirectoryInfo dirInfo = System.IO.Directory.Exists(path) == false ? System.IO.Directory.CreateDirectory(path) : new System.IO.DirectoryInfo(path);
                dirInfo.Attributes &= ~System.IO.FileAttributes.ReadOnly;

                //Get the boundaries of the section (use S, not this.Section, so each exported section has correct tile grid)
                MappingBase mapping = S.VolumeViewModel.GetTileMapping(Volume.ActiveVolumeTransform, S.Number, this.CurrentChannel, this.CurrentTransform);

                //Figure out how much we need to capture
                Size TileImageSize = new(512, 512);

                Size TileWorldSize = new(TileImageSize.Width * Downsample,
                                                TileImageSize.Height * Downsample);

                //Figure out how many tiles to expect
                Size TileDim = new((int)Math.Ceiling(mapping.ControlBounds.Width / (TileImageSize.Width * Downsample)),
                                        (int)Math.Ceiling(mapping.ControlBounds.Height / (TileImageSize.Height * Downsample)));

                Scene TileScene = new(new Viewport(0, 0, TileImageSize.Width, TileImageSize.Height), new Camera());
                TileScene.Camera.Downsample = Downsample;

                int numTiles = TileDim.Width * TileDim.Height;
                int iTile = 0;
                int MemoryFreeInterval = 1000;
                int EventInterval = (int)Math.Pow(TileDim.Width * TileDim.Height, 1 / 3.0);
                int ExistingTileUpdateInterval = 10000;
                int LoopCounter = 0;
                int MaxTilesQueued = 256;

                List<Task> listTasks = new(MaxTilesQueued);

                for (int iX = 0; iX < TileDim.Width; iX++)
                {
                    double X = (iX * TileWorldSize.Width) + (TileWorldSize.Width / 2);

                    for (int iY = 0; iY < TileDim.Height; iY++, iTile++)
                    {
                        LoopCounter++;
                        double Y = (iY * TileWorldSize.Height) + (TileWorldSize.Height / 2);

                        string Filename = path + $"X{iX:D3}_Y{iY:D3}.png";

                        //Assume images already on disk are good
                        if (System.IO.File.Exists(Filename))
                        {
                            if (LoopCounter % ExistingTileUpdateInterval == 0)
                            {
                                //Todo: Switch this to use IProgressReporter
                                progressForm.ShowProgress("Section " + S.Name + "\nFrame ID: " + Filename, (double)iTile / (double)numTiles);
                                //Application.DoEvents();
                            }

                            continue;
                        }

                        TileScene.Camera.LookAt = new Vector2((float)X, (float)Y);

                        if (false == await SceneHasTextures(TileScene, S.Number, cancelToken))
                            continue;

                        listTasks.Add(ExportTileToFileAsync(TileScene, (float)X, (float)Y, S.Number, false, false, Filename, cancelToken));

                        //Throttle tile creation so we don't exceed our memory limits

                        while (listTasks.Count > 0 && (listTasks.Count > MaxTilesQueued))
                        {
                            var completedTask = await Task.WhenAny([.. listTasks]);
                            listTasks.Remove(completedTask);
                        }

                        Application.DoEvents();

                        if (progressForm.DialogResult == DialogResult.Cancel)
                        {
                            userCancelCts.Cancel();
                            await Task.WhenAll(listTasks).ConfigureAwait(false);
                            break;
                        }

                        //Do events once in a while
                        if (LoopCounter % EventInterval == 0)
                        {
                            Parent.BeginInvoke(new Action(() =>
                            {
                                progressForm.ShowProgress("Section " + S.Name + "\nFrame ID: " + Filename, (double)iTile / (double)numTiles);
                                Parent.Invalidate();
                            }));

                            Application.DoEvents();


                            //System.Windows.Forms.Application.DoEvents();
                            if (progressForm.DialogResult == DialogResult.Cancel)
                            {
                                userCancelCts.Cancel();
                                await Task.WhenAll(listTasks).ConfigureAwait(false);
                                break;
                            }
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

                    await Task.WhenAll(listTasks).ConfigureAwait(false);

                    if (progressForm.DialogResult == DialogResult.Cancel)
                    {
                        userCancelCts.Cancel();
                        break;
                    }

                }

                System.IO.StreamWriter stream = null;

                string XMLString =
                    $"<?xml version=\"1.0\"?>\n<Level FilePostfix=\".png\" FilePrefix=\"\" Downsample=\"{Downsample}\" TileYDim=\"{TileImageSize.Width}\" TileXDim=\"{TileImageSize.Height}\" GridDimY=\"{TileDim.Height}\" GridDimX=\"{TileDim.Width}\"/>";
                string XMLPath = path + $"{S.Number:D4}.xml";
                using (stream = System.IO.File.CreateText(XMLPath))
                    await stream.WriteAsync(XMLString);

                if (progressForm.DialogResult == DialogResult.Cancel)
                {
                    userCancelCts.Cancel();
                    break;
                }
            }

            progressForm.BeginInvoke(new Action(() => progressForm.Close()));
        }

        private async Task ExportTileToFileAsync(VikingXNA.Scene TileScene, float CenterX, float CenterY, int sectionNumber, bool showOverlays, bool asyncTextureLoad, string filename, CancellationToken token = default)
        {
            var tex = await RenderSceneToTexture(TileScene, CenterX, CenterY, sectionNumber, showOverlays, asyncTextureLoad, token);
            if (tex != null)
                await tex.SaveAsync(filename, System.Drawing.Imaging.ImageFormat.Png);
        }

        public async Task<RenderTarget2D> RenderSceneToTexture(VikingXNA.Scene TileScene, float CenterX, float CenterY, int Z, bool showOverlays, bool asyncTextureLoad, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            if (!asyncTextureLoad)
                await PreloadSceneTexturesAsync(TileScene, Z, asyncTextureLoad, token);
            else
                _ = PreloadSceneTexturesAsync(TileScene, Z, asyncTextureLoad, token);
            /*
            Task preloadTask = await PreloadSceneTexturesAsync(TileScene, Z, false);
            do
            { 
                Application.DoEvents();
            }
            while (preloadTask.IsCompleted == false && preloadTask.IsFaulted == false && preloadTask.IsCanceled == false);
            */
            if (token.IsCancellationRequested)
                return null;

            SectionViewModel originalSection = this.Section;

            this.Section = State.volume.SectionViewModels[Z];

            try
            {
                GraphicsDevice graphicsDevice = this.graphicsDeviceService.GraphicsDevice;
                TileScene.Camera.LookAt = new Vector2(CenterX, CenterY);

                RenderTarget2D renderTargetTile = new(graphicsDevice, TileScene.Viewport.Width, TileScene.Viewport.Height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);

                // Use TaskCompletionSource to handle the asynchronous operation
                TaskCompletionSource<bool> taskCompletionSource = new();
                var result = this.BeginInvoke(new Action(() =>
                {

                    if (token.IsCancellationRequested)
                        return;

                    var originalScene = this.Scene;
                    try
                    {
                        var originalShowOverlays = this.ShowOverlays;
                        this.ShowOverlays = showOverlays;
                        Draw(TileScene, renderTargetTile);
                        //Obtain texture from renderTarget
                        graphicsDevice.SetRenderTarget(null);
                        taskCompletionSource.SetResult(true);

                        this.ShowOverlays = originalShowOverlays;
                    }
                    catch (Exception ex)
                    {
                        // Mark the task as faulted if an exception occurs
                        taskCompletionSource.SetException(ex);
                    }
                    finally
                    {
                        this.Scene = originalScene;
                    }
                }));

                await taskCompletionSource.Task;

                if (token.IsCancellationRequested)
                    return null;

                return renderTargetTile;
            }
            finally
            {
                this.Section = originalSection;
            }
        }

        protected void InitGraphicsDeviceForDraw(GraphicsDevice graphicsDevice)
        {

        }

        private DepthStencilState? _defaultDepthState = null;
        public DepthStencilState defaultDepthState
        {
            get
            {
                if (_defaultDepthState is null || _defaultDepthState.IsDisposed)
                {
                    _defaultDepthState = new DepthStencilState
                    {
                        DepthBufferEnable = true,
                        DepthBufferFunction = CompareFunction.LessEqual,
                        DepthBufferWriteEnable = true,
                        StencilEnable = false
                    };
                }

                return _defaultDepthState;
            }
        }

        private DepthStencilState? _OverlayBackgroundDepthState = null;
        public DepthStencilState OverlayBackgroundDepthState
        {
            get
            {
                if (_OverlayBackgroundDepthState is null || _OverlayBackgroundDepthState.IsDisposed)
                {
                    _OverlayBackgroundDepthState = new DepthStencilState
                    {
                        DepthBufferEnable = false,
                        DepthBufferWriteEnable = true,
                        DepthBufferFunction = CompareFunction.LessEqual,

                        StencilEnable = true,
                        StencilFunction = CompareFunction.Greater,
                        ReferenceStencil = 1
                    };
                }

                return _OverlayBackgroundDepthState;
            }
        }

        /// <summary>Cache of depth states for overlay drawing, keyed by (StencilValue, DepthEnabled). Cleared in Dispose.</summary>
        private readonly Dictionary<(int StencilValue, bool DepthEnabled), DepthStencilState> _overlayDepthStateCache = new();

        /// <summary>Get or create a depth state for overlay drawing. Replaces cached state when stencil/usage changes. Cache is cleared in Dispose.</summary>
        protected DepthStencilState CreateDepthStateForOverlay(int StencilValue, bool DepthEnabled = true)
        {
            var key = (StencilValue, DepthEnabled);
            if (_overlayDepthStateCache.TryGetValue(key, out var cached) && cached != null && !cached.IsDisposed)
                return cached;

            if (cached != null)
            {
                cached.Dispose();
                _overlayDepthStateCache.Remove(key);
            }

            var state = new DepthStencilState
            {
                DepthBufferEnable = DepthEnabled,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual,

                StencilEnable = true,
                StencilFunction = CompareFunction.Greater,
                ReferenceStencil = StencilValue,
                StencilPass = StencilOperation.Replace
            };
            _overlayDepthStateCache[key] = state;
            return state;
        }

        /// <summary>Cache of depth states for downsample level drawing, keyed by StencilValue. Cleared in Dispose.</summary>
        private readonly Dictionary<int, DepthStencilState> _downsampleDepthStateCache = new();

        /// <summary>Get or create a depth state for downsample level. Replaces cached state when stencil value changes. Cache is cleared in Dispose.</summary>
        protected DepthStencilState CreateDepthStateForDownsampleLevel(int StencilValue)
        {
            if (_downsampleDepthStateCache.TryGetValue(StencilValue, out var cached) && cached != null && !cached.IsDisposed)
                return cached;

            if (cached != null)
            {
                cached.Dispose();
                _downsampleDepthStateCache.Remove(StencilValue);
            }

            var state = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual,

                StencilEnable = true,
                StencilFunction = CompareFunction.GreaterEqual,
                ReferenceStencil = StencilValue,
                StencilPass = StencilOperation.Replace
            };
            _downsampleDepthStateCache[StencilValue] = state;
            return state;
        }

        private DepthStencilState? _DepthDisabledState = null;
        protected DepthStencilState DepthDisabledState
        {
            get
            {
                if (_DepthDisabledState is null || _DepthDisabledState.IsDisposed)
                {
                    _DepthDisabledState = new DepthStencilState
                    {
                        DepthBufferEnable = false
                    };
                }
                return _DepthDisabledState;
            }
        }


        public static string[] DefaultMouseHelpStrings = [];

        public static string[] DefaultKeyHelpStrings = [
            "F1: Expand full list of commands",
            "CTRL + G: Open goto position dialog",
            "Space bar: Hide annotations",
            "Space bar + CTRL: Show only annotations"
            ];

        public string[] HelpStrings
        {
            get
            {
                List<string> listHelp = [.. DefaultKeyHelpStrings];
                listHelp.AddRange(DefaultMouseHelpStrings);
                return [.. listHelp];
            }
        }

        VikingXNAGraphics.Controls.CircularButton upSectionButton;
        VikingXNAGraphics.Controls.CircularButton downSectionButton;

        protected void CreateSectionButtons()
        {
            if (upSectionButton is null)
            {
                TextureCircleView plusView = TextureCircleView.CreatePlusCircle(new Circle(Geometry.Vector2.Zero, 1.0),
                                                Microsoft.Xna.Framework.Color.Goldenrod);

                upSectionButton = new VikingXNAGraphics.Controls.CircularButton(plusView, this.OnUpSectionButtonClicked)
                {
                    OnClick = this.OnUpSectionButtonClicked
                };
            }

            if (downSectionButton is null)
            {
                TextureCircleView minusView = TextureCircleView.CreateMinusCircle(new Circle(Geometry.Vector2.Zero, 1.0),
                                                Microsoft.Xna.Framework.Color.Goldenrod);

                downSectionButton = new VikingXNAGraphics.Controls.CircularButton(minusView)
                {
                    OnClick = this.OnDownSectionButtonClicked
                };
                //                downSectionButton.OnClick += th
            }
        }

        protected void DrawXNAControls(Scene scene)
        {
            CreateSectionButtons();

            //TODO: Position the buttons
            /*
            Camera C = new Camera();

            Scene SimpleScene = new Scene()
            
            GraphicsDevice graphicsDevice = Device;
            */

            //TODO: These coordinates on the screen should be from 0 to 1 with a seperate worldviewproj matrix.  However to get this running I'm just
            //calculating in volume spce. 

            Geometry.Vector2 TopLeft = scene.ScreenToWorld(0, scene.Viewport.Height);
            Geometry.Vector2 BottomRight = scene.ScreenToWorld(scene.Viewport.Width, 0);
            Geometry.Vector2 BottomLeft = scene.ScreenToWorld(0, 0);

            Geometry.Vector2 Tenth = new(scene.VisibleWorldBounds.Width / 15.0, -scene.VisibleWorldBounds.Height / 15.0);

            double radius = Math.Min(Tenth.X, -Tenth.Y);
            upSectionButton.Circle = new Circle(BottomLeft + Tenth, radius);
            downSectionButton.Circle = new Circle((BottomLeft + Tenth) + new Geometry.Vector2(0, Tenth.Y * 2.5), radius);

            OverlayShaderEffect overlayEffect = VikingXNAGraphics.DeviceEffectsStore<OverlayShaderEffect>.TryGet(Device);
            overlayEffect.Technique = OverlayShaderEffect.Techniques.CircleSingleColorTextureAlphaOverlayEffect;
            VikingXNAGraphics.TextureCircleView.Draw(Device, scene, overlayEffect,
                [upSectionButton.circleView, downSectionButton.circleView]);
        }

        protected override void Draw(Scene scene)
        {
            //graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            if (Section is null)
                return;

            GraphicsDevice graphicsDevice = Device;
            RenderTargetBinding[] originalRenderTargets = Device.GetRenderTargets();
            Geometry.Rectangle Bounds = scene.VisibleWorldBounds;

            basicEffect.Alpha = 1.0f;
            basicEffect.AmbientLightColor = new Microsoft.Xna.Framework.Vector3(1, 1, 1);

            graphicsDevice.DepthStencilState = defaultDepthState;

            BlendState OriginalBlendState = graphicsDevice.BlendState;

            double HalfWidth = Bounds.Width / 2;
            double HalfHeight = Bounds.Height / 2;
            Geometry.Vector2 BotLeft = new(Bounds.Center.X - HalfWidth, Bounds.Center.Y + HalfHeight);
            Geometry.Vector2 TopRight = new(Bounds.Center.X + HalfWidth, Bounds.Center.Y - HalfHeight);

            VertexPositionNormalTexture[] visibleAreaMesh = [
                new( new Vector3((float)BotLeft.X, (float)BotLeft.Y, 0), Vector3.UnitZ, new Vector2(0,0)),
                new( new Vector3((float)TopRight.X, (float)BotLeft.Y, 0), Vector3.UnitZ,  new Vector2(1,0)),
                new( new Vector3((float)BotLeft.X, (float)TopRight.Y, 0), Vector3.UnitZ,   new Vector2(0,1)),
                new( new Vector3((float)TopRight.X, (float)TopRight.Y, 0), Vector3.UnitZ, new Vector2(1,1))];

            //OK, figure out if we are rendering channels or not.
            //The section channel settings are checked first.  If they
            //are not found we use the global channel settings.
            ChannelInfo[] Channelset = CurrentChannelset;

            StatusChannels = Channelset;
            State.CurrentMode = this.CurrentChannel;

            Texture? backgroundSectionTexture = null;
            Texture? ChannelOverlay = null;
            bool usedCachedTexture = false;
            var currentCacheKey = BuildSectionTextureCacheKey(scene, Channelset);

            // Check cache: reuse if scene and section unchanged and cache is valid
            if (_cachedSectionTexture != null && !_cachedSectionTexture.IsDisposed && _sectionTextureCacheKey.Equals(currentCacheKey))
            {
                backgroundSectionTexture = _cachedSectionTexture;
                ChannelOverlay = _cachedChannelOverlay;
                usedCachedTexture = true;
            }

            if (!usedCachedTexture)
            {
                if (Channelset.Length == 1)
                {
                    ChannelInfo singleChannel = Channelset[0];

                    // If the single channel is greyscale, render as greyscale
                    // If it's a color channel, apply the channel's color
                    if (singleChannel.Greyscale)
                    {
                        tileLayoutEffect.TileColor = new Microsoft.Xna.Framework.Color(1f, 1f, 1f, 1);
                    }
                    else
                    {
                        // Apply the channel's color for single color channel rendering
                        tileLayoutEffect.TileColor = new Microsoft.Xna.Framework.Color(
                            (float)singleChannel.Color.R / 255f,
                            (float)singleChannel.Color.G / 255f,
                            (float)singleChannel.Color.B / 255f,
                            (float)singleChannel.Color.A / 255f);
                    }

                    tileLayoutEffect.RenderToGreyscale();

                    var (texture, allVisibleTilesHadTextures) = DrawSection(graphicsDevice, this.Section.section, this.CurrentChannel, scene);
                    backgroundSectionTexture = texture;
                    if (texture != null && allVisibleTilesHadTextures)
                    {
                        InvalidateSectionTextureCache();
                        _cachedSectionTexture = texture as RenderTarget2D;
                        _cachedChannelOverlay = null;
                        _sectionTextureCacheKey = currentCacheKey;
                        usedCachedTexture = true;
                    }
                }
                else
                {
                    //Walk through each channel and draw the section
                    var (bgTexture, overlayTexture, allVisibleTilesHadTextures) = DrawSectionsWithChannels(graphicsDevice, Channelset, scene);
                    backgroundSectionTexture = bgTexture;
                    ChannelOverlay = overlayTexture;
                    if (bgTexture != null && allVisibleTilesHadTextures)
                    {
                        InvalidateSectionTextureCache();
                        _cachedSectionTexture = bgTexture as RenderTarget2D;
                        _cachedChannelOverlay = overlayTexture as Texture2D;
                        _sectionTextureCacheKey = currentCacheKey;
                        usedCachedTexture = true;
                    }
                }
            }



            //Enable stencil buffer.  
            graphicsDevice.SetRenderTargets(originalRenderTargets);

            this.channelOverlayEffect.SetEffectTextures(backgroundSectionTexture, ChannelOverlay);

            //this.channelOverlayEffect.BackgroundTexture = backgroundSection;
            //this.channelOverlayEffect.OverlayTexture = ChannelOverlay;

            int NextStencilValue = 0;

            graphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Microsoft.Xna.Framework.Color.Black, 1, NextStencilValue++);

            //Set a standard starting state for all overlay modules
            graphicsDevice.DepthStencilState = OverlayBackgroundDepthState;
            VikingXNAGraphics.DeviceStateManager.SetDepthStencilValue(graphicsDevice, 1);

            if (!ShowOnlyOverlays)
            {
                //OK, blend the overlay with the underlying greyscale image
                foreach (EffectPass pass in channelOverlayEffect.effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                                                                                      visibleAreaMesh, 0, visibleAreaMesh.Length,
                                                                                      indicies, 0, indicies.Length / 3);
                }
            }

            //Draw the tiles without annotations, allow them to overwrite existing data
            //            List<RenderTarget2D> OverlayList = new List<RenderTarget2D>(listOverlays.Length);
            if (ShowOverlays)
            {
                UpdateLumaTextureForOverlayEffects(backgroundSectionTexture);

                for (int i = 0; i < listOverlays.Length; i++)
                {
                    ++NextStencilValue;
                    graphicsDevice.DepthStencilState = CreateDepthStateForOverlay(++NextStencilValue);
                    VikingXNAGraphics.DeviceStateManager.SetDepthStencilValue(graphicsDevice, NextStencilValue);

                    graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1, 0);

                    ISectionOverlayExtension overlayObj = listOverlays[i];
#if DEBUG
                    BlendState startingBlendState = graphicsDevice.BlendState;
                    DepthStencilState startingDepthState = graphicsDevice.DepthStencilState;
#endif
                    overlayObj.Draw(graphicsDevice, scene, backgroundSectionTexture, ChannelOverlay, ref NextStencilValue);
#if DEBUG
                    System.Diagnostics.Debug.Assert(startingBlendState == graphicsDevice.BlendState,
                        $"Blend state changed by overlay extension draw method {overlayObj}");
                    //Stencil reference can change on depthstate, so ignore check for now
                    //System.Diagnostics.Debug.Assert(startingDepthState == graphicsDevice.DepthStencilState, string.Format("Depth state changed by overlay extension draw method {0}", overlayObj.ToString()));
#endif
                }

                ///This is a bad way to know if we are capturing a screenshot, but works for now
                if (AsynchTextureLoad)
                {
                    try
                    {
                        if (CurrentCommand != null)
                        {

                            ++NextStencilValue;
                            graphicsDevice.DepthStencilState = CreateDepthStateForOverlay(++NextStencilValue, true);
                            VikingXNAGraphics.DeviceStateManager.SetDepthStencilValue(graphicsDevice, NextStencilValue);

                            graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1, 0);

                            CurrentCommand.OnDraw(graphicsDevice, scene, basicEffect);
                        }

                    }
                    catch (InvalidOperationException)
                    {
                        Trace.WriteLine("Could not create render target for channels", "UI");
                    }
                }

                //DrawXNAControls(scene);
            }

            // Draw section number overlay (after overlays but before cleanup)
            DrawSectionNumberOverlay(graphicsDevice, scene);

            graphicsDevice.Textures[0] = null;
            graphicsDevice.Textures[1] = null;
            graphicsDevice.Textures[2] = null;
            graphicsDevice.Textures[3] = null;
            graphicsDevice.Textures[4] = null;
            graphicsDevice.Textures[5] = null;
            graphicsDevice.Textures[6] = null;
            graphicsDevice.Textures[7] = null;
            if (!usedCachedTexture)
            {
                backgroundSectionTexture?.Dispose();
                ChannelOverlay?.Dispose();
            }
            backgroundSectionTexture = null;
            ChannelOverlay = null;

            graphicsDevice.BlendState = OriginalBlendState;
            DrawCallSinceTileCacheCheckpoint = true;

            timer.Interval = 25;
        }

        private void UpdateLumaTextureForOverlayEffects(Texture BackgroundLuma)
        {
            this.LumaOverlayCurveManager.LumaTexture = BackgroundLuma;
            this.LumaOverlayCurveManager.RenderTargetSize = Device.Viewport;

            this.LumaOverlayLineManager.LumaTexture = BackgroundLuma;
            this.LumaOverlayLineManager.RenderTargetSize = Device.Viewport;

            this.PolygonOverlayEffect.LumaTexture = BackgroundLuma;
            this.PolygonOverlayEffect.RenderTargetSize = Device.Viewport;

            this.AnnotationOverlayEffect.LumaTexture = BackgroundLuma;
            this.AnnotationOverlayEffect.RenderTargetSize = Device.Viewport;
        }

        public static string TileCacheFullPath(Section section, string TextureFileName) => System.IO.Path.Combine([State.TextureCachePath, section.SectionSubPath, TextureFileName]);

        /// <summary>
        /// Resolves the full path for a tile texture (local path or HTTP(S) URL).
        /// </summary>
        private static string ResolveTileFullPath(TileViewModel t, Section section)
        {
            if (t.TextureFullPath.StartsWith(System.Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                t.TextureFullPath.StartsWith(System.Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return t.TextureFullPath;
            }
            return $"{section.Path}{System.IO.Path.DirectorySeparatorChar}{t.TextureFullPath}";
        }

        /// <summary>
        /// Fetches or constructs a TileView for the given tile model and section.
        /// </summary>
        private static TileView FetchOrConstructTileForSection(TileViewModel t, Section section, string mappingName)
        {
            string tileFileName = ResolveTileFullPath(t, section);
            return Global.TileViewModelCache.FetchOrConstructTile(t, tileFileName,
                TileCacheFullPath(section, t.TextureCacheFilePath), mappingName, 0);
        }

        /*
        protected override void OnSceneChanged(object sender, PropertyChangedEventArgs e)
        {
            PreloadSceneTextures(this.Scene, this.Section.section.Number);
        }
        */

        /// <summary>
        /// Return true if any channel in the scene has a visible tile texture
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        protected async Task<bool> SceneHasTextures(Scene scene, int Z, CancellationToken token)
        {
            if (false == Volume.SectionViewModels.ContainsKey(Z))
                return false;

            SectionViewModel visibleSection = Volume.SectionViewModels[Z];
            ChannelInfo[] channels = visibleSection.ChannelInfoArray;
            if (channels.Length == 0)
                channels = visibleSection.VolumeViewModel.DefaultChannels;

            foreach (ChannelInfo channel in channels)
            {
                Section section = visibleSection.GetSectionToDrawForChannel(channel);
                MappingBase Mapping = Viking.UI.State.volume.GetTileMapping(section.Number, channel.ChannelName, this.CurrentTransform);
                await Mapping.Initialize(token);
                if (token.IsCancellationRequested)
                    return false;

                int[] DownsamplesToRender = CalculateDownsamplesToRender(Mapping, scene.Camera.Downsample);

                DownsamplesToRender = [DownsamplesToRender.Last()];

                //Get all of the visible tiles
                TilePyramid visibleTiles = await Mapping.VisibleTilesAsync(scene.VisibleWorldBounds, scene.Camera.Downsample);
                for (int iLevel = 0; iLevel < DownsamplesToRender.Length; iLevel++)
                {
                    int level = Mapping.AvailableLevels[DownsamplesToRender[iLevel]];
                    SortedDictionary<TileUniqueKey, TileViewModel> tileList = visibleTiles.GetTilesForLevel(level);
                    if (tileList.Count > 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Queues texture loads for a single section and returns the list of load tasks.
        /// Used by PreloadSceneTexturesAsync and for adjacent-section preloading on section change.
        /// </summary>
        private async Task<List<Task<Texture2D>>> QueueTextureLoadsForSectionAsync(Scene scene, int sectionZ, bool highestResolutionOnly, CancellationToken token)
        {
            List<Task<Texture2D>> listGetTextureTasks = [];

            if (false == Volume.SectionViewModels.ContainsKey(sectionZ))
                return listGetTextureTasks;

            SectionViewModel visibleSection = Volume.SectionViewModels[sectionZ];
            ChannelInfo[] channels = visibleSection.ChannelInfoArray;
            if (channels.Length == 0)
                channels = visibleSection.VolumeViewModel.DefaultChannels;

            foreach (ChannelInfo channel in channels)
            {
                Section section = visibleSection.GetSectionToDrawForChannel(channel);
                MappingBase Mapping = Viking.UI.State.volume.GetTileMapping(section.Number, channel.ChannelName, this.CurrentTransform);
                if (Mapping is null)
                    continue;

                await Mapping.Initialize(token);
                if (token.IsCancellationRequested)
                    return listGetTextureTasks;

                CancellationToken sectionTextureLoadToken = GetOrCreateSectionTextureLoadToken(section.Number);
                int[] DownsamplesToRender = CalculateDownsamplesToRender(Mapping, scene.Camera.Downsample);

                if (highestResolutionOnly)
                    DownsamplesToRender = [DownsamplesToRender.Last()];

                var visibleTiles = await Mapping.VisibleTilesAsync(scene.VisibleWorldBounds, scene.Camera.Downsample);

                for (int iLevel = 0; iLevel < DownsamplesToRender.Length; iLevel++)
                {
                    int level = Mapping.AvailableLevels[DownsamplesToRender[iLevel]];

                    SortedDictionary<TileUniqueKey, TileViewModel> tileList = visibleTiles.GetTilesForLevel(level);

                    foreach (TileViewModel t in tileList.Values)
                    {
                        TileView tileView = FetchOrConstructTileForSection(t, section, Mapping.Name);
                        if (tileView is null)
                            continue;

                        //Don't request and draw a bunch of levels that cover the entire screen.  Saves time if we are at high magnification
                        if (tileView.HasTexture == false && tileView.Downsample > Downsample * 8 && iLevel < DownsamplesToRender.Length - 1)
                            continue;

                        if (tileView.TextureNeedsLoading)
                        {
                            listGetTextureTasks.Add(tileView.GetOrLoadTextureAsync(this.graphicsDeviceService.GraphicsDevice, sectionTextureLoadToken));
                        }
                    }
                }
            }

            return listGetTextureTasks;
        }

        /// <summary>
        /// Preloads texture for the visible tiles in the given section, awaiting completion.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="Z"></param>
        /// <param name="HighestResolutionOnly">If this is true we only load the high resolution textures.  This is used when the scene should not be drawn until all textures are loaded and there is no reason to load intermediate textures.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        protected async Task PreloadSceneTexturesAsync(Scene scene, int Z, bool HighestResolutionOnly, CancellationToken token)
        {
            var listGetTextureTasks = await QueueTextureLoadsForSectionAsync(scene, Z, HighestResolutionOnly, token);
            while (listGetTextureTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(listGetTextureTasks).ConfigureAwait(false);
                listGetTextureTasks.Remove(completedTask);
                if (completedTask.IsFaulted && completedTask.Exception is AggregateException ex)
                {
                    Trace.WriteLine($"PreloadSceneTextures texture load failed: {ex.GetBaseException().Message}");
                }
            }
        }

        protected (Texture2D? texture, bool allVisibleTilesHadTextures) DrawSection(GraphicsDevice graphicsDevice, Section section, string channel, Scene scene)
        {
            //           Microsoft.Xna.Framework.Color[] ColorWheel = new Microsoft.Xna.Framework.Color[] { new Microsoft.Xna.Framework.Color(1f,0,0), 
            //                                             new Microsoft.Xna.Framework.Color(0,1f,0),
            //                                          new Microsoft.Xna.Framework.Color(0,0,1f)};

            MappingBase mapping = Viking.UI.State.volume.GetTileMapping(section.Number, channel, this.CurrentTransform);
            if (mapping is null)
            {
                return (null, false);
            }

            if (mapping.Initialized == false)
            {
                StartMappingInitIfNeeded(section.Number, mapping);
                return (null, false);
            }

            int[] DownsamplesToRender = CalculateDownsamplesToRender(mapping, scene.Camera.Downsample);

            //Get all of the visible tiles
            var visibleTiles = mapping.VisibleTiles(scene.VisibleWorldBounds, scene.Camera.Downsample);

            if (DownsamplesToRender.Length == 0)
            {
                int[] available = mapping.AvailableLevels;
                var fallback = new System.Collections.Generic.List<int>(available.Length);
                for (int i = 0; i < available.Length; i++)
                {
                    if (visibleTiles.GetTilesForLevel(available[i]).Count > 0)
                        fallback.Add(i);
                }

                if (fallback.Count > 0)
                {
                    DownsamplesToRender = fallback.ToArray();
                }
            }

            //If we aren't loading asynchronously only load the hi-res textures since we are waiting for completion
            if (!AsynchTextureLoad && DownsamplesToRender.Length > 0)
                DownsamplesToRender = [DownsamplesToRender.Last()];

            RenderTarget2D renderTarget = new(graphicsDevice,
                                              scene.Viewport.Width,
                                              scene.Viewport.Height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

            //        Debug.Assert(graphicsDevice.Viewport.Width == ClientRectangle.Width); 

            graphicsDevice.SetRenderTarget(renderTarget);
            //       graphicsDevice.SetRenderTarget(null);

            //Clear the stencil buffer before we begin
            graphicsDevice.Clear(ClearOptions.Stencil, Microsoft.Xna.Framework.Color.Black, 1f, 0);
            DepthStencilState originalDepthState = graphicsDevice.DepthStencilState;

            bool allVisibleTilesHadTextures = true;
            // No tiles visible at any downsample level — don't cache the empty result so the
            // timer's Invalidate keeps the draw loop live until the camera moves into tile range.
            if (DownsamplesToRender.Length == 0)
                allVisibleTilesHadTextures = false;
            CancellationToken sectionTextureLoadToken = GetOrCreateSectionTextureLoadToken(section.Number);
            for (int iLevel = 0; iLevel < DownsamplesToRender.Length; iLevel++)
            {
                int level = mapping.AvailableLevels[DownsamplesToRender[iLevel]];

                //Clear the depth buffer before we begin this level, we only want to compare to tiles in our level
                graphicsDevice.Clear(ClearOptions.DepthBuffer, Microsoft.Xna.Framework.Color.Black, 1f, 0);

                //Use a stencil buffer to prevent lower-res textures from overwriting higer-res textures
                VikingXNAGraphics.DeviceStateManager.SetDepthStencilValue(graphicsDevice, iLevel);
                graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(iLevel);

                SortedDictionary<TileUniqueKey, TileViewModel> tileList = visibleTiles.GetTilesForLevel(level);

                List<TileView> tileViewsToDraw = [];

                int iColor = 0;
                foreach (TileViewModel t in tileList.Values)
                {
                    TileView tileView = FetchOrConstructTileForSection(t, section, mapping.Name);
                    if (tileView is null)
                        continue;

                    //Don't request and draw a bunch of levels that cover the entire screen.  Saves time if we are at high magnification
                    if (tileView.HasTexture == false && tileView.Downsample > Downsample * 8 && iLevel < DownsamplesToRender.Length - 1)
                        continue;

                    if (tileView.TextureNeedsLoading && !tileView.TextureIsLoading)
                    {
                        var tile = tileView;
                        tile.MarkLoadQueued();
                        _ = Task.Run(async () => await tile.GetOrLoadTextureAsync(graphicsDevice, sectionTextureLoadToken).ConfigureAwait(false))
                            .ContinueWith(tt => { if (tt.IsFaulted && tt.Exception != null) Trace.WriteLine($"DrawSection texture load failed: {tt.Exception.GetBaseException().Message}"); }, TaskContinuationOptions.OnlyOnFaulted);
                        allVisibleTilesHadTextures = false;
                    }
                    else if (tileView.TextureReadComplete)
                    {
                        tileViewsToDraw.Add(tileView);
                    }
                    else
                    {
                        allVisibleTilesHadTextures = false;
                    }
                }

                // On highest-resolution level: all tiles in tileList (that we didn't skip) should have been drawn.
                // Do not cache when tileList is empty or we drew no tiles (avoids caching black after jump-to-location).
                if (iLevel == DownsamplesToRender.Length - 1 && (tileList.Count == 0 || tileViewsToDraw.Count < tileList.Count))
                    allVisibleTilesHadTextures = false;

                foreach (TileView tileViewModel in tileViewsToDraw)
                {
                    tileViewModel.Draw(graphicsDevice, tileLayoutEffect, AsynchTextureLoad, ColorizeTiles);

                    if (iLevel == DownsamplesToRender.Length - 1 && Viking.UI.State.ShowTileMesh)
                    {
                        tileViewModel.DrawMesh(graphicsDevice, basicEffect);

                        //tileViewModel.DrawLabel(this);
                    }

                    iColor++;
                }

                //If this is the highest level resolution then draw levels
                if (Viking.UI.State.ShowTileMesh && iLevel == DownsamplesToRender.Length - 1)
                {
                    var labels = tileViewsToDraw.Select(t => t.TileLabel).ToArray();
                    LabelView.Draw(this.spriteBatch, VikingXNAGraphics.Global.DefaultFont, scene, labels);
                }
                //     if (AllTilesDrawn)
                //         break; 
            }



            if (Viking.UI.State.ShowStosMesh)
            {
                ITransform transform = null;

                if (mapping is SectionToVolumeMapping StosMapping)
                {
                    transform = StosMapping.VolumeTransform;
                }
                else
                {
                    if (mapping is TileGridToVolumeMapping TGStosMapping)
                    {
                        transform = TGStosMapping.VolumeTransform;
                    }
                }

                if (transform as Geometry.IControlPointTriangulation != null)
                {
                    VikingXNAGraphics.DeviceStateManager.SetDepthStencilValue(graphicsDevice, int.MaxValue);
                    graphicsDevice.DepthStencilState = CreateDepthStateForDownsampleLevel(int.MaxValue);

                    using TriangulationViewModel stosMeshViewModel = new(transform as Geometry.IControlPointTriangulation);
                    stosMeshViewModel.DrawMesh(graphicsDevice, basicEffect);
                    stosMeshViewModel.DrawLabels(this, scene);
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

            if (renderedTargets is null)
                return (null, false);

            if (renderedTargets.Length > 0)
                return (renderTarget, allVisibleTilesHadTextures);


            return (null, false);
        }


        protected int[] CalculateDownsamplesToRender(MappingBase Mapping, double downsample)
        {
            if (Mapping is null)
            {
                Trace.WriteLine("CalculateDownsamplesToRender Mapping parameter is null");
                return [];
            }

            int roundedDownsample = Mapping.NearestAvailableLevel(downsample);
            if (roundedDownsample == int.MaxValue)
                return [];

            //Find the index of the requested downsample level
            List<int> DownsamplesToRender = new(Mapping.AvailableLevels.Length);

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

            //Textures are fetched in the order they are asked for.  So we should ask for low-res textures before high-res textures.  However if high-res textures are available we shouldn't bother
            //with asking for low res textures.
            DownsamplesToRender.Reverse();

            return [.. DownsamplesToRender];
        }

        private (Texture2D? backgroundSection, Texture? channelOverlay, bool allVisibleTilesHadTextures) DrawSectionsWithChannels(GraphicsDevice graphicsDevice, ChannelInfo[] Channelset, Scene scene)
        {
            Texture2D? backgroundSection = null;
            Texture? channelOverlay = null;

            List<Texture2D> renderedSections = new(Channelset.Length - 1);
            List<ChannelInfo> renderedChannels = new(Channelset.Length - 1);
            //            List<float> renderedAlphas = new List<float>(Channelset.Length - 1);
            //            List<float> renderedBetas = new List<float>(Channelset.Length - 1);
            List<Vector4> renderedChannelColors = new(Channelset.Length - 1);
            bool allVisibleTilesHadTextures = true;
            try
            {
                //            int DisplayWidth = graphicsDevice.Viewport.Width;
                //            int DisplayHeight = graphicsDevice.Viewport.Height;

                //            Viewport oldViewport = graphicsDevice.Viewport;
                //            RenderTargetBinding[] oldRenderTargets = graphicsDevice.GetRenderTargets();


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
                    Section sectionToDraw = this.Section.GetSectionToDrawForChannel(channel);

                    //Can't draw if the section doesn't exist
                    if (sectionToDraw is null)
                    {
                        allVisibleTilesHadTextures = false;
                        continue;
                    }

                    string ChannelName = channel.ChannelName;
                    if (ChannelName.Length == 0)
                    {
                        ChannelName = this.CurrentChannel;
                    }

                    //Find the mapping to use
                    MappingBase mapping = this.Section.VolumeViewModel.GetTileMapping(Volume.ActiveVolumeTransform,
                                                                    sectionToDraw.Number,
                                                                    ChannelName,
                                                                    this.CurrentTransform);

                    if (mapping is null)
                    {
                        allVisibleTilesHadTextures = false;
                        continue;
                    }

                    if (mapping.Initialized == false)
                    {
                        StartMappingInitIfNeeded(sectionToDraw.Number, mapping);
                        allVisibleTilesHadTextures = false;
                        continue;
                    }

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

                    //                Geometry.Rectangle renderTargetBounds = scene.VisibleWorldBounds;

                    var (renderTarget, channelAllTexturesReady) = DrawSection(graphicsDevice, sectionToDraw, channel.ChannelName, scene);
                    if (!channelAllTexturesReady)
                        allVisibleTilesHadTextures = false;

                    if (channel.Greyscale)
                    {
                        backgroundSection = renderTarget;
                    }
                    else if (renderTarget != null)
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

                //Merge the rendered channels to a single RGB image
                //            Trace.WriteLineIf(renderedChannels.Count > this.mergeHSVImagesEffect.MaxChannels, "Too many channels being rendered, only using the first " + renderedChannels.Count.ToString());

                channelOverlay = MergeRGBImages(graphicsDevice, scene, backgroundSection, [.. renderedSections], [.. renderedChannelColors]);

            }

            finally
            {
                foreach (RenderTarget2D renderedSection in renderedSections.Cast<RenderTarget2D>())
                {
                    renderedSection.Dispose();
                }

                renderedSections.Clear();
                renderedSections = null;
            }            //Free the textures from the channels

            /*
            graphicsDevice.BlendState = OriginalBlendState;

            if (OverlayBlendState != null)
            {
                OverlayBlendState.Dispose();
                OverlayBlendState = null; 
            }
            */

            return (backgroundSection, channelOverlay, allVisibleTilesHadTextures);
        }

        /// <summary>Static blend state for RGB merging; intentionally process-lifetime, recreated when null or disposed.</summary>
        static BlendState? MergeRGBBlendState = null;

        /// <summary>
        /// This functio n
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="scene"></param>
        /// <param name="channels"></param>
        /// <param name="Colors"></param>
        /// <returns></returns>
        private RenderTarget2D MergeRGBImages(GraphicsDevice graphicsDevice, Scene scene, Texture background, Texture2D[] channels, Microsoft.Xna.Framework.Vector4[] Colors)
        {
            if (channels.Length == 0)
                return null;

            var colorStats = MergeHSVImagesEffect.CalculateChannelTotals(Colors);

            //this.mergeHSVImagesEffect.Textures = renderedSections.ToArray();
            //this.mergeHSVImagesEffect.HueAlpha = renderedAlphas.ToArray();
            //this.mergeHSVImagesEffect.HueBeta = renderedBetas.ToArray();

            //We cannot read from an active render target.  So we alternate which target is being written to so we can merge the textures. 
            RenderTarget2D activeRenderTarget = null;
            RenderTarget2D inactiveRenderTarget = null;
            BlendState oldBlendState = graphicsDevice.BlendState;

            try
            {
                if (MergeRGBBlendState is null || MergeRGBBlendState.IsDisposed)
                {
                    MergeRGBBlendState = new BlendState
                    {
                        AlphaBlendFunction = BlendFunction.Add,
                        ColorBlendFunction = BlendFunction.Add,
                        AlphaSourceBlend = Blend.One,
                        AlphaDestinationBlend = Blend.Zero,
                        ColorSourceBlend = Blend.One,
                        ColorDestinationBlend = Blend.Zero,
                        Name = "MergeRGBBlendState"
                    };
                }

                graphicsDevice.BlendState = MergeRGBBlendState;

                //       AfterFirstTextureBlendState.ColorSourceBlend = Blend.One;
                //       AfterFirstTextureBlendState.ColorDestinationBlend = Blend.One;

                try
                {
                    activeRenderTarget = new RenderTarget2D(graphicsDevice,
                                                             graphicsDevice.Viewport.Width,
                                                             graphicsDevice.Viewport.Height, false, SurfaceFormat.Color, DepthFormat.None, 0,
                                                             RenderTargetUsage.PreserveContents);
                    inactiveRenderTarget = new RenderTarget2D(graphicsDevice,
                                                            graphicsDevice.Viewport.Width,
                                                            graphicsDevice.Viewport.Height, false, SurfaceFormat.Color, DepthFormat.None, 0,
                                                            RenderTargetUsage.PreserveContents);
                }
                catch (InvalidOperationException)
                {
                    Trace.WriteLine("Could not create render target for channels", "UI");

                    return null;
                }

                //            Geometry.Rectangle renderTargetBounds = scene.VisibleWorldBounds;

                //          Debug.Assert(graphicsDevice.Viewport.Width == ClientRectangle.Width); 

                //Create a basic mesh to blend the textures onto the screen

                //  TopRight.X = System.Math.Ceiling(TopRight.X);
                //  TopRight.Y = System.Math.Ceiling(TopRight.Y);
                Geometry.Rectangle Bounds = scene.VisibleWorldBounds;
                double HalfWidth = Bounds.Width / 2;
                double HalfHeight = Bounds.Height / 2;
                Geometry.Vector2 BotLeft = new(Bounds.Center.X - HalfWidth, Bounds.Center.Y + HalfHeight);
                Geometry.Vector2 TopRight = new(Bounds.Center.X + HalfWidth, Bounds.Center.Y - HalfHeight);
                VertexPositionNormalTexture[] mesh = [
                           new( new Vector3((float)BotLeft.X, (float)BotLeft.Y, 0), Vector3.UnitZ, new Vector2(0,0)),
                           new( new Vector3((float)TopRight.X, (float)BotLeft.Y, 0), Vector3.UnitZ,  new Vector2(1,0)),
                           new( new Vector3((float)BotLeft.X, (float)TopRight.Y, 0), Vector3.UnitZ,   new Vector2(0,1)),
                           new( new Vector3((float)TopRight.X, (float)TopRight.Y, 0), Vector3.UnitZ, new Vector2(1,1))];

                graphicsDevice.SetRenderTargets(inactiveRenderTarget);
                graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(0, 0, 0, 0));
                graphicsDevice.SetRenderTargets(activeRenderTarget);
                graphicsDevice.Clear(new Microsoft.Xna.Framework.Color(0, 0, 0, 0));

                mergeHSVImagesEffect.PrepareMergeRGBImage(inactiveRenderTarget, channels[0], new Microsoft.Xna.Framework.Color(Colors[0].X, Colors[0].Y, Colors[0].Z, Colors[0].W));

                for (int i = 0; i < channels.Length; i++)
                {
                    mergeHSVImagesEffect.BaseTexture = inactiveRenderTarget;
                    mergeHSVImagesEffect.OverlayTexture = channels[i] as Texture2D;
                    mergeHSVImagesEffect.OverlayColorScalar = new Microsoft.Xna.Framework.Vector4(Colors[i].X / colorStats.ChannelColorSum[0],
                                                                                                Colors[i].Y / colorStats.ChannelColorSum[1],
                                                                                                Colors[i].Z / colorStats.ChannelColorSum[2],
                                                                                                Colors[i].W / colorStats.ChannelColorSum[3]);
                    mergeHSVImagesEffect.OverlayColor = new Microsoft.Xna.Framework.Color(Colors[i].X,
                        Colors[i].Y,
                        Colors[i].Z,
                        Colors[i].W);

                    foreach (EffectPass pass in mergeHSVImagesEffect.effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

                        graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                                                                                mesh, 0, mesh.Length,
                                                                                indicies, 0, indicies.Length / 3);
                    }

                    //Swap the render targets so we can add the next texture to the running sum 
                    (inactiveRenderTarget, activeRenderTarget) = (activeRenderTarget, inactiveRenderTarget);
                    graphicsDevice.SetRenderTargets(activeRenderTarget);
                }

                //When the loop exits the inactive render target has the sum of each channel and is set as the render target.  We need to normalize the result.


                mergeHSVImagesEffect.PrepareRGBToHCL(inactiveRenderTarget);
                foreach (EffectPass pass in mergeHSVImagesEffect.effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(PrimitiveType.TriangleList,
                        mesh, 0, mesh.Length,
                        indicies, 0, indicies.Length / 3);
                }

                //graphicsDevice.Viewport = oldViewport;
                graphicsDevice.Textures[0] = null;

            }
            finally
            {
                graphicsDevice.BlendState = oldBlendState;
                graphicsDevice.SetRenderTargets(null);
                inactiveRenderTarget?.Dispose();
            }

            //       SaveTexture(renderOverlayTarget, "D:\\Temp\\MergeRGB.png");

            return activeRenderTarget;
        }

        private static void SaveTexture(Texture2D texture, string filename)
        {
            try
            {
                using System.IO.FileStream saveFile = System.IO.File.OpenWrite(filename);
                texture?.SaveAsPng(saveFile, texture.Width, texture.Height);
            }
            catch (System.IO.IOException)
            {
            }
        }


        [DllImport("user32.dll")]
        private static extern uint GetQueueStatus(uint flags);

        private const uint QS_PAINT = 0x0020;

        private void timer_Tick(object sender, EventArgs e)
        {
            Form? form = _hostForm ?? FindForm();
            if (form?.WindowState == FormWindowState.Minimized)
            {
                timer.Enabled = false;
                return;
            }

            if (!HavePaintInQueue())
                this.Invalidate();
        }

        private bool HavePaintInQueue() => (GetQueueStatus(QS_PAINT) & QS_PAINT) != 0;

        protected void SetOverlayVisiblity(bool ControlDown, bool SpaceDown)
        {
            if (SpaceDown)
            {
                ShowOnlyOverlays = ControlDown;
                ShowOverlays = ControlDown;
            }
            else
            {
                ShowOnlyOverlays = false;
                ShowOverlays = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.C:
                    if (e.Control == true)
                    {
                        if (Section is null)
                            break;

                        //On Ctrl+C, copy current mouse position to keyboard
                        Geometry.Vector2 Pos = StatusPosition;
                        string PosText = Util.CoordinatesToCopyPaste(Pos.X, Pos.Y, Section.Number, Downsample);
                        Clipboard.SetText(PosText);

                    }
                    break;
                case Keys.Space:
                    SetOverlayVisiblity(e.Control, true);
                    this.Invalidate();
                    break;
                case Keys.Control:
                case Keys.ControlKey:
                    SetOverlayVisiblity(true, System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Space));
                    this.Invalidate();
                    break;
                case Keys.F1:
                    if (commandHelpText != null)
                    {
                        this.commandHelpText.IsDropDownOpen = !this.commandHelpText.IsDropDownOpen;
                        this.timerHelpTextChange.Enabled = !this.commandHelpText.IsDropDownOpen;
                    }
                    break;
            }

            base.OnKeyDown(e);
        }


        protected override void OnKeyUp(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Control:
                case Keys.ControlKey:
                    SetOverlayVisiblity(false, System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Space));
                    this.Invalidate();
                    break;
                case Keys.Space:
                    SetOverlayVisiblity(e.Control, false);
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

            if (e.Button == MouseButtons.Left)
            {
                Geometry.Vector2 worldPosition = this.ScreenToWorld(e.X, e.Y);

                if (upSectionButton != null && upSectionButton.Contains(worldPosition))
                    upSectionButton.OnClick(upSectionButton, worldPosition, VikingXNAGraphics.Controls.InputDevice.Mouse, VikingXNAGraphics.Controls.MouseButton.LEFT);

                if (downSectionButton != null && downSectionButton.Contains(worldPosition))
                    downSectionButton.OnClick(downSectionButton, worldPosition, VikingXNAGraphics.Controls.InputDevice.Mouse, VikingXNAGraphics.Controls.MouseButton.LEFT);
            }
        }

        private bool OnDownSectionButtonClicked(IClickable sender, Geometry.Vector2 position, VikingXNAGraphics.Controls.InputDevice source, object input_state)
        {
            if (source == VikingXNAGraphics.Controls.InputDevice.Mouse)
            {
                VikingXNAGraphics.Controls.MouseButton button = (VikingXNAGraphics.Controls.MouseButton)input_state;
                if (button == MouseButton.LEFT)
                {
                    this.StepDownNSections(1);
                    return true;
                }
            }
            else if (source == VikingXNAGraphics.Controls.InputDevice.Pen)
            {
                this.StepDownNSections(1);
                return true;
            }

            return false;
        }

        private bool OnUpSectionButtonClicked(IClickable sender, Geometry.Vector2 position, VikingXNAGraphics.Controls.InputDevice source, object input_state)
        {
            if (source == VikingXNAGraphics.Controls.InputDevice.Mouse)
            {
                VikingXNAGraphics.Controls.MouseButton button = (VikingXNAGraphics.Controls.MouseButton)input_state;
                if (button == MouseButton.LEFT)
                {
                    this.StepUpNSections(1);
                    return true;
                }
            }
            else if (source == VikingXNAGraphics.Controls.InputDevice.Pen)
            {
                this.StepUpNSections(1);
                return true;
            }

            return false;
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

                System.Threading.Tasks.Task.Run(TileViewModelCacheCheckpointAction);
                System.Threading.Tasks.Task.Run(TileCacheCheckpointAction);

                //TileViewModelCacheCheckpointAction.BeginInvoke(null, null);
                //TileCacheCheckpointAction.BeginInvoke(null, null);

                //Global.TileViewModelCache.Checkpoint();
                //Viking.VolumeModel.Global.TileCache.Checkpoint();

                //DrawCallSinceTileCacheCheckpoint = false;

                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate () { DrawCallSinceTileCacheCheckpoint = false; }), DispatcherPriority.Background, null);
            }
        }


        private void menuSection_DropDownOpening(object sender, EventArgs e)
        {
            menuSectionUseSpecific.Checked = State.UseSectionSpecificTransform;
            menuSectionShowMesh.Checked = State.ShowStosMesh;
            menuSectionShowTileMesh.Checked = State.ShowTileMesh;

            if (State.volume is null || Section is null)
                return;

            List<ToolStripItem> items = [];

            foreach (string t in Section.TilesetNames)
            {
                ToolStripMenuItem menuItem = new(t, null, OnSectionTilesetClick);
                if (t == CurrentTransform)
                    menuItem.Checked = true;

                items.Add(menuItem);
            }

            foreach (string t in Section.ImagePyramids.Keys.ToArray())
            {
                ToolStripMenuItem menuItem = new(t);
                if (t == CurrentTransform)
                    menuItem.Checked = true;

                AddTransformsToSectionChannelPyramidMenuItem(menuItem);
                items.Add(menuItem);
            }

            menuSectionChannel.DropDownItems.Clear();
            menuSectionChannel.DropDownItems.AddRange([.. items]);


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
                if (menuChannelPyramid is null)
                    return;

                List<ToolStripItem> items = [];
                items.Clear();
                menuChannelPyramid.DropDownItems.Clear();

                foreach (string t in Section.PyramidTransformNames)
                {
                    ToolStripMenuItem menuItem = new(t, null, OnSectionPyramidTransformClick);
                    if (t == CurrentTransform)
                        menuItem.Checked = true;

                    items.Add(menuItem);
                }

                menuChannelPyramid.DropDownItems.AddRange([.. items]);
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
            if (sender is not ToolStripMenuItem menuItem)
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
            if (sender is not ToolStripMenuItem menuItem)
                return;

            CurrentChannel = menuItem.Text;
            CurrentTransform = menuItem.Text;

            this.Refresh();
        }

        private void useSectionSpecificTransformsToolStripMenuItem_Click(object sender, EventArgs e) => State.UseSectionSpecificTransform = !State.UseSectionSpecificTransform;

        private void menuSectionShowMesh_Click_1(object sender, EventArgs e) => State.ShowStosMesh = !State.ShowStosMesh;

        private async void menuExportFrames_Click(object sender, EventArgs e)
        {
            try
            {
                using Viking.UI.Forms.FrameCapturesForm form = new();
                DialogResult result = form.ShowDialog();
                if (result == DialogResult.Cancel)
                    return;

                //Capture each of the requested frames
                using GenericProgressForm progressForm = new();
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
                    await this.ExportImage(frame.Filename, frame.Rect, Z, frame.downsample, frame.IncludeOverlay);
                    //var task = System.Threading.Tasks.Task.Run(() => this.ExportImage(frame.Filename, frame.Rect, Z, frame.downsample, frame.IncludeOverlay));
                    //task.Wait();
                    progressForm.ShowProgress("Exported frame: " + frame.Filename, (double)i / (double)form.Frames.Length);
                    //System.Windows.Forms.Application.DoEvents();
                    if (progressForm.DialogResult == DialogResult.Cancel)
                        break;
                }

                progressForm.Close();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"menuExportFrames_Click failed: {ex}", "SectionViewerControl");
            }
        }

        private void menuGoToLocation_Click(object sender, EventArgs e) => ShowGoToLocationForm();

        protected void ShowGoToLocationForm()
        {
            Viking.UI.Forms.GoToLocationForm form = null;
            try
            {
                form = new GoToLocationForm
                {
                    X = Camera.LookAt.X,
                    Y = Camera.LookAt.Y
                };

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
                form?.Dispose();
                form = null;
            }
        }

        public void GoToLocation(Vector2 location, int Z) => GoToLocation(location, Z, false, this.Downsample);

        public void GoToLocation(Vector2 location, int Z, double newDownsample) => GoToLocation(location, Z, false, newDownsample);

        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace) => GoToLocation(location, Z, InputInSectionSpace, this.Camera.Downsample);

        public void GoToLocation(Vector2 location, int Z, bool InputInSectionSpace, double newDownsample)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => GoToLocation(location, Z, InputInSectionSpace, newDownsample)));
                return;
            }

            if (UI.State.volume is null)
                return;
            if (UI.State.volume.SectionViewModels.ContainsKey(Z) == false)
            {
                MessageBox.Show("There is no section # " + Z.ToString() + " in the volume.", "Error", MessageBoxButtons.OK);
                return;
            }

            SectionViewModel newSection = UI.State.volume.SectionViewModels[Z];
            this.Section = newSection;

            if (InputInSectionSpace)
            {
                MappingBase map = this.Section.VolumeViewModel.GetTileMapping(Volume.ActiveVolumeTransform, this.Section.Number, this.CurrentChannel, this.CurrentTransform);
                if (map != null)
                {
                    bool Mapped = map.TrySectionToVolume(new Geometry.Vector2(location.X, location.Y), out Geometry.Vector2 TransformedPoint);

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
            this.StatusPosition = new Geometry.Vector2(location.X, location.Y);
            this.StatusSection = this.Section.Number;
            this.StatusMagnification = newDownsample;

            //Redraw since we are at a new location
            this.Refresh();
        }

        private void menuCaptureScreen_Click(object sender, EventArgs e) => this.CurrentCommand = new Viking.UI.Commands.ScreenCaptureCommand(this);

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
            if (Section is null)
                return;
            using SetupChannelsForm ChannelSetup = new(this.Section.VolumeViewModel.DefaultChannels, this.Section.VolumeViewModel.ChannelNames);
            if (ChannelSetup.ShowDialog() == DialogResult.OK)
            {
                this.Section.VolumeViewModel.DefaultChannels = ChannelSetup.ChannelInfo;
                this.Invalidate();
            }

        }

        private void OnVolumeTransformClicked(object sender, EventArgs e)
        {
            ToolStripMenuItem? menuItem = sender as ToolStripMenuItem;
            if (menuItem is null)
                return;

            Volume.ActiveVolumeTransform = menuItem.Text.ToLower() == "none" ? null : menuItem.Text;
        }

        private void OnVolumeTransformChanged(object sender, TransformChangedEventArgs e)
        {
            //TODO: Cancel the active command
            InvalidateSectionTextureCache();
            this.Invalidate();
        }

        private void OnSectionTransformChanged(object sender, TransformChangedEventArgs e) =>
            //TODO: Cancel the active command
            this.Invalidate();

        private void OnSectionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //TODO: Cancel the active command
            System.Diagnostics.Trace.WriteLine($"SectionViewerControl: Property '{e.PropertyName}' changed, invalidating", "SectionViewerControl");
            this.Invalidate();
        }

        private void menuVolume_DropDownOpening(object sender, EventArgs e)
        {
            menuVolumeTransforms.DropDownItems.Clear();

            ToolStripMenuItem menuNoneItem = new("None", null, OnVolumeTransformClicked);
            menuVolumeTransforms.DropDownItems.Add(menuNoneItem);

            foreach (string VolumeTransform in Viking.UI.State.volume.TransformNames)
            {
                ToolStripMenuItem menuItem = new(VolumeTransform, null, OnVolumeTransformClicked)
                {
                    Checked = Volume.ActiveVolumeTransform == VolumeTransform
                };
                menuVolumeTransforms.DropDownItems.Add(menuItem);
            }
        }

        private void menuColorizeTiles_Click(object sender, EventArgs e) => menuColorizeTiles.Checked = !menuColorizeTiles.Checked;

        private void menuExportTiles_Click(object sender, EventArgs e)
        {
            using TileExportForm exportProperties = new();
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

            Task.Run(() => ExportTiles(exportProperties.ExportPath, FirstExportSection, LastExportSection, exportProperties.Downsample, CancellationToken.None));
        }

        private void menuSectionShowTileMesh_Click(object sender, EventArgs e) => State.ShowTileMesh = !State.ShowTileMesh;

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

        private void timerHelpTextChange_Tick(object sender, EventArgs e)
        {
            if (this.commandHelpText is null)
                return;

            this.commandHelpText.TextArrayIndex++;
        }

        private void menuShowCommandHelp_Click(object sender, EventArgs e)
        {
            this.commandHelpTextScrollerHost.Visible = !this.commandHelpTextScrollerHost.Visible;
            menuShowCommandHelp.Checked = this.commandHelpTextScrollerHost.Visible;
            timerHelpTextChange.Enabled = this.commandHelpTextScrollerHost.Visible;

            Viking.Properties.Settings.Default.ShowCommandHelp = this.commandHelpTextScrollerHost.Visible;
            Viking.Properties.Settings.Default.Save();
        }

        #region Section Number Overlay

        /// <summary>
        /// Initialize the section number overlay with settings and callbacks
        /// </summary>
        private void InitializeSectionNumberOverlay()
        {
            sectionNumberOverlay = new SectionNumberOverlayView
            {
                Enabled = Viking.Properties.Settings.Default.SectionNumberOverlayEnabled,
                SectionsAboveBelow = Viking.Properties.Settings.Default.SectionNumberOverlayCount,
                Edge = ParseOverlayEdge(Viking.Properties.Settings.Default.SectionNumberOverlayEdge),
                Opacity = Viking.Properties.Settings.Default.SectionNumberOverlayOpacity,
                MinOpacityForNonCenterSections = Viking.Properties.Settings.Default.SectionNumberOverlayMinOpacityNonCenter,
                CenterMagnification = Viking.Properties.Settings.Default.SectionNumberOverlayCenterMagnification,
                SectionExistsFunc = SectionExistsInVolume,
                HasTransformsFunc = VolumeHasTransforms,
                // PID parameters
                /*
                PidProportionalGain = Viking.Properties.Settings.Default.SectionNumberOverlayPidProportionalGain,
                PidDerivativeGain = Viking.Properties.Settings.Default.SectionNumberOverlayPidDerivativeGain,
                PidIntegralGain = Viking.Properties.Settings.Default.SectionNumberOverlayPidIntegralGain,
                PidVelocityThresholdScreenHeights = Viking.Properties.Settings.Default.SectionNumberOverlayPidVelocityThreshold,
                PidPositionThresholdScreenHeights = Viking.Properties.Settings.Default.SectionNumberOverlayPidPositionThreshold
                */
            };

            // Configure the acceleration from settings (in screen-height units)
            //sectionNumberOverlay.AccelerationInScreenHeights = Viking.Properties.Settings.Default.SectionNumberOverlayAcceleration;

            // Update min/max section numbers from volume if available
            if (State.volume != null)
            {
                var sections = State.volume.SectionViewModels;
                if (sections.Count > 0)
                {
                    sectionNumberOverlay.MinSectionNumber = sections.Keys.Min();
                    sectionNumberOverlay.MaxSectionNumber = sections.Keys.Max();
                }
            }

            // Initialize with current section if available
            if (Section != null)
            {
                sectionNumberOverlay.Initialize(Section.Number);
            }
        }

        /// <summary>
        /// Update the section number overlay when section changes
        /// </summary>
        private void UpdateSectionNumberOverlay()
        {
            if (sectionNumberOverlay == null || Section is null)
                return;

            // Update min/max section numbers from volume
            if (State.volume != null)
            {
                var sections = State.volume.SectionViewModels;
                if (sections.Count > 0)
                {
                    sectionNumberOverlay.MinSectionNumber = sections.Keys.Min();
                    sectionNumberOverlay.MaxSectionNumber = sections.Keys.Max();
                }
            }

            // Update the current section (triggers animation)
            sectionNumberOverlay.SetCurrentSection(Section.Number);
        }

        /// <summary>
        /// Draw the section number overlay
        /// </summary>
        private void DrawSectionNumberOverlay(GraphicsDevice graphicsDevice, Scene scene)
        {
            if (sectionNumberOverlay == null || spriteBatch == null || fontArial == null)
                return;

            if (!sectionNumberOverlay.Enabled)
                return;

            // Only show overlay when volume has multiple sections
            if (State.volume == null || State.volume.SectionViewModels.Count <= 1)
                return;

            // Draw the overlay (animation uses precomputed trajectory sampled in Draw)
            sectionNumberOverlay.Draw(
                spriteBatch,
                fontArial,
                graphicsDevice.Viewport.Width,
                graphicsDevice.Viewport.Height
            );
        }

        /// <summary>
        /// Check if a section number exists in the volume
        /// </summary>
        private bool SectionExistsInVolume(int sectionNumber)
        {
            if (State.volume == null)
                return true;

            return State.volume.SectionViewModels.ContainsKey(sectionNumber);
        }

        /// <summary>
        /// Check if the volume has slice-to-slice transforms
        /// </summary>
        private bool VolumeHasTransforms()
        { 
            // Check if any section has an active tile transform
            if (State.volume == null)
                return false;

            if(!string.IsNullOrEmpty(State.volume.DefaultVolumeTransform))
                return true; 

            // If UseSectionSpecificTransform is enabled, transforms exist
            if (!string.IsNullOrEmpty(State.volume.ActiveVolumeTransform))
                return true;
             
            return false;
        }

        /// <summary>
        /// Parse the overlay edge setting string to enum
        /// </summary>
        private static OverlayEdge ParseOverlayEdge(string edge)
        {
            if (string.Equals(edge, "Right", StringComparison.OrdinalIgnoreCase))
                return OverlayEdge.Right;
            return OverlayEdge.Left;
        }

        /// <summary>
        /// Static reference to the viewer preferences dialog (modeless)
        /// </summary>
        private static Viking.UI.WPF.Forms.ViewerPreferencesDialog? _viewerPreferencesDialog;

        /// <summary>
        /// Open the Viewer Preferences dialog
        /// </summary>
        private void menuViewerPreferences_Click(object sender, EventArgs e)
        {
            // If dialog already exists and is open, just focus it
            if (_viewerPreferencesDialog != null && !_viewerPreferencesDialog.IsClosed)
            {
                _viewerPreferencesDialog.Focus();
                return;
            }

            // Create ViewModel and load current settings
            var viewModel = new Viking.UI.WPF.Forms.ViewerPreferencesDialogViewModel();
            viewModel.LoadCurrentSettings(
                Viking.Properties.Settings.Default.SectionNumberOverlayEnabled,
                Viking.Properties.Settings.Default.SectionNumberOverlayCount,
                Viking.Properties.Settings.Default.SectionNumberOverlayAcceleration,
                Viking.Properties.Settings.Default.SectionNumberOverlayEdge,
                Viking.Properties.Settings.Default.SectionNumberOverlayOpacity,
                Viking.Properties.Settings.Default.SectionNumberOverlayMinOpacityNonCenter,
                Viking.Properties.Settings.Default.SectionNumberOverlayCenterMagnification,
                Viking.Properties.Settings.Default.SectionNumberOverlayPidProportionalGain,
                Viking.Properties.Settings.Default.SectionNumberOverlayPidDerivativeGain,
                Viking.Properties.Settings.Default.SectionNumberOverlayPidIntegralGain,
                Viking.Properties.Settings.Default.SectionNumberOverlayPidVelocityThreshold,
                Viking.Properties.Settings.Default.SectionNumberOverlayPidPositionThreshold,
                Viking.Properties.Settings.Default.TextureLoadingWindow,
                Viking.Properties.Settings.Default.MinTexturesToLoadFromQueue,
                Viking.Properties.Settings.Default.VisibleTileSortIntervalMs,
                Viking.Properties.Settings.Default.MaxConcurrentTextureRequests,
                Viking.Properties.Settings.Default.LoadAdjacentSectionTextures
            );

            // Wire up real-time preview for settings changes
            viewModel.SectionNumberOverlaySettingsChanged += () =>
            {
                ApplySectionNumberOverlaySettings(viewModel);
            };

            _viewerPreferencesDialog = new Viking.UI.WPF.Forms.ViewerPreferencesDialog(viewModel);

            // Wire up event handlers to save settings
            _viewerPreferencesDialog.ApplyClicked += (s, args) => SaveViewerPreferencesFromViewModel(viewModel);
            _viewerPreferencesDialog.OkClicked += (s, args) => SaveViewerPreferencesFromViewModel(viewModel);

            _viewerPreferencesDialog.Show(); // Modeless dialog
        }

        /// <summary>
        /// Apply section number overlay settings from viewmodel for real-time preview
        /// </summary>
        private void ApplySectionNumberOverlaySettings(Viking.UI.WPF.Forms.ViewerPreferencesDialogViewModel viewModel)
        {
            if (sectionNumberOverlay == null)
                return;

            sectionNumberOverlay.Enabled = viewModel.SectionNumberOverlayEnabled;
            sectionNumberOverlay.SectionsAboveBelow = viewModel.SectionNumberOverlayCount;
            sectionNumberOverlay.AccelerationInScreenHeights = viewModel.SectionNumberOverlayAcceleration;
            sectionNumberOverlay.Edge = ParseOverlayEdge(viewModel.SectionNumberOverlayEdge);
            sectionNumberOverlay.Opacity = viewModel.SectionNumberOverlayOpacity;
            sectionNumberOverlay.MinOpacityForNonCenterSections = viewModel.SectionNumberOverlayMinOpacityNonCenter;
            sectionNumberOverlay.CenterMagnification = viewModel.SectionNumberOverlayCenterMagnification;
            
            // PID parameters
            sectionNumberOverlay.PidProportionalGain = viewModel.PidProportionalGain;
            sectionNumberOverlay.PidDerivativeGain = viewModel.PidDerivativeGain;
            sectionNumberOverlay.PidIntegralGain = viewModel.PidIntegralGain;
            sectionNumberOverlay.PidVelocityThresholdScreenHeights = viewModel.PidVelocityThreshold;
            sectionNumberOverlay.PidPositionThresholdScreenHeights = viewModel.PidPositionThreshold;

            this.Invalidate();
        }

        /// <summary>
        /// Save viewer preferences from viewmodel to settings
        /// </summary>
        private static void SaveViewerPreferencesFromViewModel(Viking.UI.WPF.Forms.ViewerPreferencesDialogViewModel viewModel)
        {
            Viking.Properties.Settings.Default.SectionNumberOverlayEnabled = viewModel.SectionNumberOverlayEnabled;
            Viking.Properties.Settings.Default.SectionNumberOverlayCount = viewModel.SectionNumberOverlayCount;
            Viking.Properties.Settings.Default.SectionNumberOverlayAcceleration = viewModel.SectionNumberOverlayAcceleration;
            Viking.Properties.Settings.Default.SectionNumberOverlayEdge = viewModel.SectionNumberOverlayEdge;
            Viking.Properties.Settings.Default.SectionNumberOverlayOpacity = viewModel.SectionNumberOverlayOpacity;
            Viking.Properties.Settings.Default.SectionNumberOverlayMinOpacityNonCenter = viewModel.SectionNumberOverlayMinOpacityNonCenter;
            Viking.Properties.Settings.Default.SectionNumberOverlayCenterMagnification = viewModel.SectionNumberOverlayCenterMagnification;
            Viking.Properties.Settings.Default.SectionNumberOverlayPidProportionalGain = viewModel.PidProportionalGain;
            Viking.Properties.Settings.Default.SectionNumberOverlayPidDerivativeGain = viewModel.PidDerivativeGain;
            Viking.Properties.Settings.Default.SectionNumberOverlayPidIntegralGain = viewModel.PidIntegralGain;
            Viking.Properties.Settings.Default.SectionNumberOverlayPidVelocityThreshold = viewModel.PidVelocityThreshold;
            Viking.Properties.Settings.Default.SectionNumberOverlayPidPositionThreshold = viewModel.PidPositionThreshold;
            Viking.Properties.Settings.Default.TextureLoadingWindow = viewModel.TextureLoadingWindow;
            Viking.Properties.Settings.Default.MinTexturesToLoadFromQueue = viewModel.MinTexturesToLoadFromQueue;
            Viking.Properties.Settings.Default.VisibleTileSortIntervalMs = viewModel.VisibleTileSortIntervalMs;
            Viking.Properties.Settings.Default.MaxConcurrentTextureRequests = viewModel.MaxConcurrentTextureRequests;
            Viking.Properties.Settings.Default.LoadAdjacentSectionTextures = viewModel.LoadAdjacentSectionTextures;
            Viking.Properties.Settings.Default.Save();
            Viking.PendingTextureQueue.UpdateSortInterval(viewModel.VisibleTileSortIntervalMs);
            Viking.TextureReaderV2.ApplyMaxConcurrentRequestPreference(
                viewModel.MaxConcurrentTextureRequests,
                Section?.VolumeViewModel?.DefaultTileWidth);
        }

        #endregion
    }
}

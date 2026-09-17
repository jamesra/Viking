using System;
using System.Diagnostics;
using Viking.Common;
using Viking.ViewModels;

namespace Viking.UI
{
    public class State
    {
        public static VikingMain Appwindow;

        public static System.Windows.Forms.Form MdiParent => State.Appwindow;

        /// <summary>
        /// Dispatcher for invoking methods on the main thread. 
        /// </summary>
        public static System.Windows.Threading.Dispatcher MainThreadDispatcher;

        public static Viking.UI.Forms.SectionViewerForm ViewerForm;

        public static void InvalidateViewerControl() => ViewerControl?.Invalidate();

        /// <summary>
        /// The section viewer control for creating commands
        /// 
        /// This is not going in the right direction for supporting multiple viewer controls,
        /// but that is a major rewrite and I needed the extensions to work cleanly.
        /// </summary>
        public static Viking.UI.Controls.SectionViewerControl? ViewerControl => ViewerForm?.SectionControl;


        public static string CurrentMode = "";



        //Stores userAccessLevel for the profided credentials: Include: Admin, Modify, Read
        public static string[] UserAccessLevel;

        //Current user access level as a single string value
        public static string userAccessLevel = "Exit";

        //User credentials used during authentication
        public static Duende.IdentityModel.Client.TokenResponse? UserBearerToken = null;

        public static System.Net.NetworkCredential UserCredentials = new("anonymous", "connectome");

        public static readonly System.Net.NetworkCredential AnonymousCredentials = new("anonymous", "connectome");

        private static readonly string CacheSubPath = "Cache";
        public static readonly string CachePath = System.Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\" + CacheSubPath;

        public static string VolumeCachePath
        {
            get
            {
                if (volume != null)
                {
                    return GetVolumeCachePath(volume.Name);
                }

                throw new InvalidOperationException("Requesting Volume Cache Path before volume.name is available");

            }
        }

        public static string GetVolumeCachePath(string VolumeName) => System.IO.Path.Combine(CachePath, VolumeName);

        public static string TextureCachePath => System.IO.Path.Combine(State.VolumeCachePath, "Textures");

        public static void ClearVolumeTextureCache()
        {
            if (System.IO.Directory.Exists(State.TextureCachePath))
            {
                System.IO.Directory.Delete(State.TextureCachePath, true);
                System.IO.Directory.CreateDirectory(State.TextureCachePath);
            }
        }

        static State()
        {

        }

        #region Events

        /// <summary>
        /// Fires when the user asks to show/hide a control type
        /// </summary>
        public static event ViewChangeEventHandler ViewChanged;

        #endregion

        #region Drag Drop Code

        public static System.Windows.Forms.MouseButtons DragDropButton;
        private static IUIObject? _DragDropObject;

        public static IUIObject? DragDropObject
        {
            get => _DragDropObject;
            set => _DragDropObject = value;
        }

        /// <summary>
        /// When an image is dragged we want to draw the image relative to where the
        /// image center was when the person started the drag operation. 
        /// </summary>
        public static System.Drawing.Point DragDropOrigin = new(0, 0);
        #endregion 

        #region Selection State

        private static IUIObjectBasic? _SelectedObject;

        /// <summary>
        /// The currently selected object in the UI
        /// </summary>
        public static IUIObjectBasic? SelectedObject
        {
            get => _SelectedObject;
            set
            {
                bool FireEvent = _SelectedObject != value;
                _SelectedObject = value;
                if (FireEvent && ItemSelected != null)
                {
                    Viking.Common.ObjectSelectedEventArgs Args = new(value);
                    ItemSelected(value, Args);
                }
                if (value != null)
                {
                    Trace.WriteLine("Selected Object: " + value.ToString(), "UI");
                }
                else
                {
                    Trace.WriteLine("Selected Object: null", "UI");
                }
            }
        }

        /// <summary>
        /// Fired when an object is selected in the UI
        /// </summary>
        public static event ObjectSelectedEventHandler ItemSelected;

        #endregion


        /// <summary>
        /// If this is true we remember the last transform used for each section and switch to that transform if we display that section
        /// If false we use the transform from the last section we viewed if it is available. 
        /// </summary>
        public static bool UseSectionSpecificTransform = false;

        /// <summary>
        /// Set to true if we want to display the transform mesh used to create the image
        /// </summary>
        public static bool ShowStosMesh = false;

        /// <summary>
        /// Set to true if we want to show the mesh for indi
        /// </summary>
        public static bool ShowTileMesh = false;

        private static VolumeViewModel? _volume = null;

        /// <summary>
        /// The volume currently being viewed
        /// </summary>
        public static VolumeViewModel volume
        {
            get => _volume;
            set => _volume = value;
        }

        /// <summary>
        /// Identity resource name for the open volume (e.g. RC2). Used for SBFSEM-tools deep links.
        /// Falls back to VikingXML volume name when the user opened a URL without the Identity tree.
        /// </summary>
        public static string? IdentityVolumeName { get; set; }

        /// <summary>
        /// Volume endpoint URL currently open (normalized at login). Used to match viking:// deep links
        /// for same-volume single-instance activation.
        /// </summary>
        public static string? VolumeUrl { get; set; }

        /// <summary>
        /// Base URL for opening a cell in SBFSEM-tools (default https://sbfsem-tools.com/open).
        /// Used by Identity bounce after auth; the menu opens the bounce URL, not this directly.
        /// </summary>
        public static string SbfsemToolsOpenUrl { get; set; } = "https://sbfsem-tools.com/open";

        /// <summary>
        /// Identity WebManagement bounce URL for Open in SBFSEM-tools
        /// (default https://identity.codepharm.net:4001/SbfsemOpen/Redirect).
        /// </summary>
        public static string SbfsemToolsIdentityBounceUrl { get; set; } = "https://identity.codepharm.net:4001/SbfsemOpen/Redirect";

        /// <summary>
        /// Arguments passed to Viking on startup
        /// </summary>
        public static System.Collections.Specialized.NameValueCollection StartupArguments = [];

        /// <summary>
        /// Registered by WebAnnotation to navigate to a Location ID (AskServer). Used by deep-link activation.
        /// </summary>
        public static Action<long>? GoToAnnotationLocation { get; set; }

    }
}

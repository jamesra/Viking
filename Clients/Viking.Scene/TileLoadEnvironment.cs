using System;
using System.Windows.Threading;
using Microsoft.Xna.Framework.Graphics;
using Viking.VolumeModel;
using Rectangle = Geometry.Rectangle;

namespace Viking
{
    /// <summary>
    /// Process-wide hooks so the shared tile/texture pipeline does not take a WinForms viewer type.
    /// </summary>
    public static class TileLoadEnvironment
    {
        public static Volume? Volume { get; set; }

        public static string CachePath { get; set; } =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\Cache";

        /// <summary>
        /// Per-volume texture cache folder. Set by BindVolume; StartTexturePipeline does not change this.
        /// </summary>
        public static string TextureCachePath { get; set; } = CachePath;

        /// <summary>
        /// Dispatcher that owns the GraphicsDevice. PendingTextureQueue creates Texture2Ds here;
        /// if this is null when a tile finishes decoding, the pump pauses and never uploads.
        /// </summary>
        public static Dispatcher? UiDispatcher { get; set; }

        /// <summary>
        /// GraphicsDevice for texture upload. PendingTextureQueue skips work while this returns null.
        /// </summary>
        public static Func<GraphicsDevice?>? GetDevice { get; set; }

        /// <summary>Used by the sort timer to prioritize tiles in the current camera frustum.</summary>
        public static Func<Rectangle>? GetVisibleWorldBounds { get; set; }

        public static Func<int>? GetSectionNumber { get; set; }

        public static Func<double>? GetDownsample { get; set; }

        public static int VisibleTileSortIntervalMs { get; set; } = 1000;

        public static int MinTexturesToLoadFromQueue { get; set; } = 3;

        public static int TextureLoadingWindowMs { get; set; } = 30;

        /// <summary>
        /// Set by the viewport host so texture uploads can request another present without a busy render loop.
        /// </summary>
        public static Action? RequestRender { get; set; }

        /// <summary>
        /// True while HTTP/decode or GPU upload work is outstanding, or DrawTiles asked
        /// for another Present because visible tiles were empty or still loading.
        /// </summary>
        public static bool HasTexturePipelineWork =>
            FollowUpPresents > 0
            || !PendingTextureQueue.IsEmpty
            || TextureRequestQueue.HasPending;

        /// <summary>
        /// Set by DrawTiles when the current pass is not a finished textured frame.
        /// HasTexturePipelineWork includes this so the on-demand present loop cannot idle
        /// before RequestRender is hooked or Task.Run has called EnqueueRequest.
        /// </summary>
        public static int FollowUpPresents { get; set; }

        /// <summary>
        /// Upload decoded tiles on the UI thread before Present so HTTP workers
        /// are not blocked behind CompositionTarget.Rendering.
        /// </summary>
        public static void PumpPendingTexturesOnUiThread() => PendingTextureQueue.PumpOnUiThread();

        public static TileViewModelCache TileViewModelCache { get; } = new();

        public static LocalTextureCache TextureCache { get; } = new();

        /// <summary>
        /// Viking starts these from VikingMain_Load. Jotunn must do the same or decoded
        /// tiles never become Texture2Ds and the view stays black.
        /// </summary>
        public static void StartTexturePipeline()
        {
            TextureRequestQueue.StartWorkers();
            _ = PendingTextureQueue.PostPump();
            PendingTextureQueue.StartSortTimer();
        }

        /// <summary>
        /// Points the texture cache at this volume's name. Call whenever the open volume changes;
        /// StartTexturePipeline does not do this.
        /// </summary>
        public static void BindVolume(Volume volume)
        {
            Volume = volume;
            FollowUpPresents = 0;
            if (volume == null)
                return;

            TextureCachePath = System.IO.Path.Combine(CachePath, volume.Name, "Textures");
            if (!System.IO.Directory.Exists(TextureCachePath))
                System.IO.Directory.CreateDirectory(TextureCachePath);
        }
    }
}

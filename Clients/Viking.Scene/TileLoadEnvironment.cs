using System;
using System.Windows.Threading;
using Microsoft.Xna.Framework.Graphics;
using Viking.VolumeModel;
using Rectangle = Geometry.Rectangle;

namespace Viking
{
    /// <summary>
    /// Process-wide hooks so the shared tile/texture pipeline does not take a WinForms viewer type.
    /// Viking and Jotunn both bind this at startup.
    /// </summary>
    public static class TileLoadEnvironment
    {
        public static Volume? Volume { get; set; }

        public static string CachePath { get; set; } =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Viking\\Cache";

        public static string TextureCachePath { get; set; } = CachePath;

        public static Dispatcher? UiDispatcher { get; set; }

        public static Func<GraphicsDevice?>? GetDevice { get; set; }

        public static Func<Rectangle>? GetVisibleWorldBounds { get; set; }

        public static Func<int>? GetSectionNumber { get; set; }

        public static Func<double>? GetDownsample { get; set; }

        public static int VisibleTileSortIntervalMs { get; set; } = 1000;

        public static int MinTexturesToLoadFromQueue { get; set; } = 3;

        public static int TextureLoadingWindowMs { get; set; } = 30;

        public static TileViewModelCache TileViewModelCache { get; } = new();

        public static LocalTextureCache TextureCache { get; } = new();

        public static void BindVolume(Volume volume)
        {
            Volume = volume;
            if (volume == null)
                return;

            TextureCachePath = System.IO.Path.Combine(CachePath, volume.Name, "Textures");
            if (!System.IO.Directory.Exists(TextureCachePath))
                System.IO.Directory.CreateDirectory(TextureCachePath);
        }
    }
}

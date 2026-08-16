using System.Net.Http;

namespace Viking
{
    /// <summary>
    /// Scene-local stand-in for VikingCore's Global so linked tile/texture sources compile without pulling UI types.
    /// </summary>
    internal static class Global
    {
        public static HttpClient HttpClient => global::Viking.Common.SharedResources.HttpClient;

        public static LocalTextureCache TextureCache => TileLoadEnvironment.TextureCache;

        public static TileViewModelCache TileViewModelCache => TileLoadEnvironment.TileViewModelCache;

        public static void AddTexture(Microsoft.Xna.Framework.Graphics.Texture tex, string msg)
        {
        }

        public static void RemoveTexture(Microsoft.Xna.Framework.Graphics.Texture tex)
        {
        }

        public static void AddTextureReader(object tex, string msg)
        {
        }

        public static void RemoveTextureReader(object tex)
        {
        }
    }
}

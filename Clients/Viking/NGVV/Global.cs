using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Viking.Common;

namespace Viking
{
    /// <summary>
    /// This class was originally created to help track texture allocation and some debugging information.  Currently
    /// public global variables are stored in the UI.State class, but that is not consistent with naming 
    /// conventions used in all extension modules I've written. 
    /// </summary>
    internal static class Global
    {
        /// <summary>
        /// Shared instance of an HttpClient, using this facilitates KeepAlive use for reusing TCP connections
        /// </summary>
        public static HttpClient HttpClient => Viking.Common.SharedResources.HttpClient;


        public static Defaults Default = new();

        public static LocalTextureCache TextureCache = new();

        public static TileViewModelCache TileViewModelCache = new();

        private static readonly Dictionary<int, string> AllocatedTextures = [];

        public static bool TracePenEvents = false;

        public static void AddTexture(Microsoft.Xna.Framework.Graphics.Texture tex, string msg)
        {
            //            Trace.WriteLine("Adding Texture: " + tex.GetHashCode().ToString(), "TextureUse");
            /*
#if DEBUG
            lock(Global.AllocatedTextures)
            {
                Global.AllocatedTextures.Add(tex.GetHashCode(), msg); 
            }
#endif   
             */
        }

        public static void RemoveTexture(Microsoft.Xna.Framework.Graphics.Texture tex)
        {

            //            Trace.WriteLine("Removing Texture: " + tex.GetHashCode().ToString(), "TextureUse");
            /*
#if DEBUG            
            lock(Global.AllocatedTextures)
            {
                Global.AllocatedTextures.Remove(tex.GetHashCode()); 
            }
#endif    
             */
        }

        public static void PrintAllocatedTextures()
        {
            Trace.WriteLine("Allocated textures", "TextureUse");

            List<string> values;
            lock (Global.AllocatedTextures)
            {
                values = [.. Global.AllocatedTextures.Values];
            }

            values.Sort();

            foreach (string str in values)
            {
                Trace.WriteLine("\t" + str, "TextureUse");
            }
        }

        private static readonly Dictionary<int, string> AllocatedTextureReaders = [];

        public static void AddTextureReader(object tex, string msg)
        {
            //            Trace.WriteLine("Adding Texture Reader: " + tex.GetHashCode().ToString(), "TextureUse");

            lock (Global.AllocatedTextureReaders)
            {
                try
                {
                    Global.AllocatedTextureReaders.Add(tex.GetHashCode(), msg);
                }
                catch (ArgumentException) { } //Ignore duplicate key                
            }

            _TexturesLoading = true;

        }

        public static void RemoveTextureReader(object tex)
        {

            //            Trace.WriteLine("Removing Texture Reader: " + tex.GetHashCode().ToString(), "TextureUse");

            lock (Global.AllocatedTextureReaders)
            {
                Global.AllocatedTextureReaders.Remove(tex.GetHashCode());
            }
        }

        /// <summary>
        /// Set to true if textures were loading last time we asked if we needed to refresh
        /// </summary>
        private static bool _TexturesLoading = true;

        public static bool TexturesLoadedNeedRefresh
        {
            get
            {
                lock (Global.AllocatedTextureReaders)
                {
                    if (Global.AllocatedTextureReaders.Keys.Count > 0)
                    {
                        _TexturesLoading = true;
                        return true;
                    }

                    //If there were textures last time we checked, but none now, return true
                    if (_TexturesLoading)
                    {
                        _TexturesLoading = false;
                        return true;
                    }

                    return false;

                }


            }

        }

        public static void PrintAllocatedTextureReaders()
        {
            Trace.WriteLine("Allocated Texture  Readers", "TextureUse");
            List<string> values;
            lock (Global.AllocatedTextureReaders)
            {
                values = [.. Global.AllocatedTextureReaders.Values];
            }

            values.Sort();

            foreach (string str in values)
            {
                Trace.WriteLine("\t" + str, "TextureUse");
            }
        }


        /// <summary>
        /// Keep the textures from this many sections +/- the current section in memory
        /// </summary>
        public const int SectionsCached = 2;
    }
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.Common;

namespace Viking
{
    /// <summary>
    /// This class was originally created to help track texture allocation and some debugging information.  Currently
    /// public global variables are stored in the UI.State class, but that is not consistent with naming 
    /// conventions used in all extension modules I've written. 
    /// </summary>
    internal class Global
    {
        static public Defaults Default = new Defaults();

        static public LocalTextureCache TextureCache = new LocalTextureCache();

        static public TileViewModelCache TileViewModelCache = new TileViewModelCache();

        static private readonly Dictionary<int, string> AllocatedTextures = new Dictionary<int, string>();

        static public bool TracePenEvents = false;

        static public void AddTexture(Microsoft.Xna.Framework.Graphics.Texture tex, string msg)
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

        static public void RemoveTexture(Microsoft.Xna.Framework.Graphics.Texture tex)
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

        static public void PrintAllocatedTextures()
        {
            Trace.WriteLine("Allocated textures", "TextureUse");

            List<string> values;
            lock (Global.AllocatedTextures)
            {
                values = Global.AllocatedTextures.Values.ToList<string>();
            }

            values.Sort();

            foreach (string str in values)
            {
                Trace.WriteLine("\t" + str, "TextureUse");
            }
        }

        static private readonly Dictionary<int, string> AllocatedTextureReaders = new Dictionary<int, string>();

        static public void AddTextureReader(object tex, string msg)
        {
            //            Trace.WriteLine("Adding Texture Reader: " + tex.GetHashCode().ToString(), "TextureUse");

            lock (Global.AllocatedTextureReaders)
            {
                if (Global.AllocatedTextureReaders.ContainsKey(tex.GetHashCode()) == false)
                    Global.AllocatedTextureReaders.Add(tex.GetHashCode(), msg);
            }

            _TexturesLoading = true;

        }

        static public void RemoveTextureReader(object tex)
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
        static private bool _TexturesLoading = true;

        static public bool TexturesLoadedNeedRefresh
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

        static public void PrintAllocatedTextureReaders()
        {
            Trace.WriteLine("Allocated Texture  Readers", "TextureUse");
            List<string> values;
            lock (Global.AllocatedTextureReaders)
            {
                values = Global.AllocatedTextureReaders.Values.ToList<string>();
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

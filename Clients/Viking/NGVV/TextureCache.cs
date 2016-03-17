using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Viking.UI;
using Viking.Common;

namespace Viking
{
    internal class LocalTextureCacheEntry : CacheEntry<string>
    {
        public LocalTextureCacheEntry(string filename)
            : base(filename)
        {
            FileInfo info = new FileInfo(filename);
            this.Size = info.Length;
            this.LastAccessed = info.LastAccessTimeUtc;            
        }

        public override void Dispose()
        {
        }
    }

    /// <summary>
    /// This class manages all requests for textures
    /// </summary>
    class LocalTextureCache : TimeQueueCache<string, LocalTextureCacheEntry, byte[], FileStream>
    {
        public LocalTextureCache()
        {
            //Create the cache directory if it does not exist
            if(System.IO.Directory.Exists(State.CachePath) == false)
                System.IO.Directory.CreateDirectory(State.CachePath);

            //Search the cache directory and create a list of existing files
//            string[] dirs = System.IO.Directory.GetDirectories(State.CachePath);

            //Have a bigger cache on disk for textures
            this.MaxCacheSize = 1;
            this.MaxCacheSize <<= 30;
        }

        public void PopulateCache(string Path)
        {
            System.Threading.Tasks.Task.Run(() => _PopulateCacheThreadStart(Path));
            //Action<string> checkAction = new Action<string>(_PopulateCacheThreadStart);
            //checkAction.BeginInvoke(Path, null, null); 
        }

        /// <summary>
        /// Add all textures found under the specified directory to the cache
        /// </summary>
        /// <param name="path"></param>
        private void _PopulateCacheThreadStart(string path)
        {
            DateTime Start = DateTime.Now;
            Trace.WriteLine("Populating cache", "TextureUse");

            CheckDirectory(path); 

            TimeSpan elapsed = new TimeSpan(DateTime.Now.Ticks - Start.Ticks);
            Trace.WriteLine("Finish cache populate: " + elapsed.ToString(), "TextureUse"); 
        }

        /// <summary>
        /// Recursively check the supplied directory and all subdirectories, adding files to cache lists
        /// </summary>
        /// <param name="path"></param>
        private void CheckDirectory(string path)
        {  
            string[] dirs = System.IO.Directory.GetDirectories(path);
            foreach (string dir in dirs)
            {
                CheckDirectory(dir);
            }

            string[] files = System.IO.Directory.GetFiles(path);


            foreach (string file in files)
            {
                LocalTextureCacheEntry entry = new LocalTextureCacheEntry(file);

                bool Added = AddEntry(entry);
                if (!Added)
                {
                    entry.Dispose();
                    entry = null; 
                }
            }
        }

  //      static public List<int> AllocatedTextures = new List<int>();
        protected override FileStream Fetch(LocalTextureCacheEntry entry)
        {
            FileStream stream = null; 
            
            if (System.IO.File.Exists(entry.Key))
            {
                try
                {
                    stream = new FileStream(entry.Key, FileMode.Open, FileAccess.Read);
                }
                catch (System.IO.IOException )
                {
                    //Couldn't open the file, return null
                    return null; 
                }
            }

            return stream; 
        }
        
          
        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected override LocalTextureCacheEntry CreateEntry(string filename, byte[] textureBuffer)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            FileStream stream = null; 
            try
            {
                stream = new FileStream(filename, FileMode.Create, FileAccess.Write);
                if (stream != null)
                { 
                    stream.Write(textureBuffer, 0, textureBuffer.Length);
                    stream.Dispose(); 
                    stream = null; 

                    LocalTextureCacheEntry entry = new LocalTextureCacheEntry(filename);
                    return entry; 
                }
            }
            catch (System.IO.IOException ioexception)
            {
                Trace.WriteLine(ioexception.Message);
                Trace.WriteLine(ioexception.StackTrace);

                return null;
            }
            
            //     stream.Close();
            //An entry is created if the asynch write succeeds
            return null; 
        }

        /// <summary>
        /// The entry is being removed from the cache, so delete the file from the cache
        /// </summary>
        protected override bool OnRemoveEntry(LocalTextureCacheEntry entry)
        {
            if (System.IO.File.Exists(entry.Key) == false)
                return true;

            try
            {
                System.IO.File.Delete(entry.Key);
            }
            catch (System.UnauthorizedAccessException except)
            {
                Trace.WriteLine("Could not remove file, access exception: " + entry.Key + "\n" + except.Message, "TextureUse");
                return false;
            }
            catch (System.IO.IOException except)
            {
                Trace.WriteLine("Could not remove file: " + entry.Key + "\n" + except.Message, "TextureUse");
                return false;
            }

            return true;
        }
    }
}

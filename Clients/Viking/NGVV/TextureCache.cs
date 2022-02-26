using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using Viking.Common;
using Viking.UI;

namespace Viking
{
    internal class LocalTextureCacheEntry : CacheEntry<string>
    {
        public LocalTextureCacheEntry(string filename)
            : this(new FileInfo(filename))
        { 
        }

        public LocalTextureCacheEntry(FileInfo fileinfo)
            : base(fileinfo.FullName)
        { 
            this.Size = fileinfo.Length;
            this.LastAccessed = fileinfo.LastAccessTimeUtc;
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
            if (System.IO.Directory.Exists(State.CachePath) == false)
                System.IO.Directory.CreateDirectory(State.CachePath);

            //Search the cache directory and create a list of existing files
            //            string[] dirs = System.IO.Directory.GetDirectories(State.CachePath);

            //Have a bigger cache on disk for textures
            this.MaxCacheSize = 1;
            this.MaxCacheSize <<= 30;
        }

        public async Task PopulateCache(string Path, CancellationToken token)
        {
            await _PopulateCacheThreadStart(Path, token);
            //Action<string> checkAction = new Action<string>(_PopulateCacheThreadStart);
            //checkAction.BeginInvoke(Path, null, null); 
        }

        /// <summary>
        /// Add all textures found under the specified directory to the cache
        /// </summary>
        /// <param name="path"></param>
        private async System.Threading.Tasks.Task _PopulateCacheThreadStart(string path, CancellationToken token)
        {
            DateTime Start = DateTime.Now;
            Trace.WriteLine("Populating cache", "TextureUse");
            var dirinfo = new DirectoryInfo(path);
            if (false == dirinfo.Exists)
            {
                dirinfo.Create();
            }
                
            await CheckDirectory(dirinfo, token);

            TimeSpan elapsed = new TimeSpan(DateTime.Now.Ticks - Start.Ticks);
            Trace.WriteLine("Finish cache populate: " + elapsed.ToString(), "TextureUse");
        }

        /// <summary>
        /// Recursively check the supplied directory and all subdirectories, adding files to cache lists
        /// </summary>
        /// <param name="path"></param>
        private async Task CheckDirectory(DirectoryInfo path, CancellationToken token)
        {
            if (path.Exists == false)
                return;

            var subdirs = path.EnumerateDirectories().ToArray();
            System.Collections.Generic.List<Task> listTasks = new System.Collections.Generic.List<Task>(subdirs.Length);
            foreach (var subdir in subdirs)
            {
                if (token.IsCancellationRequested)
                    return;

                listTasks.Add(CheckDirectory(subdir, token));
            }
              
            foreach (var file in path.EnumerateFiles())
            {
                LocalTextureCacheEntry entry = new LocalTextureCacheEntry(file);

                bool Added = AddEntry(entry);
                if (!Added)
                {
                    entry.Dispose();
                    entry = null;
                }

                if (token.IsCancellationRequested)
                    return;
            }

            if(listTasks.Count > 0)
                Task.WaitAll(listTasks.ToArray(), token);

            return;
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
                catch (System.IO.IOException)
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
            using (var output = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                try
                {
                    output.Write(textureBuffer, 0, textureBuffer.Length); 

                    LocalTextureCacheEntry entry = new LocalTextureCacheEntry(filename);
                    return entry; 
                }
                catch (System.IO.IOException ioexception)
                {
                    Trace.WriteLine(ioexception.Message);
                    Trace.WriteLine(ioexception.StackTrace);

                    return null;
                }
            }

            //     stream.Close();
            //An entry is created if the asynch write succeeds
            return null;
        }

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected override LocalTextureCacheEntry CreateEntry(string filename, Func<string,byte[]> textureBufferFactory)
        {
            return CreateEntry(filename, textureBufferFactory(filename)); 
        }

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected override async Task<LocalTextureCacheEntry> CreateEntryAsync(string filename, byte[] textureBuffer)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            using (var output = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                try
                {
                    await output.WriteAsync(textureBuffer, 0, textureBuffer.Length);

                    LocalTextureCacheEntry entry = new LocalTextureCacheEntry(filename);
                    return entry;
                }
                catch (System.IO.IOException ioexception)
                {
                    Trace.WriteLine(ioexception.Message);
                    Trace.WriteLine(ioexception.StackTrace);

                    return null;
                }
            }

            //     stream.Close();
            //An entry is created if the asynch write succeeds
            return null;
        }

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected async Task<LocalTextureCacheEntry> CreateEntryAsync(string filename, Stream textureBuffer)
        {
            if (filename is null)
                throw new ArgumentNullException($"{nameof(LocalTextureCache)} create entry passed null filename");

            try
            {
                return await CreateEntryAssumeDirectoryExistsAsync(filename, textureBuffer);
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                //If the directory does not exist then create it and try again
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
                return await CreateEntryAssumeDirectoryExistsAsync(filename, textureBuffer); 
            }

            return null; 
        }

        private async Task<LocalTextureCacheEntry> CreateEntryAssumeDirectoryExistsAsync(string filename,
            Stream textureBuffer)
        {
            using (var output = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                try
                {
                    await textureBuffer.CopyToAsync(output);

                    return new LocalTextureCacheEntry(filename);
                }
                catch (System.IO.IOException ioexception)
                {
                    Trace.WriteLine(ioexception.Message);
                    Trace.WriteLine(ioexception.StackTrace);

                    return null;
                }
            }
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
         

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        public virtual async Task<bool> AddAsync(string key, Stream value)
        {
            var entry = await CreateEntryAsync(key,value);
            if (entry == null)
                return false;

            return AddEntry(entry);
        }
    }
}

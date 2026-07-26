using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    class LocalTextureCache : TimeQueueCache<string, LocalTextureCacheEntry, byte[], Stream>
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);

        internal static SemaphoreSlim GetFileLock(string filename) =>
            FileLocks.GetOrAdd(filename, _ => new SemaphoreSlim(1, 1));

        /// <summary>
        /// Delete a texture cache file using the same per-path lock as reads and writes.
        /// </summary>
        internal static void DeleteCachedFile(string filename)
        {
            if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
                return;

            var fileLock = GetFileLock(filename);
            fileLock.Wait();
            try
            {
                File.Delete(filename);
            }
            catch (IOException ex)
            {
                Trace.WriteLine($"Failed to delete texture cache file: {filename}\n{ex}", "TextureUse");
            }
            finally
            {
                fileLock.Release();
            }
        }

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

        public async Task PopulateCache(string Path, CancellationToken token) => await _PopulateCacheThreadStart(Path, token);//Action<string> checkAction = new Action<string>(_PopulateCacheThreadStart);//checkAction.BeginInvoke(Path, null, null); 

        /// <summary>
        /// Add all textures found under the specified directory to the cache
        /// </summary>
        /// <param name="path"></param>
        private async System.Threading.Tasks.Task _PopulateCacheThreadStart(string path, CancellationToken token)
        {
            DateTime Start = DateTime.Now;
            Trace.WriteLine("Populating cache", "TextureUse");
            DirectoryInfo dirinfo = new(path);
            if (false == dirinfo.Exists)
            {
                dirinfo.Create();
            }

            await CheckDirectory(dirinfo, token);

            TimeSpan elapsed = new(DateTime.Now.Ticks - Start.Ticks);
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

            System.Collections.Generic.List<Task> listTasks = new(128);
            foreach (var subdir in path.EnumerateDirectories())
            {
                if (token.IsCancellationRequested)
                    return;

                listTasks.Add(CheckDirectory(subdir, token));
            }

            foreach (var file in path.EnumerateFiles())
            {
                LocalTextureCacheEntry entry = new(file);

                if (!AddEntry(entry))
                {
                    entry.Dispose();
                    entry = null;
                }

                if (token.IsCancellationRequested)
                    return;
            }

            await Task.WhenAll(listTasks);

            return;
        }

        //      static public List<int> AllocatedTextures = new List<int>();

        public new Stream Fetch(string key) => base.Fetch(key);

        protected override Stream Fetch(LocalTextureCacheEntry entry)
        {
            if (System.IO.File.Exists(entry.Key) == false)
                return null;

            var fileLock = GetFileLock(entry.Key);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                fileLock.Wait();
                try
                {
                    byte[] bytes = File.ReadAllBytes(entry.Key);
                    return new MemoryStream(bytes, writable: false);
                }
                catch (IOException) when (attempt < 3)
                {
                    Thread.Sleep(25 * (attempt + 1));
                }
                catch (IOException)
                {
                    return null;
                }
                finally
                {
                    fileLock.Release();
                }
            }

            return null;
        }


        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected override LocalTextureCacheEntry CreateEntry(string filename, byte[] textureBuffer)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            var fileLock = GetFileLock(filename);
            fileLock.Wait();
            try
            {
                return WriteStreamToCacheFile(filename, stream =>
                {
                    stream.Write(textureBuffer, 0, textureBuffer.Length);
                });
            }
            finally
            {
                fileLock.Release();
            }
        }

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected override LocalTextureCacheEntry CreateEntry(string filename, Func<string, byte[]> textureBufferFactory) => CreateEntry(filename, textureBufferFactory(filename));

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        protected override async Task<LocalTextureCacheEntry> CreateEntryAsync(string filename, byte[] textureBuffer)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            var fileLock = GetFileLock(filename);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await WriteStreamToCacheFileAsync(filename, async stream =>
                {
                    await stream.WriteAsync(textureBuffer, 0, textureBuffer.Length).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
            }
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
            var fileLock = GetFileLock(filename);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return await WriteStreamToCacheFileAsync(filename, stream => textureBuffer.CopyToAsync(stream))
                    .ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
            }
        }

        /// <summary>
        /// Write cache data via a temp file and atomically replace the target so concurrent readers
        /// are not disrupted and writers do not fight over an open path.
        /// </summary>
        private static LocalTextureCacheEntry WriteStreamToCacheFile(string filename, Action<Stream> writeBody)
        {
            string tempPath = filename + ".part";
            try
            {
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writeBody(output);
                }

                if (ReplaceCacheFile(tempPath, filename))
                {
                    return new LocalTextureCacheEntry(filename);
                }

                return null;
            }
            catch (IOException ioexception)
            {
                Trace.WriteLine(ioexception.Message);
                Trace.WriteLine(ioexception.StackTrace);
                TryDeleteQuiet(tempPath);
                return null;
            }
        }

        private static async Task<LocalTextureCacheEntry> WriteStreamToCacheFileAsync(string filename, Func<Stream, Task> writeBody)
        {
            string tempPath = filename + ".part";
            try
            {
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await writeBody(output).ConfigureAwait(false);
                }

                if (ReplaceCacheFile(tempPath, filename))
                {
                    return new LocalTextureCacheEntry(filename);
                }

                return null;
            }
            catch (IOException ioexception)
            {
                Trace.WriteLine(ioexception.Message);
                Trace.WriteLine(ioexception.StackTrace);
                TryDeleteQuiet(tempPath);
                return null;
            }
        }

        /// <summary>
        /// Atomically publish a temp cache file. Returns false only when replace fails and no usable file exists.
        /// </summary>
        private static bool ReplaceCacheFile(string tempPath, string filename)
        {
            try
            {
                if (File.Exists(filename))
                    File.Replace(tempPath, filename, null);
                else
                    File.Move(tempPath, filename);

                return true;
            }
            catch (IOException ex)
            {
                // Target may still be open for read by another thread; keep the existing file if present.
                TryDeleteQuiet(tempPath);
                bool targetExists = File.Exists(filename);
                return targetExists;
            }
        }

        private static void TryDeleteQuiet(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
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

            var entry = await CreateEntryAsync(key, value);
            if (entry is null)
            {
                return false;
            }

            bool added = AddEntry(entry);
            return added;
        }
    }
}

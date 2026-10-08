using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Per-file locking and atomic writes for transform JSON cache files.
    /// </summary>
    internal static class TransformCacheFileIO
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static SemaphoreSlim GetLock(string path) =>
            FileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));

        public static void Save(string path, Action<Stream> writeBody)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));


            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var fileLock = GetLock(path);
            fileLock.Wait();
            try
            {
                string tempPath = path + ".part";
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writeBody(output);
                }

                ReplaceFile(tempPath, path);
            }
            finally
            {
                fileLock.Release();
            }
        }

        public static T TryLoad<T>(string path, Func<Stream, T> readBody, out Exception lastError)
        {
            lastError = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return default;
            }

            if (!IsLikelyJsonTransformCache(path))
            {
                lastError = new InvalidDataException("Transform cache is not JSON (likely legacy BinaryFormatter data)");
                TryDelete(path);
                return default;
            }


            var fileLock = GetLock(path);
            const int maxAttempts = 4;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                fileLock.Wait();
                try
                {
                    string partPath = path + ".part";
                    if (File.Exists(partPath) && attempt < maxAttempts - 1)
                    {
                        Thread.Sleep(25 * (attempt + 1));
                        continue;
                    }

                    using var fstream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    T result = readBody(fstream);
                    return result;
                }
                catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts - 1)
                {
                    lastError = ex;
                    Thread.Sleep(25 * (attempt + 1));
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    return default;
                }
                finally
                {
                    fileLock.Release();
                }
            }

            return default;
        }

        public static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var fileLock = GetLock(path);
            fileLock.Wait();
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            finally
            {
                fileLock.Release();
            }
        }

        private static bool IsTransient(Exception ex) =>
            ex is IOException;

        private static void ReplaceFile(string tempPath, string targetPath)
        {
            try
            {
                if (File.Exists(targetPath))
                    File.Replace(tempPath, targetPath, null);
                else
                    File.Move(tempPath, targetPath);
            }
            catch (IOException)
            {
                TryDeleteQuiet(tempPath);
                throw;
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
        /// JSON transform caches start with '[' or '{'. Legacy BinaryFormatter files do not.
        /// </summary>
        private static bool IsLikelyJsonTransformCache(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int b;
                while ((b = fs.ReadByte()) >= 0)
                {
                    if (char.IsWhiteSpace((char)b))
                        continue;

                    return b == (byte)'[' || b == (byte)'{';
                }
            }
            catch (IOException)
            {
            }

            return false;
        }
    }
}

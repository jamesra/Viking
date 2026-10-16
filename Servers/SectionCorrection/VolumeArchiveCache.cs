using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// Builds the aggregate <c>volume.npz</c> of a published group on first request and keeps it beside the group's
    /// files, named for the manifest's build time. A publish replaces the whole group directory (or, for an
    /// incremental publish, copies only section files), so a cached archive from an older build disappears with it.
    /// </summary>
    public static class VolumeArchiveCache
    {
        static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>File name of the cached archive for a build time.</summary>
        public static string CacheFileName(DateTime builtUtc) => "volume." + builtUtc.Ticks + ".npz";

        /// <summary>
        /// Returns the path of the archive for the group's current build, generating it when it is not cached yet.
        /// Concurrent callers for the same group generate it once. Blocks while generating.
        /// </summary>
        /// <param name="groupDirectory">Published group directory containing <c>manifest.json</c>.</param>
        /// <returns>Path of the cached archive, or null when the group has no manifest.</returns>
        public static string EnsureArchive(string groupDirectory)
        {
            if (string.IsNullOrEmpty(groupDirectory) ||
                !File.Exists(Path.Combine(groupDirectory, PublishedCorrectionSet.ManifestFileName)))
                return null;

            object gate = Locks.GetOrAdd(Path.GetFullPath(groupDirectory), _ => new object());
            lock (gate)
            {
                CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(groupDirectory);
                string cached = Path.Combine(groupDirectory, CacheFileName(manifest.Provenance?.BuiltUtc ?? DateTime.MinValue));
                if (File.Exists(cached))
                    return cached;

                PublishedCorrectionSet set = PublishedCorrectionSet.LoadDirectory(groupDirectory);
                string temp = cached + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    PublishedCorrectionSet.WriteVolumeNpz(temp, set.Sections.Values);
                    File.Move(temp, cached);
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }

                DeleteStale(groupDirectory, cached);
                return cached;
            }
        }

        static void DeleteStale(string groupDirectory, string keep)
        {
            foreach (string file in Directory.EnumerateFiles(groupDirectory, "volume*.npz"))
            {
                if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}

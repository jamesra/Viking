using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// In-memory catalog of published correction directories: {root}/{VolumeName}/{StosGroup}/manifest.json.
    /// Skips sibling *.staging and *.old folders left by atomic publish, and keeps the previous
    /// in-memory set while that swap is in progress so gRPC can keep serving.
    /// </summary>
    public sealed class VolumeCorrectionCatalog : IDisposable
    {
        readonly object _gate = new();
        Dictionary<(string Volume, string StosGroup), PublishedCorrectionSet> _sets =
            new(VolumeStosComparer.Instance);
        FileSystemWatcher _watcher;
        Timer _reloadDebounce;
        readonly string _root;

        public VolumeCorrectionCatalog(string rootDirectory)
        {
            _root = rootDirectory;
        }

        public string RootDirectory => _root;

        public IReadOnlyCollection<(string VolumeName, PublishedCorrectionSet Set)> Sets
        {
            get
            {
                lock (_gate)
                    return [.. _sets.Select(kv => (kv.Key.Volume, kv.Value))];
            }
        }

        public IReadOnlyCollection<string> VolumeNames
        {
            get
            {
                lock (_gate)
                    return [.. _sets.Keys.Select(k => k.Volume).Distinct(StringComparer.OrdinalIgnoreCase)];
            }
        }

        public bool TryGet(string volumeName, string stosGroup, out PublishedCorrectionSet set)
        {
            lock (_gate)
                return _sets.TryGetValue((volumeName ?? "", stosGroup ?? ""), out set);
        }

        /// <summary>
        /// Safe folder name for Identity volume Name. Empty after sanitizing returns "_".
        /// </summary>
        public static string SanitizeVolumeName(string volumeName)
        {
            if (string.IsNullOrWhiteSpace(volumeName))
                return "_";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = volumeName.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == Path.DirectorySeparatorChar ||
                    chars[i] == Path.AltDirectorySeparatorChar)
                    chars[i] = '_';
            }

            string name = new string(chars).Trim('.', ' ');
            return string.IsNullOrWhiteSpace(name) ? "_" : name;
        }

        public void Load()
        {
            Dictionary<(string Volume, string StosGroup), PublishedCorrectionSet> next =
                new(VolumeStosComparer.Instance);
            HashSet<(string Volume, string StosGroup)> unreadable = new(VolumeStosComparer.Instance);
            if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
            {
                foreach (string volumeDir in Directory.GetDirectories(_root))
                {
                    if (IsTransientPublishDir(volumeDir))
                        continue;

                    string volumeName = Path.GetFileName(volumeDir);
                    if (string.IsNullOrWhiteSpace(volumeName))
                        continue;

                    foreach (string groupDir in Directory.GetDirectories(volumeDir))
                    {
                        string folder = Path.GetFileName(groupDir);
                        if (folder.EndsWith(".staging", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (folder.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
                        {
                            string liveName = folder.Substring(0, folder.Length - ".old".Length);
                            if (Directory.Exists(Path.Combine(volumeDir, liveName)))
                                continue;
                            TryRead(volumeName, groupDir, next);
                            continue;
                        }

                        if (!TryRead(volumeName, groupDir, next))
                            unreadable.Add((volumeName, folder));
                    }
                }
            }

            lock (_gate)
            {
                foreach (KeyValuePair<(string Volume, string StosGroup), PublishedCorrectionSet> previous in _sets)
                {
                    if (next.ContainsKey(previous.Key))
                        continue;
                    if (unreadable.Contains(previous.Key) || PublishSwapInProgress(previous.Key.Volume, previous.Key.StosGroup))
                        next[previous.Key] = previous.Value;
                }

                _sets = next;
            }
        }

        /// <summary>
        /// Reads one stos-group directory into <paramref name="next"/>.
        /// Returns false when a manifest exists but cannot be read, so the caller can keep the previous set.
        /// </summary>
        static bool TryRead(
            string volumeName,
            string groupDir,
            Dictionary<(string Volume, string StosGroup), PublishedCorrectionSet> next)
        {
            string manifest = Path.Combine(groupDir, PublishedCorrectionSet.ManifestFileName);
            if (!File.Exists(manifest))
                return true;

            try
            {
                PublishedCorrectionSet set = PublishedCorrectionSet.LoadDirectory(groupDir);
                if (!string.IsNullOrWhiteSpace(set.StosGroup))
                    next[(volumeName, set.StosGroup)] = set;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True while CorrectionPublisher has moved the live folder to *.old or is still writing *.staging.
        /// The in-memory set stays until the new directory loads.
        /// </summary>
        bool PublishSwapInProgress(string volume, string stosGroup)
        {
            if (string.IsNullOrWhiteSpace(_root) || string.IsNullOrWhiteSpace(volume) || string.IsNullOrWhiteSpace(stosGroup))
                return false;

            string live = Path.Combine(_root, volume, stosGroup);
            return Directory.Exists(live + ".staging") || Directory.Exists(live + ".old");
        }

        public void Watch()
        {
            if (string.IsNullOrWhiteSpace(_root))
                return;
            Directory.CreateDirectory(_root);
            _watcher = new FileSystemWatcher(_root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }

        static bool IsTransientPublishDir(string path)
        {
            string name = Path.GetFileName(path);
            return name.EndsWith(".staging", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".old", StringComparison.OrdinalIgnoreCase);
        }

        void OnChanged(object sender, FileSystemEventArgs e)
        {
            _reloadDebounce?.Dispose();
            _reloadDebounce = new Timer(_ =>
            {
                try
                {
                    Load();
                }
                catch
                {
                    // Keep the previous catalog if a mid-write reload fails.
                }
            }, null, 750, Timeout.Infinite);
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _reloadDebounce?.Dispose();
        }

        sealed class VolumeStosComparer : IEqualityComparer<(string Volume, string StosGroup)>
        {
            public static readonly VolumeStosComparer Instance = new();

            public bool Equals((string Volume, string StosGroup) x, (string Volume, string StosGroup) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Volume, y.Volume) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.StosGroup, y.StosGroup);

            public int GetHashCode((string Volume, string StosGroup) obj)
            {
                int h1 = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Volume ?? "");
                int h2 = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.StosGroup ?? "");
                unchecked
                {
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}

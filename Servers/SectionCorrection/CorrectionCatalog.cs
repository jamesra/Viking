using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// In-memory catalog of published correction directories: {root}/{StosGroup}/manifest.json.
    /// Skips sibling *.staging and *.old folders left by the CorrectionPublisher swap, and keeps the
    /// previous in-memory set while that swap is in progress or its manifest cannot be read, so
    /// GrpcAnnotationService keeps serving the last good set. Same rules as <see cref="VolumeCorrectionCatalog"/>.
    /// </summary>
    public sealed class CorrectionCatalog : IDisposable
    {
        readonly object _gate = new();
        Dictionary<string, PublishedCorrectionSet> _sets = new(StringComparer.OrdinalIgnoreCase);
        FileSystemWatcher _watcher;
        Timer _reloadDebounce;
        readonly string _root;

        public CorrectionCatalog(string rootDirectory)
        {
            _root = rootDirectory;
        }

        public string RootDirectory => _root;

        /// <summary>
        /// Combines <paramref name="relativeParts"/> beneath <paramref name="root"/> and returns the full path,
        /// or null when the result does not lie strictly inside the root folder.
        /// </summary>
        /// <param name="root">Catalog root folder; null or blank means nothing can be resolved.</param>
        /// <param name="relativeParts">Caller-supplied path segments, such as a route's group and file name.</param>
        /// <returns>
        /// The full path, or null when <paramref name="root"/> is blank, a part is null or not a valid path,
        /// or the combined path escapes the root through "..", a rooted part, or a sibling folder whose name
        /// starts with the root's name (root "C:\corr" never admits "C:\corr-private").
        /// </returns>
        /// <remarks>
        /// Pure string work: the file system is not touched, so callers still check that the file exists.
        /// Comparison ignores case on Windows and is ordinal elsewhere, matching the host file system.
        /// </remarks>
        public static string ResolveInsideRoot(string root, params string[] relativeParts)
        {
            if (string.IsNullOrWhiteSpace(root) || relativeParts == null)
                return null;

            try
            {
                string fullRoot = Path.GetFullPath(root);
                string fullPath = Path.GetFullPath(Path.Combine([fullRoot, .. relativeParts]));
                return IsStrictlyInside(fullRoot, fullPath) ? fullPath : null;
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>
        /// True when <paramref name="fullPath"/> lies beneath <paramref name="fullRoot"/>; both are already full paths.
        /// The root is compared with a trailing separator so a sibling sharing its name prefix does not match.
        /// </summary>
        static bool IsStrictlyInside(string fullRoot, string fullPath)
        {
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                                fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;
            return fullPath.Length > rootPrefix.Length && fullPath.StartsWith(rootPrefix, comparison);
        }

        public IReadOnlyCollection<PublishedCorrectionSet> Sets
        {
            get
            {
                lock (_gate)
                    return [.. _sets.Values];
            }
        }

        public bool TryGet(string stosGroup, out PublishedCorrectionSet set)
        {
            lock (_gate)
                return _sets.TryGetValue(stosGroup ?? "", out set);
        }

        /// <summary>
        /// Rebuilds the catalog from disk. Called once at startup by GrpcAnnotationService, DataExport and
        /// MonogameTestbed, and by the <see cref="Watch"/> debounce timer on a thread-pool thread.
        /// A {StosGroup}.staging folder is never read (the publisher writes its manifest before its section files).
        /// A {StosGroup}.old folder is read only when the live folder is missing (publish stopped mid-swap).
        /// A previously loaded set is kept when its manifest is unreadable or a swap is in progress.
        /// </summary>
        public void Load()
        {
            Dictionary<string, PublishedCorrectionSet> next = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> unreadable = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
            {
                foreach (string dir in Directory.GetDirectories(_root))
                {
                    string folder = Path.GetFileName(dir);
                    if (folder.EndsWith(StagingSuffix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (folder.EndsWith(OldSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Directory.Exists(dir.Substring(0, dir.Length - OldSuffix.Length)))
                            TryRead(dir, next);
                        continue;
                    }

                    if (!TryRead(dir, next))
                        unreadable.Add(folder);
                }
            }

            lock (_gate)
            {
                foreach (KeyValuePair<string, PublishedCorrectionSet> previous in _sets)
                {
                    if (!next.ContainsKey(previous.Key) &&
                        (unreadable.Contains(previous.Key) || PublishSwapInProgress(previous.Key)))
                        next[previous.Key] = previous.Value;
                }

                _sets = next;
            }
        }

        const string StagingSuffix = ".staging";
        const string OldSuffix = ".old";

        /// <summary>
        /// Reads one stos-group directory into <paramref name="next"/>, keyed by the manifest's StosGroup.
        /// Returns false only when a manifest exists but cannot be read, so <see cref="Load"/> can keep the previous set.
        /// </summary>
        static bool TryRead(string dir, Dictionary<string, PublishedCorrectionSet> next)
        {
            if (!File.Exists(Path.Combine(dir, PublishedCorrectionSet.ManifestFileName)))
                return true;
            try
            {
                PublishedCorrectionSet set = PublishedCorrectionSet.LoadDirectory(dir);
                if (!string.IsNullOrWhiteSpace(set.StosGroup))
                    next[set.StosGroup] = set;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True while CorrectionPublisher is writing {StosGroup}.staging or has parked the live folder at {StosGroup}.old.
        /// </summary>
        bool PublishSwapInProgress(string stosGroup)
        {
            if (string.IsNullOrWhiteSpace(_root) || string.IsNullOrWhiteSpace(stosGroup))
                return false;
            string live = Path.Combine(_root, stosGroup);
            return Directory.Exists(live + StagingSuffix) || Directory.Exists(live + OldSuffix);
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
    }
}

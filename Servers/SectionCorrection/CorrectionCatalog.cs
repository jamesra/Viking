using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// In-memory catalog of published correction directories: {root}/{StosGroup}/manifest.json.
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

        public void Load()
        {
            Dictionary<string, PublishedCorrectionSet> next = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root))
            {
                foreach (string dir in Directory.GetDirectories(_root))
                {
                    string manifest = Path.Combine(dir, PublishedCorrectionSet.ManifestFileName);
                    if (!File.Exists(manifest))
                        continue;
                    try
                    {
                        PublishedCorrectionSet set = PublishedCorrectionSet.LoadDirectory(dir);
                        if (!string.IsNullOrWhiteSpace(set.StosGroup))
                            next[set.StosGroup] = set;
                    }
                    catch (Exception)
                    {
                        // Skip a corrupt publish; the last good set stays until a successful reload.
                    }
                }
            }

            lock (_gate)
                _sets = next;
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

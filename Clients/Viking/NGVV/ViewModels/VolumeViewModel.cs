using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Viking.UI.WPF.Models;
using Viking.VolumeModel;

namespace Viking.ViewModels
{

    public class VolumeViewModel : IVolumeTransformProvider
    {
        private readonly Volume _Volume;
        private readonly MappingManager _MappingManager;

        public readonly SortedList<int, SectionViewModel> SectionViewModels;

        public string Name => _Volume.Name;

        public bool IsLocal => _Volume.IsLocal;

        public int? DefaultTileWidth => _Volume.DefaultTileWidth;

        public UnitsAndScale.IAxisUnits DefaultXYScale => _Volume.DefaultXYScale;

        public int DefaultSectionNumber
        {
            get
            {
                if (_Volume.DefaultSectionNumber.HasValue)
                {
                    if (SectionViewModels.ContainsKey(_Volume.DefaultSectionNumber.Value))
                    {
                        return _Volume.DefaultSectionNumber.Value;
                    }
                }

                return SectionViewModels.Keys[0];
            }
        }

        public string DefaultVolumeTransform => _Volume.DefaultVolumeTransform;

        public ChannelInfo[] DefaultChannels
        {
            get => _Volume.DefaultChannels;
            set => _Volume.DefaultChannels = value;
        }

        public string[] ChannelNames => Volume.ChannelNames;

        public string[] TransformNames => [.. _Volume.Transforms.Keys];

        public XElement VolumeElement => _Volume.VolumeElement;

        public bool UpdateServerVolumePositions => _Volume.UpdateServerVolumePositions;

        public VolumeViewModel(Volume volume)
        {
            this._Volume = volume;

            _MappingManager = new MappingManager(volume);

            SectionViewModels = new SortedList<int, SectionViewModel>(_Volume.Sections.Count);

            foreach (Section s in _Volume.Sections.Values)
            {
                SectionViewModel sectionViewModel = new(this, s);
                SectionViewModels.Add(s.Number, sectionViewModel);
            }

            _ActiveVolumeTransform = this.DefaultVolumeTransform;

            // Apply persisted section reference settings
            ApplyPersistedSectionReferences();
        }

        private void ApplyPersistedSectionReferences()
        {
            try
            {
                string volumeLocalDir = _Volume?.LocalVolumeDir;
                if (string.IsNullOrWhiteSpace(volumeLocalDir))
                {
                    return;
                }

                var allSettings = SectionReferenceSettings.LoadForVolume(volumeLocalDir);
                if (allSettings is null || allSettings.Count == 0)
                {
                    return;
                }

                foreach (var kvp in allSettings)
                {
                    int sectionNumber = kvp.Key;
                    var references = kvp.Value;

                    if (!SectionViewModels.TryGetValue(sectionNumber, out var sectionViewModel))
                    {
                        continue;
                    }

                    // Apply reference above
                    if (references.ReferenceAbove.HasValue)
                    {
                        if (_Volume.Sections.TryGetValue(references.ReferenceAbove.Value, out var refAbove))
                        {
                            sectionViewModel.ReferenceSectionAbove = refAbove;
                        }
                    }

                    // Apply reference below
                    if (references.ReferenceBelow.HasValue)
                    {
                        if (_Volume.Sections.TryGetValue(references.ReferenceBelow.Value, out var refBelow))
                        {
                            sectionViewModel.ReferenceSectionBelow = refBelow;
                        }
                    }

                    // Apply channel settings
                    if (references.Channels != null && references.Channels.Length > 0)
                    {
                        var channels = SectionReferenceSettings.FromDto(references.Channels);
                        if (channels != null && channels.Length > 0)
                        {
                            if (_Volume.Sections.TryGetValue(sectionNumber, out var section))
                            {
                                section.ChannelInfoArray = channels;
                                System.Diagnostics.Trace.WriteLine($"VolumeViewModel: Loaded {channels.Length} channels for section {sectionNumber}", "VolumeViewModel");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to apply persisted section references: {ex.Message}");
            }
        }

        public string Host => _Volume.Host;

        public MappingBase GetTileMapping(string VolumeTransformName, int SectionNumber, string ChannelName, string SectionTransformName) => _MappingManager.GetMapping(VolumeTransformName, SectionNumber, ChannelName, SectionTransformName);

        public MappingBase GetTileMapping(int SectionNumber, string ChannelName, string SectionTransformName) => _MappingManager.GetMapping(this.ActiveVolumeTransform, SectionNumber, ChannelName, SectionTransformName);

        protected static string BuildTransformKey(string VolumeTransformName, int SectionNumber)
        {
            string key = VolumeTransformName + '-' + SectionNumber.ToString("D4");
            return key;
        }

        /// <summary>
        /// A cached <see cref="VolumeToSectionTransform"/> and the section transform it wraps.
        /// </summary>
        private sealed class SectionTransformEntry(ITransform source, VolumeToSectionTransform wrapper)
        {
            public readonly ITransform Source = source;
            public readonly VolumeToSectionTransform Wrapper = wrapper;
        }

        /// <summary>
        /// The cached wrappers for one active volume transform, by section number.
        /// </summary>
        private sealed class ActiveTransformCache(string volumeTransformName)
        {
            public readonly string VolumeTransformName = volumeTransformName;
            public readonly ConcurrentDictionary<int, SectionTransformEntry> Sections = new();
        }

        private volatile ActiveTransformCache? _SectionTransformCache;

        /// <summary>
        /// Wrappers for sections that have no transform in the active volume transform, by section number.
        /// </summary>
        private readonly ConcurrentDictionary<int, VolumeToSectionTransform> _IdentitySectionTransforms = new();

        private VolumeToSectionTransform GetIdentitySectionTransform(int SectionNumber) =>
            _IdentitySectionTransforms.GetOrAdd(SectionNumber,
                number => new VolumeToSectionTransform(BuildTransformKey("Identity", number),
                                                       new Geometry.Transforms.IdentityTransform()));

        /// <summary>
        /// Returns the transform between a section and the volume for the active volume transform.
        /// </summary>
        /// <remarks>
        /// Safe to call from render and worker threads. One <see cref="VolumeToSectionTransform"/> is kept per section and
        /// shared between callers; it is immutable. The cache is dropped when <see cref="ActiveVolumeTransform"/> changes, and an
        /// entry is replaced if the volume now holds a different transform instance for the section.
        /// </remarks>
        public IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber)
        {
            string activeTransform = this.ActiveVolumeTransform;
            if (activeTransform is null)
                return GetIdentitySectionTransform(SectionNumber);

            SortedList<int, ITransform> SectionTransforms = _Volume.Transforms[activeTransform];

            if (!SectionTransforms.TryGetValue(SectionNumber, out var transform))
                return GetIdentitySectionTransform(SectionNumber);

            ActiveTransformCache cache = _SectionTransformCache;
            if (cache is null || !string.Equals(cache.VolumeTransformName, activeTransform, StringComparison.Ordinal))
            {
                cache = new ActiveTransformCache(activeTransform);
                _SectionTransformCache = cache;
            }

            if (cache.Sections.TryGetValue(SectionNumber, out SectionTransformEntry entry) && ReferenceEquals(entry.Source, transform))
                return entry.Wrapper;

            VolumeToSectionTransform wrapper = new(BuildTransformKey(activeTransform, SectionNumber), transform);
            cache.Sections[SectionNumber] = new SectionTransformEntry(transform, wrapper);
            return wrapper;
        }

        public void ReduceCacheFootprint(object state) => _MappingManager.ReduceCacheFootprint();

        /// <summary>
        /// Mapping cache for a background job. Separate from the viewer's cache so a long section walk does not evict the transforms on screen.
        /// The caller drops the manager when the job ends.
        /// </summary>
        public MappingManager CreateMappingManager() => new(_Volume);

        #region Events


        /// <summary>
        /// Fires when the transform used to place the section into the volume changes
        /// </summary>
        public event Viking.Common.TransformChangedEventHandler TransformChanged;

        #endregion

        protected string _ActiveVolumeTransform;
        public string ActiveVolumeTransform
        {
            get => _ActiveVolumeTransform;
            set
            {
                bool NewValue = value != _ActiveVolumeTransform;

                if (NewValue)
                {
                    string OldTransform = _ActiveVolumeTransform;
                    _ActiveVolumeTransform = value;
                    _SectionTransformCache = null;

                    TransformChanged?.Invoke(this, new Viking.Common.TransformChangedEventArgs(_ActiveVolumeTransform, OldTransform));
                }
            }
        }

        public bool UsingVolumeTransform => ActiveVolumeTransform != null;
    }
}

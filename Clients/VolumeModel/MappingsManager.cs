using System;
using Geometry;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;

namespace Viking.VolumeModel
{
    public class SectionTransformsDictionary : ConcurrentDictionary<string, MappingBase>
    {

    }

    /// <summary>
    /// The mappings for each section the viewer has used, kept so returning to a section does not load and warp it again.
    /// Entry sizes are estimated bytes (<see cref="MappingBase.EstimatedMemoryBytes"/>), and the cache is held under
    /// <see cref="MemoryBudgetBytes"/> by <see cref="EnforceMemoryBudget"/>. Evicting an entry calls FreeMemory on every
    /// mapping for that section.
    /// </summary>
    /// <remarks>
    /// <para>Every GetMapping marks its section used, and a section used since the last checkpoint is never evicted. Each
    /// <see cref="EnforceMemoryBudget"/> pass evicts first and checkpoints after, so a pass can evict only sections nobody
    /// asked for since the previous pass; the section on screen is asked for every frame and stays.</para>
    /// <para>A checkpoint here never evicts on its own (<see cref="OnCheckpointFailed"/> does nothing); eviction is by
    /// size only. An RC2 section takes about 9 MB once drawn, so the default 1 GiB holds over a hundred sections.</para>
    /// </remarks>
    public class SectionTransformsCache : TimeQueueCache<int, SectionMappingsCacheEntry, SectionTransformsDictionary, SectionTransformsDictionary>
    {
        /// <summary>Default memory budget for cached sections: 1 GiB.</summary>
        public const long DefaultMemoryBudgetBytes = 1L << 30;

        /// <summary>Estimated bytes of section mappings to keep before least recently used sections are evicted.</summary>
        public long MemoryBudgetBytes
        {
            get => this.MaxCacheSize;
            set => this.MaxCacheSize = Math.Max(1, value);
        }

        /// <summary>1 while an <see cref="EnforceMemoryBudget"/> pass runs, so overlapping timer ticks skip.</summary>
        private int _enforcing;

        public SectionTransformsCache()
        {
            this.MemoryBudgetBytes = DefaultMemoryBudgetBytes;
        }

        /// <summary>
        /// Recomputes every section's estimated size from its mappings (they grow after the entry is added, as sections
        /// load, warp and are drawn) and returns the new total.
        /// </summary>
        public long RefreshEntrySizes()
        {
            foreach (SectionMappingsCacheEntry entry in dictEntries.Values)
                Interlocked.Exchange(ref entry.Size, Math.Max(1, entry.EstimatedMemoryBytes()));

            return RecountCacheSize();
        }

        /// <summary>
        /// One budget pass: refreshes entry sizes; if the cache is over <see cref="MemoryBudgetBytes"/>, evicts sections not
        /// used since the previous pass, least recently used first, until it is under budget; then checkpoints so the next
        /// pass sees only sections used from now on. Returns the number of sections evicted.
        /// </summary>
        /// <remarks>
        /// Runs synchronously; call it off the UI thread. Meant to be called periodically (Viking's cache-cleaning timer
        /// calls it every 60 s through <see cref="MappingManager.ReduceCacheFootprint"/>). If every section was used since
        /// the previous pass the cache stays over budget until a later pass. Returns 0 at once if a pass is running.
        /// </remarks>
        public int EnforceMemoryBudget()
        {
            if (Interlocked.CompareExchange(ref _enforcing, 1, 0) != 0)
                return 0;

            try
            {
                int evicted = 0;
                if (RefreshEntrySizes() > MemoryBudgetBytes)
                {
                    List<SectionMappingsCacheEntry> oldestFirst = [.. dictEntries.Values];
                    oldestFirst.Sort();

                    foreach (SectionMappingsCacheEntry entry in oldestFirst)
                    {
                        if (CachedSize <= MemoryBudgetBytes)
                            break;

                        if (entry.WasUsedSinceLastCheckpoint || entry.CheckpointExempt)
                            continue;

                        RemoveEntry(entry);
                        evicted++;
                    }

                    if (evicted > 0)
                        System.Diagnostics.Trace.WriteLine($"Section cache evicted {evicted} sections; {CachedSize / (1 << 20)} MB of {MemoryBudgetBytes / (1 << 20)} MB remain", "Cache");
                }

                Checkpoint();
                return evicted;
            }
            finally
            {
                Interlocked.Exchange(ref _enforcing, 0);
            }
        }

        /// <summary>
        /// A checkpoint only clears the used marks. Eviction is by size, in <see cref="EnforceMemoryBudget"/>, so an unused
        /// section stays cached while the cache is under budget.
        /// </summary>
        protected override void OnCheckpointFailed(SectionMappingsCacheEntry entry)
        {
        }

        protected override SectionTransformsDictionary Fetch(SectionMappingsCacheEntry entry) => entry.TransformsForSection;

        protected override SectionMappingsCacheEntry CreateEntry(int key, SectionTransformsDictionary entry)
        {
            SectionMappingsCacheEntry cacheEntry = new(key, entry);
            return cacheEntry;
        }

        protected override SectionMappingsCacheEntry CreateEntry(int key, Func<int, SectionTransformsDictionary> entryFactory)
        {
            SectionMappingsCacheEntry cacheEntry = new(key, entryFactory(key));
            return cacheEntry;
        }

        protected override Task<SectionMappingsCacheEntry> CreateEntryAsync(int key, SectionTransformsDictionary entry)
        {
            SectionMappingsCacheEntry cacheEntry = new(key, entry);
            return Task.FromResult(cacheEntry);
        }
    }

    public class SectionMappingsCacheEntry : CacheEntry<int>
    {
        public SectionTransformsDictionary TransformsForSection = new();

        public SectionMappingsCacheEntry(int SectionNumber, SectionTransformsDictionary entry) :
            base(SectionNumber)
        {
            this.Size = 1;
            this.TransformsForSection = entry;
        }

        /// <summary>
        /// Estimated memory of this section's mappings, counting a volume transform shared by several of them once. Zero once
        /// disposed.
        /// </summary>
        public long EstimatedMemoryBytes()
        {
            SectionTransformsDictionary mappings = TransformsForSection;
            if (mappings is null)
                return 0;

            long bytes = 0;
            List<ITransform> volumeTransforms = new(2);
            foreach (MappingBase mapping in mappings.Values)
            {
                bytes += mapping.EstimatedMemoryBytes;
                ITransform shared = mapping.SharedVolumeTransform;
                if (shared != null && !volumeTransforms.Exists(t => ReferenceEquals(t, shared)))
                {
                    volumeTransforms.Add(shared);
                    bytes += MappingBase.EstimateTransformBytes(shared);
                }
            }
            return bytes;
        }

        public sealed override void Dispose()
        {
            if (TransformsForSection != null)
            {
                foreach (MappingBase mapping in this.TransformsForSection.Values)
                {
                    mapping.FreeMemory();
                }

                TransformsForSection.Clear();
                this.TransformsForSection = null;
            }
        }
    }


    /// <summary>
    /// Creates and caches MappingBase instances. Tileset vs pyramid keys differ — see GetMapping.
    /// </summary>
    public class MappingManager(Volume Volume)
    {
        private readonly VolumeModel.Volume volume = Volume;

        public SectionTransformsCache SectionMappingCache = new();

        /// <summary>
        /// Starts a section cache budget pass on the thread pool: evicts least recently used sections, not used since the
        /// previous pass, while the cached mappings are over their memory budget (about 1 GiB). Called periodically by the
        /// viewer's cache-cleaning timer, on the UI thread, so it does not wait for the pass.
        /// </summary>
        public void ReduceCacheFootprint() => Task.Run(SectionMappingCache.EnforceMemoryBudget);

        //static private ConcurrentDictionary<string, MappingBase> mapTable = new ConcurrentDictionary<string, MappingBase>();

        protected static string BuildKey(string VolumeTransformName, Section section, string SectionTransformName)
        {
            string key = VolumeTransformName + '-' + section.Number.ToString("D4") + '-' + SectionTransformName;
            return key;
        }


        /// <summary>
        /// Tileset: cache key uses ChannelName (warp is baked into the tiles).
        /// Pyramid: cache key uses SectionTransformName (mosaic stos); CurrentPyramid is then set to ChannelName.
        /// Null VolumeTransformName is mosaic-only. Missing stos falls back to mosaic so the view is not blank.
        /// Returns null when the section or channel does not exist.
        /// </summary>
        public MappingBase GetMapping(string VolumeTransformName, int SectionNumber, string ChannelName, string SectionTransformName)
        {
            if (!volume.Sections.ContainsKey(SectionNumber))
            {
                return null;
            }

            SectionTransformsDictionary dict = SectionMappingCache.Fetch(SectionNumber) ?? SectionMappingCache.GetOrAdd(SectionNumber, new SectionTransformsDictionary());
            MappingBase transform = GetMappingForSection(dict, VolumeTransformName, SectionNumber, ChannelName, SectionTransformName);
            return transform;
        }

        private MappingBase GetMappingForSection(SectionTransformsDictionary transformsForSection, string VolumeTransformName, int SectionNumber, string ChannelName, string SectionTransformName)
        {
            Section section = volume.Sections[SectionNumber];

            SectionTransformName ??= section.DefaultPyramidTransform;
            ChannelName ??= "";

            //If the transform is rolled into the tiles then use the channel name to generate the key
            string key;
            string SectionMapKey = "";
            bool success;
            MappingBase mapping;
            if (section.TilesetNames.Contains(ChannelName))
            {
                //It is a tileset
                key = BuildKey(VolumeTransformName, section, ChannelName);

                //Return the map if we have it.

                success = transformsForSection.TryGetValue(key, out mapping);
                if (success)
                    return mapping;

                SectionMapKey = ChannelName;
            }
            else if (section.ImagePyramids.TryGetValue(ChannelName, out var pyramid))
            {
                //It is a pyramid + Transform
                key = BuildKey(VolumeTransformName, section, SectionTransformName);
                //Return the map if we have it. 
                success = transformsForSection.TryGetValue(key, out mapping);
                if (success)
                {
                    FixedTileCountMapping FixedTileMapping = mapping as FixedTileCountMapping;
                    //Set the image pyramid the transform is working against so we know how many levels we have available
                    FixedTileMapping.CurrentPyramid = pyramid;

                    return mapping;
                }

                SectionMapKey = SectionTransformName;
            }
            else
            {
                //Hmm... Try loading the default
                if (section.DefaultChannel != ChannelName)
                    return GetMapping(VolumeTransformName, SectionNumber, section.DefaultChannel, section.DefaultPyramidTransform);
                else
                    return null;
            }

            //Return the map if we have it. 
            success = transformsForSection.TryGetValue(key, out mapping);
            if (success)
                return mapping;

            //We don't need a fancy mapping.  Add a reference from the section to the mapTable
            if (false == section.WarpedTo.TryGetValue(SectionMapKey, out MappingBase sectionWarpedToMapValue))
            {
                return null;
            }

            if (VolumeTransformName is null)
            {
                MappingBase output = transformsForSection.GetOrAdd(key, sectionWarpedToMapValue);

                if (output is FixedTileCountMapping fixedMapping)
                {
                    Pyramid ImagePyramid = section.ImagePyramids[ChannelName];
                    fixedMapping.CurrentPyramid = ImagePyramid;

                }
                return output;
            }
            else
            {
                //We have to create a volume transform for the requested map 
                if (false == volume.Transforms.TryGetValue(VolumeTransformName, out SortedList<int, ITransform> stosTransforms))
                    return null;

                if (false == stosTransforms.TryGetValue(section.Number, out var transform))
                {
                    //Maybe we are the reference section, check if there is a mapping for no transform.  This at least prevents displaying
                    //a blank screen
                    return GetMapping(null, SectionNumber, ChannelName, SectionTransformName);
                }

                if (transform is null)
                {
                    //A transform was unable to be generated placing the section in the transform.  Use a mosaic instead
                    return GetMapping(null, SectionNumber, ChannelName, SectionTransformName);
                }

                MappingBase output = section.CreateSectionToVolumeMapping(transform, SectionMapKey, key);
                if (output is FixedTileCountMapping fixedMapping)
                {
                    Pyramid ImagePyramid = section.ImagePyramids[ChannelName];
                    fixedMapping.CurrentPyramid = ImagePyramid;
                }

                output = transformsForSection.GetOrAdd(key, output);
                return output;
            }
        }
    }
}

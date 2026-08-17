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
    /// Per-section mapping cache. Evicting an entry calls FreeMemory on every mapping for that section.
    /// </summary>
    public class SectionTransformsCache : TimeQueueCache<int, SectionMappingsCacheEntry, SectionTransformsDictionary, SectionTransformsDictionary>
    {
        public long NumSectionsToKeepInMemory
        {
            get => this.MaxCacheSize;
            set => this.MaxCacheSize = value;
        }


        public SectionTransformsCache()
        {
            this.NumSectionsToKeepInMemory = 6;
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

        public void ReduceCacheFootprint() => SectionMappingCache.ReduceCacheFootprint(null);

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

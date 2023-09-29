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

    public class SectionTransformsCache : TimeQueueCache<int, SectionMappingsCacheEntry, SectionTransformsDictionary, SectionTransformsDictionary>
    {
        public long NumSectionsToKeepInMemory
        {
            get { return this.MaxCacheSize; }
            set { this.MaxCacheSize = value; }
        }


        public SectionTransformsCache()
        {
            this.NumSectionsToKeepInMemory = 6; //Total number of sections we will keep loaded by default
        }

        protected override SectionTransformsDictionary Fetch(SectionMappingsCacheEntry entry)
        {
            return entry.TransformsForSection;
        }

        protected override SectionMappingsCacheEntry CreateEntry(int key, SectionTransformsDictionary entry)
        {
            SectionMappingsCacheEntry cacheEntry = new SectionMappingsCacheEntry(key, entry);
            return cacheEntry;
        }

        protected override SectionMappingsCacheEntry CreateEntry(int key, Func<int, SectionTransformsDictionary> entryFactory)
        {
            SectionMappingsCacheEntry cacheEntry = new SectionMappingsCacheEntry(key, entryFactory(key));
            return cacheEntry;
        }

        protected override Task<SectionMappingsCacheEntry> CreateEntryAsync(int key, SectionTransformsDictionary entry)
        {
            SectionMappingsCacheEntry cacheEntry = new SectionMappingsCacheEntry(key, entry);
            return Task.FromResult(cacheEntry);
        }
    }

    public class SectionMappingsCacheEntry : CacheEntry<int>
    {
        public SectionTransformsDictionary TransformsForSection = new SectionTransformsDictionary();

        public SectionMappingsCacheEntry(int SectionNumber, SectionTransformsDictionary entry) :
            base(SectionNumber)
        {
            this.Size = 1;
            this.TransformsForSection = entry;
        }

        public override sealed void Dispose()
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
    /// This class holds references to all of the Mapping objects that are created during runtime and 
    /// creates mappings on the fly as needed
    /// </summary>
    public class MappingManager
    {
        private readonly VolumeModel.Volume volume;

        public SectionTransformsCache SectionMappingCache = new SectionTransformsCache();

        public MappingManager(Volume Volume)
        {
            this.volume = Volume;
        }

        public void ReduceCacheFootprint()
        {
            SectionMappingCache.ReduceCacheFootprint(null);
        }

        //static private ConcurrentDictionary<string, MappingBase> mapTable = new ConcurrentDictionary<string, MappingBase>();

        protected static string BuildKey(string VolumeTransformName, Section section, string SectionTransformName)
        {
            string key = VolumeTransformName + '-' + section.Number.ToString("D4") + '-' + SectionTransformName;
            return key;
        }


        /// <summary>
        /// Find the Mapping for the requested transform, section, sectiontransform tuple
        /// </summary>
        /// <param name="VolumeTransformName"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="SectionTransformName"></param>
        /// <returns></returns>
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

            if (SectionTransformName == null)
                SectionTransformName = section.DefaultPyramidTransform;
            if (ChannelName == null)
            {
                ChannelName = "";
            }

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
            else if (section.ImagePyramids.ContainsKey(ChannelName))
            {
                //It is a pyramid + Transform
                key = BuildKey(VolumeTransformName, section, SectionTransformName);
                //Return the map if we have it. 
                success = transformsForSection.TryGetValue(key, out mapping);
                if (success)
                {
                    FixedTileCountMapping FixedTileMapping = mapping as FixedTileCountMapping;
                    //Set the image pyramid the transform is working against so we know how many levels we have available
                    Pyramid ImagePyramid = section.ImagePyramids[ChannelName];
                    FixedTileMapping.CurrentPyramid = ImagePyramid;

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
            if (false == section.WarpedTo.ContainsKey(SectionMapKey))
            {
                return null;
            }

            MappingBase map = null;
            if (VolumeTransformName == null)
            {
                map = section.WarpedTo[SectionMapKey];
                map = transformsForSection.GetOrAdd(key, map);

                if (map is FixedTileCountMapping fixedMapping)
                {
                    Pyramid ImagePyramid = section.ImagePyramids[ChannelName];
                    fixedMapping.CurrentPyramid = ImagePyramid;

                }
                return map;
            }
            else
            {
                //We have to create a volume transform for the requested map 
                if (false == volume.Transforms.ContainsKey(VolumeTransformName))
                    return null;

                SortedList<int, ITransform> stosTransforms = volume.Transforms[VolumeTransformName];
                if (false == stosTransforms.ContainsKey(section.Number))
                {
                    //Maybe we are the reference section, check if there is a mapping for no transform.  This at least prevents displaying
                    //a blank screen
                    return GetMapping(null, SectionNumber, ChannelName, SectionTransformName);
                }

                if (stosTransforms[section.Number] == null)
                {
                    //A transform was unable to be generated placing the section in the transform.  Use a mosaic instead
                    return GetMapping(null, SectionNumber, ChannelName, SectionTransformName);
                }

                map = section.CreateSectionToVolumeMapping(stosTransforms[section.Number], SectionMapKey, key);
                if (map is FixedTileCountMapping fixedMapping)
                {
                    Pyramid ImagePyramid = section.ImagePyramids[ChannelName];
                    fixedMapping.CurrentPyramid = ImagePyramid;
                }

                map = transformsForSection.GetOrAdd(key, map);
                return map;
            }
        }
    }
}

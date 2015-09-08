using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml; 
using System.Xml.Linq;
using Geometry;
using System.Collections.Concurrent;
using Common;

namespace Viking.VolumeModel
{
    public class SectionTransformsDictionary : ConcurrentDictionary<string, MappingBase>
    {

    }

    public class SectionTransformsCache : Common.DataStructures.TimeQueueCache<int, SectionMappingsCacheEntry, SectionTransformsDictionary, SectionTransformsDictionary>
    {
        public long NumSectionsToKeepInMemory
        {
            get { return this.MaxCacheSize; }
            set { this.MaxCacheSize = value; }
        }


        public SectionTransformsCache()
        {
            this.NumSectionsToKeepInMemory = 10; //Total number of sections we will keep loaded by default
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

        protected override bool OnRemoveEntry(SectionMappingsCacheEntry entry)
        {
            foreach(MappingBase mapping in entry.TransformsForSection.Values)
            {
                mapping.FreeMemory();
            }

            return base.OnRemoveEntry(entry);
        }
    }

    public class SectionMappingsCacheEntry : Common.DataStructures.CacheEntry< int >
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
            /*
            foreach (MappingBase mapping in this.TransformsForSection)
            {
                mapping
            }
             */

            if (TransformsForSection != null)
            {
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
        static public SectionTransformsCache SectionMappingCache = new SectionTransformsCache();
         
        //static private ConcurrentDictionary<string, MappingBase> mapTable = new ConcurrentDictionary<string, MappingBase>();

        public static string BuildKey(string VolumeTransformName, Section section, string SectionTransformName)
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
        public static MappingBase GetMapping(string VolumeTransformName, Section section, string ChannelName, string SectionTransformName)
        {
            if (section == null)
                return null;

            SectionTransformsDictionary dict = SectionMappingCache.Fetch(section.Number);
            if(dict == null)
            {
                dict = SectionMappingCache.GetOrAdd(section.Number, new SectionTransformsDictionary());
            }

            MappingBase transform = GetMappingForSection(dict, VolumeTransformName, section, ChannelName, SectionTransformName);
            SectionMappingCache.ReduceCacheFootprint(null);
            return transform;
        }

        private static MappingBase GetMappingForSection(SectionTransformsDictionary transformsForSection, string VolumeTransformName, Section section, string ChannelName, string SectionTransformName)
        {

            if (VolumeTransformName == null)
                VolumeTransformName = "None"; 
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
            else if(section.ImagePyramids.ContainsKey(ChannelName))
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
                    return GetMapping(VolumeTransformName, section, section.DefaultChannel, section.DefaultPyramidTransform);
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
            if (VolumeTransformName == "None")
            {
                map = section.WarpedTo[SectionMapKey];
                map = transformsForSection.GetOrAdd(key, map);

                FixedTileCountMapping fixedMapping = map as FixedTileCountMapping;
                if (fixedMapping != null)
                {
                    Pyramid ImagePyramid = section.ImagePyramids[ChannelName];
                    fixedMapping.CurrentPyramid = ImagePyramid;

                }
                return map;
            }
            else
            {
                //We have to create a volume transform for the requested map
                Volume volume = section.volume; 
                if(false == volume.Transforms.ContainsKey(VolumeTransformName))
                    return null;

                SortedList<int, Geometry.Transforms.TriangulationTransform> stosTransforms = volume.Transforms[VolumeTransformName];
                if (false == stosTransforms.ContainsKey(section.Number))
                {
                    //Maybe we are the reference section, check if there is a mapping for no transform.  This at least prevents displaying
                    //a blank screen
                    return GetMapping("None", section, ChannelName, SectionTransformName); 
                }

                if (stosTransforms[section.Number] == null)
                {
                    //A transform was unable to be generated placing the section in the transform.  Use a mosaic instead
                    return GetMapping("None", section, ChannelName, SectionTransformName); 
                }

                map = section.CreateSectionToVolumeMapping(stosTransforms[section.Number], SectionMapKey, key);
                FixedTileCountMapping fixedMapping = map as FixedTileCountMapping;
                if(fixedMapping != null)
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

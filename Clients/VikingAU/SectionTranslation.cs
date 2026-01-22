using Geometry.Graphics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.Collections;
using Geometry;

namespace Viking.AU
{
    internal class SectionTranslationJSON
    {
        public Dictionary<long, SectionTranslationEntryJSON> Sections;

        /// <summary>
        /// Only modify annotations before the specified date
        /// </summary>
        public DateTime? DefaultTranslateBefore { get; set; }
    }

    internal class SectionTranslationEntryJSON
    {
        /// <summary>
        /// Amount to translate in X
        /// </summary>
        public double X { get; set; }

        public double Y { get; set; }

        /// <summary>
        /// Only modify annotations before the specified date
        /// </summary>
        public DateTime? TranslateBefore { get; set; }
    }

    public readonly struct SectionTranslation(long sectionNumber, GridVector2 offset, DateTime datecutoff)
    {
        /// <summary>
        /// Section number to translate
        /// </summary>
        public readonly long SectionNumber = sectionNumber;

        public readonly GridVector2 Offset = offset;

        /// <summary>
        /// Only modify annotations before the specified date
        /// </summary>
        public readonly DateTime TranslateBefore = datecutoff;
    }

    class SectionTranslations : IReadOnlyDictionary<long, SectionTranslation>
    {
        readonly SortedList<long, SectionTranslation> _offsetTable = [];
        private static long ConvertKey(string str) => System.Convert.ToInt64(str);

        public void Add(long key, SectionTranslation color) => _offsetTable.Add(key, color);

        public bool ContainsKey(long key) => _offsetTable.ContainsKey(key);
        public bool TryGetValue(long key, out SectionTranslation value) => _offsetTable.TryGetValue(key, out value);

        public SectionTranslation this[long key] => _offsetTable[key];
        public IEnumerable<long> Keys => _offsetTable.Keys;
        public IEnumerable<SectionTranslation> Values => _offsetTable.Values;


        public static SectionTranslations CreateFromConfigFile(string config_txt_full_path)
        {
            string full_path = System.IO.Path.GetFullPath(config_txt_full_path);
            if (!System.IO.File.Exists(full_path))
            {
                throw new System.IO.FileNotFoundException("Translation file not found " + full_path);
            }

            System.IO.FileInfo fi = new(config_txt_full_path);

            SectionTranslations output = [];
            string config = System.IO.File.ReadAllText(full_path);
            //Use the last write time as the date cutoff, this discourages users from re-translating the same sections
            output.AddFromJSON(config, fi.LastWriteTimeUtc);
            return output;
        }



        /// <summary>
        /// Adds entries to this object from a JSON string
        /// Parses a JSON dictionary into a SectionTranslations object, example of JSON:
        /// {
        ///     1: { 
        ///     "X": 10.0,
        ///     "Y": 20.0,
        ///     "TranslateBefore": "2021-08-01T00:00:00"
        ///     },
        /// }
        /// </summary>
        /// <param name="config_data"></param>
        /// <param name="defaultDateCutoff">If a date is provided, use that value for unspecfied DateCutoffs.</param>
        /// <returns></returns>
        public void AddFromJSON(string config_data, DateTime defaultCutoffDate)
        {
            var input = Newtonsoft.Json.JsonConvert.DeserializeObject<SectionTranslationJSON>(config_data);

            //Use the date in the file before using the passed cutoff date
            defaultCutoffDate = input.DefaultTranslateBefore ?? defaultCutoffDate;

            foreach (var kvp in input.Sections)
            {
                SectionTranslation sectionData = new(kvp.Key,
                                             new GridVector2(kvp.Value.X, kvp.Value.Y),
                                             kvp.Value.TranslateBefore ?? defaultCutoffDate);

                this.Add(kvp.Key, sectionData);
            }

            return;
        }

        public IEnumerator<KeyValuePair<long, SectionTranslation>> GetEnumerator() => _offsetTable.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _offsetTable.GetEnumerator();

        public int Count => _offsetTable.Count;
    }


}
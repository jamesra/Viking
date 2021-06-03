using System;
using System.Collections.Generic;
using System.Linq;

namespace Annotation
{
    internal static class Utils
    {
        public static List<long[]> SortAndChunk(this ICollection<long> IDs, uint maxchunksize)
        {
            List<long> list_IDs = IDs.ToList();
            list_IDs.Sort();
            int numChunks = (int)Math.Ceiling((float)IDs.Count / (float)maxchunksize);
            int chunk_size = (int)Math.Ceiling((float)IDs.Count / numChunks);

            List<long[]> output = new List<long[]>(numChunks);

            while (list_IDs.Count > 0)
            {
                int NumIDs = list_IDs.Count < chunk_size ? list_IDs.Count : chunk_size;

                long[] ShorterIDArray = new long[NumIDs];

                list_IDs.CopyTo(0, ShorterIDArray, 0, NumIDs);
                list_IDs.RemoveRange(0, NumIDs);

                //yield(ShorterIDArray);
                output.Add(ShorterIDArray);
            }

            return output;
        }
    }
}

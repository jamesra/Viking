using System;
using System.Collections.Generic;
using System.Linq;

namespace Annotation
{
    internal static class Utils
    {
        public static List<long[]> SortAndChunk(this ICollection<long> IDs, uint maxchunksize)
        {
            //We created a copy of the input so no need to copy it again to sort
            return IDs.ToArray().SortAndChunk(maxchunksize, CanSortIDsInPlace: true); 
        }

        public static List<long[]> SortAndChunk(this long[] IDs, uint maxchunksize, bool CanSortIDsInPlace = false)
        {
            long[] sorted;
            if(CanSortIDsInPlace)
            { 
                sorted = IDs;
            }
            else
            {
                sorted = new long[IDs.Length];
                IDs.CopyTo(sorted, 0);                
            }

            int count = IDs.Length;
            Array.Sort(sorted);

            int numChunks = (int)Math.Ceiling((float)count / (float)maxchunksize);
            int chunk_size = (int)Math.Ceiling((float)count / numChunks);

            List<long[]> output = new(numChunks);

            for (int i = 0; i < numChunks; i++)
            {
                int start = i * chunk_size;
                int len = Math.Min(chunk_size, count - start);
                if (len <= 0)
                    continue;
                long[] chunk = new long[len];
                Array.Copy(sorted, start, chunk, 0, len);
                output.Add(chunk);
            }

            return output;
        }
    }
}

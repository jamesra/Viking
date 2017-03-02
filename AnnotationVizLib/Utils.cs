using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnnotationVizLib
{
    
    public static class Utils<T>
    {
        private static int NumberOfChunks(int totalsize, int Chunksize)
        {
            return (totalsize / Chunksize) + ((totalsize % Chunksize == 0) ? 0 : 1);
        }

        public static List<T>[] SplitListIntoChunks(List<T> input, int Chunksize)
        {
            int numChunks = NumberOfChunks(input.Count, Chunksize);

            List<T>[] arrayOfLists = new List<T>[numChunks];

            if (input.Count < Chunksize)
            {
                arrayOfLists[0] = input;
            }
            else
            {
                for (int i = 0; i < numChunks; i++)
                {
                    int iStartRange = (i * Chunksize);
                    int rangeCount = iStartRange + Chunksize > input.Count ? input.Count - iStartRange : Chunksize;
                    arrayOfLists[i] = new List<T>(input.GetRange(iStartRange, rangeCount));
                }
            }

            return arrayOfLists;
        }
    } 
}

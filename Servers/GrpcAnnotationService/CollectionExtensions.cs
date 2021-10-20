using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gRPCAnnotationService
{
    public static class LongKeyExtensions
    {
        public static IEnumerable<long[]> Chunk(this long[] input, int Chunksize = 2000)
        {
            return CollectionExtensions<long>.Chunk(input, Chunksize);
        }
    }

    public static class CollectionExtensions<T>
    {
        /// <summary>
        /// SQL queries using a CONTAINS clause have a limit to the collection they can be compared against.  This helper function
        /// chunks input collection into a set of smaller collections no longer than the chunk size.  Each chunk can then be sent to the database as a query
        /// </summary>
        /// <param name="input"></param>
        /// <param name="ChunkSize"></param>
        /// <returns></returns>
        public static IEnumerable<T[]> Chunk(T[] input, int ChunkSize = 2000)
        { 
            if (input.Length <= ChunkSize)
            { 
                yield return input;
                yield break;
            }

            int remaining_length = input.Length;
            int iStart = 0;
            T[] chunk = new T[ChunkSize]; //Most chunks are the same size, so allocate a chunk size array first
            while (remaining_length > 0)
            { 
                int chunk_length = remaining_length > ChunkSize ? ChunkSize : remaining_length;
                
                //Reallocate T[] if it does not match the chunk length
                if(chunk_length != chunk.Length)
                {
                    chunk = new T[chunk_length];
                }

                Array.Copy(input, iStart, chunk, 0, chunk_length);

                yield return chunk;
                iStart += chunk_length;
                remaining_length -= chunk_length;
            }

            yield break;
        }
    }
}

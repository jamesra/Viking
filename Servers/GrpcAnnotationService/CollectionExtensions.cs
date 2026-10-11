using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Extension form of <see cref="CollectionExtensions{T}.Chunk"/> for the <c>long</c> ID arrays the
    /// gRPC annotation services pass to EF <c>Contains</c> queries.
    /// </summary>
    public static class LongKeyExtensions
    {
        /// <summary>
        /// Same as <see cref="CollectionExtensions{T}.Chunk"/>, including its buffer-reuse contract.
        /// Called with no size argument, this binds here rather than to <c>Enumerable.Chunk</c>, which has no default size.
        /// </summary>
        public static IEnumerable<long[]> Chunk(this long[] input, int Chunksize = 2000)
        {
            return CollectionExtensions<long>.Chunk(input, Chunksize);
        }
    }

    /// <summary>
    /// Array chunking for the gRPC annotation services; see <see cref="Chunk"/>.
    /// </summary>
    /// <typeparam name="T">Element type of the array being split.</typeparam>
    public static class CollectionExtensions<T>
    {
        /// <summary>
        /// Splits ID lists so SQL IN / Contains stays under the parameter limit. Default 2000 matches the services.
        /// </summary>
        /// <param name="input">Array to split. Not modified.</param>
        /// <param name="ChunkSize">Maximum elements per chunk; must be positive.</param>
        /// <returns>
        /// <paramref name="input"/> itself when it fits in one chunk (so empty input yields one empty chunk);
        /// otherwise full chunks of <paramref name="ChunkSize"/> followed by one shorter remainder if needed.
        /// </returns>
        /// <remarks>
        /// Full-size chunks share one buffer that is overwritten on the next iteration, so callers must
        /// finish using each chunk (for example, run its query with <c>ToListAsync</c>) before advancing,
        /// and must copy a chunk they need to keep. Pinned by <c>CollectionExtensionsChunkTests</c>.
        /// </remarks>
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

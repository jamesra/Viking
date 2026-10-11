using System.Collections.Generic;
using System.Linq;
using FsCheck;
using gRPCAnnotationService;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins <see cref="CollectionExtensions{T}.Chunk"/>, which splits ID lists so EF <c>Contains</c>
    /// queries in the gRPC annotation services stay under SQL Server's parameter limit.
    /// Chunks are copied out with <c>ToArray</c> because the method reuses one buffer across yields.
    /// </summary>
    public class CollectionExtensionsChunkTests
    {
        /// <summary>Input lengths up to a few chunks of the default size, and chunk sizes from 1 to above the default.</summary>
        private static Arbitrary<(long[] Input, int Size)> InputAndSize() =>
            Arb.From(
                from length in Gen.Choose(0, 7000)
                from size in Gen.Choose(1, 2500)
                select (Enumerable.Range(0, length).Select(i => (long)i * 7919 - 3).ToArray(), size));

        private static List<long[]> CopyChunks(long[] input, int size) =>
            CollectionExtensions<long>.Chunk(input, size).Select(c => c.ToArray()).ToList();

        [Fact]
        public void ConcatenatedChunksEqualInput()
        {
            Prop.ForAll(InputAndSize(), t =>
                CopyChunks(t.Input, t.Size).SelectMany(c => c).SequenceEqual(t.Input)).QuickCheckThrowOnFailure();
        }

        /// <summary>Every chunk but the last is exactly full, the last is non-empty, and the count is ceil(n/size) (one for empty input).</summary>
        [Fact]
        public void ChunksAreFullExceptTheLast()
        {
            Prop.ForAll(InputAndSize(), t =>
            {
                var chunks = CopyChunks(t.Input, t.Size);
                int expectedCount = t.Input.Length == 0 ? 1 : (t.Input.Length + t.Size - 1) / t.Size;
                return chunks.Count == expectedCount
                    && chunks.Take(chunks.Count - 1).All(c => c.Length == t.Size)
                    && chunks[^1].Length == t.Input.Length - t.Size * (chunks.Count - 1);
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>Input that fits in one chunk (including empty input) is yielded as-is, not copied.</summary>
        [Theory]
        [InlineData(0, 5)]
        [InlineData(3, 5)]
        [InlineData(5, 5)]
        public void InputThatFitsIsYieldedByReference(int length, int size)
        {
            var input = new long[length];
            var chunk = Assert.Single(CollectionExtensions<long>.Chunk(input, size));
            Assert.Same(input, chunk);
        }

        /// <summary>
        /// Full-size chunks share one buffer that the next iteration overwrites, so a caller must
        /// finish with each chunk before advancing. Every current caller runs its query to completion inside the loop.
        /// </summary>
        [Fact]
        public void FullChunksReuseOneBuffer()
        {
            var input = Enumerable.Range(0, 9).Select(i => (long)i).ToArray();
            var chunks = CollectionExtensions<long>.Chunk(input, 3).ToList();
            Assert.Equal(3, chunks.Count);
            Assert.Same(chunks[0], chunks[1]);
            Assert.Same(chunks[1], chunks[2]);
            Assert.Equal(new long[] { 6, 7, 8 }, chunks[0]);
        }

        [Fact]
        public void LongExtensionUsesDefaultSizeOf2000()
        {
            var input = Enumerable.Range(0, 2001).Select(i => (long)i).ToArray();
            var lengths = LongKeyExtensions.Chunk(input).Select(c => c.Length).ToList();
            Assert.Equal(new[] { 2000, 1 }, lengths);
        }

        [Fact]
        public void LongExtensionPassesChunkSizeThrough()
        {
            var input = Enumerable.Range(0, 7).Select(i => (long)i).ToArray();
            var lengths = LongKeyExtensions.Chunk(input, 3).Select(c => c.Length).ToList();
            Assert.Equal(new[] { 3, 3, 1 }, lengths);
        }
    }
}

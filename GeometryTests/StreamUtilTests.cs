using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="StreamUtil.ToLinesAsync"/>, which <c>TilesToSectionMapping</c> uses to load .mosaic files and
    /// <c>TransformFactory</c> uses to load .stos files. Covers a stream whose <see cref="Stream.Length"/> overstates
    /// its data (must terminate with the lines actually present rather than spin forever) and well-formed streams read in
    /// small chunks, where multi-byte UTF-8 characters and \r\n pairs split across read boundaries must decode exactly as
    /// <see cref="StreamUtil.ToLines(string)"/> splits the same text.
    /// </summary>
    [TestClass]
    public class StreamUtilTests
    {
        /// <summary>
        /// Upper bound on how long one ToLinesAsync call may take before the test treats it as hung.
        /// The streams here are in memory and tiny, so a real result arrives in milliseconds.
        /// </summary>
        private static readonly TimeSpan HangTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// A seekable in-memory stream that returns at most <c>maxChunk</c> bytes per read and reports
        /// <c>extraLength</c> more bytes in <see cref="Length"/> than it holds, like a file truncated while being read.
        /// <c>canSeek</c> false hides seeking so the non-seekable branch can be exercised on the same data.
        /// </summary>
        private sealed class ChunkedStream(byte[] data, int maxChunk, long extraLength = 0, bool canSeek = true) : Stream
        {
            private readonly MemoryStream inner = new(data, false);

            public override bool CanRead => true;
            public override bool CanSeek => canSeek;
            public override bool CanWrite => false;
            public override long Length => canSeek ? inner.Length + extraLength : throw new NotSupportedException();

            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                inner.Read(buffer, offset, Math.Min(count, maxChunk));

            public override long Seek(long offset, SeekOrigin origin) =>
                canSeek ? inner.Seek(offset, origin) : throw new NotSupportedException();

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static string[] ReadLines(Stream stream)
        {
            Task<string[]> task = Task.Run(stream.ToLinesAsync);
            Assert.IsTrue(task.Wait(HangTimeout), "ToLinesAsync did not finish; it is looping on a 0-byte read.");
            return task.Result;
        }

        [TestMethod]
        public void LengthOverstatingDataReturnsTheLinesPresent()
        {
            byte[] data = Encoding.UTF8.GetBytes("Number of Images: 2\r\ntile1.png\ntile2.png");
            using ChunkedStream stream = new(data, maxChunk: 4096, extraLength: 100);

            CollectionAssert.AreEqual(new[] { "Number of Images: 2", "tile1.png", "tile2.png" }, ReadLines(stream));
        }

        [TestMethod]
        public void EmptyStreamWithOverstatedLengthReturnsOneEmptyLine()
        {
            using ChunkedStream stream = new([], maxChunk: 4096, extraLength: 8);

            CollectionAssert.AreEqual(new[] { "" }, ReadLines(stream));
        }

        [TestMethod]
        public void EmptyStreamReturnsOneEmptyLine()
        {
            using ChunkedStream stream = new([], maxChunk: 4096);

            CollectionAssert.AreEqual(new[] { "" }, ReadLines(stream));
        }

        [TestMethod]
        public void ReadsFromTheStartWhenPositionedAtTheEnd()
        {
            byte[] data = Encoding.UTF8.GetBytes("a\nb\n");
            using ChunkedStream stream = new(data, maxChunk: 4096);
            stream.Position = data.Length;

            CollectionAssert.AreEqual(new[] { "a", "b", "" }, ReadLines(stream));
        }

        [TestMethod]
        public void Utf8ByteOrderMarkIsNotPartOfTheFirstLine()
        {
            byte[] data = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("é\r\n€")).ToArray();
            using ChunkedStream stream = new(data, maxChunk: 1);

            CollectionAssert.AreEqual(new[] { "é", "€" }, ReadLines(stream));
        }

        /// <summary>
        /// Text built from ASCII, two-, three-, and four-byte UTF-8 characters joined by mixed line endings,
        /// read through seekable and non-seekable streams that return 1 to 7 bytes per read, decodes to the same
        /// lines as splitting the original string. Small chunks put multi-byte characters and \r\n pairs across reads.
        /// </summary>
        [TestMethod]
        public void ChunkedReadsMatchSplittingTheString()
        {
            string[] pieces = ["a", "Z", "7", " ", "é", "ß", "€", "中", "\U0001D11E", "\r\n", "\n", "\r"];
            Gen<string> textGen = Gen.Elements(pieces).ListOf().Select(p => string.Concat(p));
            Arbitrary<(string Text, int Chunk, bool CanSeek)> arb = Arb.From(
                from text in textGen
                from chunk in Gen.Choose(1, 7)
                from canSeek in Arb.Generate<bool>()
                select (text, chunk, canSeek));

            CoreCheck.Run(
                Prop.ForAll(arb, c =>
                {
                    using ChunkedStream stream = new(Encoding.UTF8.GetBytes(c.Text), c.Chunk, canSeek: c.CanSeek);
                    return ReadLines(stream).SequenceEqual(c.Text.ToLines());
                }),
                nameof(ChunkedReadsMatchSplittingTheString));
        }
    }
}

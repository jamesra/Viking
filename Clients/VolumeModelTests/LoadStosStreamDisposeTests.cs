using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Ensures <see cref="LoadStosResult.LoadAsync(System.IO.Stream, XElement, DateTime?)"/> disposes the
    /// stos stream when <see cref="Geometry.Transforms.TransformFactory.ParseStos"/> throws, so zip entry
    /// streams opened during volume load are not leaked.
    /// </summary>
    [TestClass]
    public class LoadStosStreamDisposeTests
    {
        /// <summary>Wraps an inner stream and records whether <see cref="Stream.Dispose"/> ran.</summary>
        private sealed class DisposeTrackingStream(Stream inner) : Stream
        {
            public bool DisposeCalled { get; private set; }

            public override bool CanRead => inner.CanRead;

            public override bool CanSeek => inner.CanSeek;

            public override bool CanWrite => inner.CanWrite;

            public override long Length => inner.Length;

            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCalled = true;
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }

            public override void Flush() => inner.Flush();

            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

            public override void SetLength(long value) => inner.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        }

        [TestMethod]
        public async Task LoadAsync_StreamOverload_DisposesStream_WhenParseStosThrows()
        {
            var element = new XElement(
                "Transform",
                new XAttribute("pixelSpacing", "1"),
                new XAttribute("mappedSection", "35"),
                new XAttribute("controlSection", "37"));

            using (var tracker = new DisposeTrackingStream(new MemoryStream()))
            {
                await Assert.ThrowsExceptionAsync<IndexOutOfRangeException>(async () =>
                    await LoadStosResult.LoadAsync(tracker, element, DateTime.UtcNow).ConfigureAwait(false));

                Assert.IsTrue(tracker.DisposeCalled, "LoadAsync must dispose the stream when parsing fails.");
            }
        }
    }
}

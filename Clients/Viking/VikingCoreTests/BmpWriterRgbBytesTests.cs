using System;
using System.Drawing;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="BmpWriter"/> ARGB byte[] conversion used when saving textures without XNA SaveAsPng.
    /// </summary>
    [TestClass]
    public class BmpWriterRgbBytesTests
    {
        private static byte[] ArgbPixel(byte a, byte r, byte g, byte b) => new[] { b, g, r, a };

        private static byte[] ReadLogicalArgbRows(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData locked = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int logicalRowBytes = bmp.Width * 4;
                var result = new byte[logicalRowBytes * bmp.Height];
                if (locked.Stride == logicalRowBytes)
                {
                    Marshal.Copy(locked.Scan0, result, 0, result.Length);
                }
                else
                {
                    for (int row = 0; row < bmp.Height; row++)
                    {
                        Marshal.Copy(
                            locked.Scan0 + locked.Stride * row,
                            result,
                            row * logicalRowBytes,
                            logicalRowBytes);
                    }
                }

                return result;
            }
            finally
            {
                bmp.UnlockBits(locked);
            }
        }

        [TestMethod]
        public void ToBmp_1x1_KnownArgb_PreservesPixelViaLockBits()
        {
            var buffer = ArgbPixel(0xFF, 0x11, 0x22, 0x33);
            using var bmp = buffer.ToBmp(1, 1);
            Assert.AreEqual(1, bmp.Width);
            Assert.AreEqual(1, bmp.Height);
            CollectionAssert.AreEqual(buffer, ReadLogicalArgbRows(bmp));
        }

        [TestMethod]
        public void ToBmp_5x2_WidthWhereStrideMayExceedWidthTimesFour_PreservesEveryPixel()
        {
            const int width = 5;
            const int height = 2;
            var buffer = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    buffer[i] = (byte)(0x40 + x);
                    buffer[i + 1] = (byte)(0x50 + y);
                    buffer[i + 2] = (byte)(0x60 + x + y);
                    buffer[i + 3] = 0xFF;
                }
            }

            using var bmp = buffer.ToBmp(width, height);
            Assert.AreEqual(width, bmp.Width);
            Assert.AreEqual(height, bmp.Height);
            CollectionAssert.AreEqual(buffer, ReadLogicalArgbRows(bmp));
        }

        [TestMethod]
        public void ToBmp_1x3_NarrowWidthUsesPaddedStride_PreservesRows()
        {
            const int width = 1;
            const int height = 3;
            var buffer = new byte[]
            {
                10, 20, 30, 0xFF,
                11, 21, 31, 0xFE,
                12, 22, 32, 0xFD,
            };

            using var bmp = buffer.ToBmp(width, height);
            CollectionAssert.AreEqual(buffer, ReadLogicalArgbRows(bmp));
        }

        [TestMethod]
        public async Task ToBmpAsync_ByteArray_MatchesSyncFor1x1()
        {
            var buffer = ArgbPixel(0xAA, 0x01, 0x02, 0x03);
            using var sync = buffer.ToBmp(1, 1);
            using var asyncBmp = await buffer.ToBmpAsync(1, 1);
            Assert.AreEqual(sync.Width, asyncBmp.Width);
            Assert.AreEqual(sync.Height, asyncBmp.Height);
            CollectionAssert.AreEqual(ReadLogicalArgbRows(sync), ReadLogicalArgbRows(asyncBmp));
        }

        [TestMethod]
        public void ToBmp_RandomValidBuffers_MatchDimensionsAndLogicalArgbRows()
        {
            var seedGen = Gen.Choose(0, 128).SelectMany(len =>
                len == 0
                    ? Gen.Constant(Array.Empty<byte>())
                    : Gen.ArrayOf(len, Arb.Generate<byte>()));

            Prop.ForAll(
                Arb.From(GenDimensions()),
                Arb.From(seedGen),
                (dims, seed) =>
                {
                    int width = dims.Item1;
                    int height = dims.Item2;
                    var buffer = new byte[width * height * 4];
                    if (seed.Length > 0)
                    {
                        for (int offset = 0; offset < buffer.Length; offset += seed.Length)
                        {
                            int copy = Math.Min(seed.Length, buffer.Length - offset);
                            Buffer.BlockCopy(seed, 0, buffer, offset, copy);
                        }
                    }

                    using var bmp = buffer.ToBmp(width, height);
                    return bmp.Width == width
                           && bmp.Height == height
                           && buffer.SequenceEqual(ReadLogicalArgbRows(bmp));
                })
                .QuickCheckThrowOnFailure();
        }

        private static Gen<(int, int)> GenDimensions()
        {
            var width = Gen.Choose(1, 48);
            var height = Gen.Choose(1, 48);
            return from w in width
                   from h in height
                   select (w, h);
        }
    }
}

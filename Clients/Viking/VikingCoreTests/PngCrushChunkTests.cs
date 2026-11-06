using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace VikingCoreTests
{
    /// <summary>Reads the gain Nornir stamps into a crushed PNG tile's <c>nornir_crush</c> tEXt chunk.</summary>
    [TestClass]
    public class PngCrushChunkTests
    {
        private static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }

            return ~crc;
        }

        private static byte[] BigEndian(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        /// <summary>A real 4 x 4 greyscale PNG with a tEXt chunk inserted after IHDR, as Pillow writes it.</summary>
        private static MemoryStream PngWithText(string keyword, string text)
        {
            using Bitmap bitmap = new(4, 4, PixelFormat.Format24bppRgb);
            using MemoryStream plain = new();
            bitmap.Save(plain, ImageFormat.Png);
            byte[] png = plain.ToArray();

            byte[] type = Encoding.ASCII.GetBytes("tEXt");
            byte[] body = Encoding.ASCII.GetBytes(keyword + "\0" + text);
            byte[] typeAndBody = new byte[type.Length + body.Length];
            type.CopyTo(typeAndBody, 0);
            body.CopyTo(typeAndBody, type.Length);

            MemoryStream result = new();
            const int afterIhdr = 8 + 4 + 4 + 13 + 4;
            result.Write(png, 0, afterIhdr);
            result.Write(BigEndian((uint)body.Length), 0, 4);
            result.Write(typeAndBody, 0, typeAndBody.Length);
            result.Write(BigEndian(Crc32(typeAndBody)), 0, 4);
            result.Write(png, afterIhdr, png.Length - afterIhdr);
            result.Position = 0;
            return result;
        }

        [TestMethod]
        public void ReadsTheGainAndLeavesTheStreamReadyToDecode()
        {
            using MemoryStream stream = PngWithText("nornir_crush", "{\"step\": 120, \"offset\": 0, \"bits\": 8, \"gain\": 1.0375, \"effective_step\": 115.66}");

            Assert.AreEqual(1.0375, PngCrushChunk.TryReadGain(stream));
            Assert.AreEqual(0, stream.Position, "the stream is back where it started");

            using Bitmap decoded = new(stream);
            Assert.AreEqual(4, decoded.Width);
        }

        [TestMethod]
        public void TileDecodeCarriesTheGain()
        {
            using MemoryStream stream = PngWithText("nornir_crush", "{\"gain\": 0.5}");

            TextureData data = TextureReaderV2.TextureDataFromStream(stream);

            Assert.AreEqual(0.5, data.crushGain);
            Assert.AreEqual(16, data.pixelBytes.Length);
        }

        [TestMethod]
        public void NoGainWhenTheChunkIsAbsentHasNoGainFieldOrIsAnotherKeyword()
        {
            using (MemoryStream plain = new())
            {
                using Bitmap bitmap = new(2, 2, PixelFormat.Format24bppRgb);
                bitmap.Save(plain, ImageFormat.Png);
                plain.Position = 0;
                Assert.IsNull(PngCrushChunk.TryReadGain(plain), "an ordinary PNG");
            }

            using (MemoryStream noGain = PngWithText("nornir_crush", "{\"step\": 120, \"offset\": 0}"))
                Assert.IsNull(PngCrushChunk.TryReadGain(noGain), "a pyramid tile before TileGains ran");

            using (MemoryStream other = PngWithText("Comment", "{\"gain\": 2}"))
                Assert.IsNull(PngCrushChunk.TryReadGain(other), "another keyword");

            using (MemoryStream notPng = new([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]))
                Assert.IsNull(PngCrushChunk.TryReadGain(notPng), "not a PNG");
        }

        [TestMethod]
        public void ParsesGainFormsPythonWrites()
        {
            Assert.AreEqual(1.0, PngCrushChunk.ParseGain("{\"gain\": 1}"));
            Assert.AreEqual(0.0125, PngCrushChunk.ParseGain("{\"gain\":1.25e-2}")!.Value, 1e-12);
            Assert.AreEqual(1.5, PngCrushChunk.ParseGain("{\"a\": 3, \"gain\":   1.5 , \"effective_step\": 80}"));
            Assert.IsNull(PngCrushChunk.ParseGain("{\"effective_gain\": 2}"), "a longer key is not the gain");
        }
    }
}

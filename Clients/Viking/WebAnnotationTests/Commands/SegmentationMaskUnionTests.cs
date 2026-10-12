using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WebAnnotation.UI.Commands.Segmentation;
using Rectangle = Geometry.Rectangle;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The background mask sums squares and polygons into one 1-bit, 1024-square PNG centred on the seed,
    /// image row 0 at the highest y.
    /// </summary>
    [TestClass]
    public class SegmentationMaskUnionTests
    {
        private static readonly Vector2 Centre = new(500, 500);

        private static Image<L8> Decode(Viking.gRPC.SegmentationServiceTypes.V1.Mask mask)
            => Image.Load<L8>(mask.Png.ToByteArray());

        private static bool On(Image<L8> image, Viking.gRPC.SegmentationServiceTypes.V1.Mask mask, int mosaicX, int mosaicY)
            => image[mosaicX - mask.Bounds.XMin, mask.Bounds.YMax - mosaicY].PackedValue == 255;

        [TestMethod]
        public void AnEmptyUnionHasNoMask() => Assert.IsNull(new SegmentationMaskUnion().ToMask(1, Centre));

        [TestMethod]
        public void TheMaskIsAlwaysTheWindowSquareCentredOnTheSeed()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(480, 490), new Vector2(520, 510)));

            var mask = union.ToMask(1, Centre)!;
            using Image<L8> image = Decode(mask);

            Assert.AreEqual(1024, mask.Bounds.XMax - mask.Bounds.XMin + 1);
            Assert.AreEqual(1024, mask.Bounds.YMax - mask.Bounds.YMin + 1);
            Assert.AreEqual(500 - 512, mask.Bounds.XMin);
            Assert.AreEqual(1024, image.Width);
            Assert.AreEqual(1024, image.Height);
            Assert.IsTrue(On(image, mask, 500, 500));
            Assert.IsFalse(On(image, mask, 600, 600));
        }

        [TestMethod]
        public void ThePngIsOneBitPerPixel()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(0, 0), new Vector2(1000, 1000)));

            var mask = union.ToMask(1, Centre)!;
            var info = Image.Identify(mask.Png.ToByteArray());

            Assert.AreEqual(1, info.PixelType.BitsPerPixel);
            Assert.IsTrue(mask.Png.Length < 2000, $"a 1024 square of one shape should compress to a few hundred bytes, got {mask.Png.Length}");
        }

        [TestMethod]
        public void TwoSeparateShapesAreOneMaskAndTheGapStaysOff()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(400, 400), new Vector2(420, 420)));
            union.Add(new Rectangle(new Vector2(600, 600), new Vector2(620, 620)));

            var mask = union.ToMask(1, Centre)!;
            using Image<L8> image = Decode(mask);

            Assert.IsTrue(On(image, mask, 410, 410));
            Assert.IsTrue(On(image, mask, 610, 610));
            Assert.IsFalse(On(image, mask, 510, 510));
            Assert.IsFalse(On(image, mask, 410, 610));
        }

        [TestMethod]
        public void APolygonsHoleStaysOpenAndAnotherShapeMayFillIt()
        {
            Vector2[] outer = [new(450, 450), new(550, 450), new(550, 550), new(450, 550)];
            Vector2[] hole = [new(490, 490), new(510, 490), new(510, 510), new(490, 510)];
            SegmentationMaskUnion union = new();
            union.Add([outer, hole]);

            var mask = union.ToMask(1, Centre)!;
            using (Image<L8> image = Decode(mask))
            {
                Assert.IsTrue(On(image, mask, 460, 460));
                Assert.IsFalse(On(image, mask, 500, 500));
            }

            union.Add(new Rectangle(new Vector2(495, 495), new Vector2(505, 505)));
            mask = union.ToMask(1, Centre)!;
            using Image<L8> filled = Decode(mask);
            Assert.IsTrue(On(filled, mask, 500, 500));
        }

        [TestMethod]
        public void DownsampleScalesTheWindowToMosaicPixels()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(1900, 1900), new Vector2(2100, 2100)));

            var mask = union.ToMask(4, new Vector2(2000, 2000))!;

            Assert.AreEqual(500 - 512, mask.Bounds.XMin);
            Assert.AreEqual(500 - 512 + 1023, mask.Bounds.XMax);
        }

        [TestMethod]
        public void ShapesOutsideTheWindowAreDroppedAndAShapeThatStraddlesItIsClipped()
        {
            SegmentationMaskUnion far = new();
            far.Add(new Rectangle(new Vector2(100000, 100000), new Vector2(100010, 100010)));
            Assert.IsNull(far.ToMask(1, Centre));

            SegmentationMaskUnion straddle = new();
            straddle.Add(new Rectangle(new Vector2(400, 400), new Vector2(100000, 100000)));
            var mask = straddle.ToMask(1, Centre)!;
            using Image<L8> image = Decode(mask);
            Assert.IsTrue(On(image, mask, 500, 500));
            Assert.AreEqual(1024, image.Width);
        }

        [TestMethod]
        public void ACircleSeedIsMaskedAsTheCircleNotItsInscribedSquare()
        {
            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(
                [new Vector2(500, 500), new Vector2(600, 500), new Vector2(500, 600), new Vector2(400, 500), new Vector2(500, 400)],
                out CircleSegmentationPrompts.StartingPrompt prompt));
            Assert.AreEqual(100.0, prompt.Seed.Circle!.Value.Radius, 1e-9);
            SegmentationMaskUnion union = new();
            union.Add(prompt.Seed);

            var mask = union.ToMask(1, Centre)!;
            using Image<L8> image = Decode(mask);

            Assert.IsTrue(On(image, mask, 500, 500));
            Assert.IsTrue(On(image, mask, 425, 500), "inside the circle, outside the inscribed square (half side 70.7)");
            Assert.IsTrue(On(image, mask, 500, 590));
            Assert.IsFalse(On(image, mask, 580, 580), "the corner of the circle's bounding box, 113 from the centre");
            Assert.IsFalse(On(image, mask, 500, 610));
        }
    }
}

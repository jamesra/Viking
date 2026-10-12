using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The background mask sums squares and polygons into one bounds-sized PNG, image row 0 at the highest y.
    /// </summary>
    [TestClass]
    public class SegmentationMaskUnionTests
    {
        private static Image<L8> Decode(Viking.gRPC.SegmentationServiceTypes.V1.Mask mask)
            => Image.Load<L8>(mask.Png.ToByteArray());

        private static bool On(Image<L8> image, Viking.gRPC.SegmentationServiceTypes.V1.Mask mask, int mosaicX, int mosaicY)
            => image[mosaicX - mask.Bounds.XMin, mask.Bounds.YMax - mosaicY].PackedValue == 255;

        [TestMethod]
        public void AnEmptyUnionHasNoMask() => Assert.IsNull(new SegmentationMaskUnion().ToMask(1));

        [TestMethod]
        public void ASquareFillsItsBoundsAndThePngMatchesTheBounds()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(100, 200), new Vector2(140, 230)));

            var mask = union.ToMask(1)!;
            using Image<L8> image = Decode(mask);

            Assert.AreEqual(100, mask.Bounds.XMin);
            Assert.AreEqual(140, mask.Bounds.XMax);
            Assert.AreEqual(200, mask.Bounds.YMin);
            Assert.AreEqual(230, mask.Bounds.YMax);
            Assert.AreEqual(mask.Bounds.XMax - mask.Bounds.XMin + 1, image.Width);
            Assert.AreEqual(mask.Bounds.YMax - mask.Bounds.YMin + 1, image.Height);
            Assert.IsTrue(On(image, mask, 120, 215));
            Assert.IsTrue(On(image, mask, 101, 201));
        }

        [TestMethod]
        public void TwoSeparateShapesAreOneMaskAndTheGapStaysOff()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(0, 0), new Vector2(20, 20)));
            union.Add(new Rectangle(new Vector2(100, 100), new Vector2(120, 120)));

            var mask = union.ToMask(1)!;
            using Image<L8> image = Decode(mask);

            Assert.IsTrue(On(image, mask, 10, 10));
            Assert.IsTrue(On(image, mask, 110, 110));
            Assert.IsFalse(On(image, mask, 60, 60));
            Assert.IsFalse(On(image, mask, 10, 110));
        }

        [TestMethod]
        public void APolygonsHoleStaysOpenAndAnotherShapeMayFillIt()
        {
            Vector2[] outer = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];
            Vector2[] hole = [new(40, 40), new(60, 40), new(60, 60), new(40, 60)];
            SegmentationMaskUnion union = new();
            union.Add([outer, hole]);

            var mask = union.ToMask(1)!;
            using (Image<L8> image = Decode(mask))
            {
                Assert.IsTrue(On(image, mask, 10, 10));
                Assert.IsFalse(On(image, mask, 50, 50));
            }

            union.Add(new Rectangle(new Vector2(45, 45), new Vector2(55, 55)));
            mask = union.ToMask(1)!;
            using Image<L8> filled = Decode(mask);
            Assert.IsTrue(On(filled, mask, 50, 50));
        }

        [TestMethod]
        public void DownsampleScalesTheBoundsToMosaicPixels()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(400, 800), new Vector2(800, 1200)));

            var mask = union.ToMask(4)!;

            Assert.AreEqual(100, mask.Bounds.XMin);
            Assert.AreEqual(200, mask.Bounds.XMax);
            Assert.AreEqual(200, mask.Bounds.YMin);
            Assert.AreEqual(300, mask.Bounds.YMax);
        }

        [TestMethod]
        public void AUnionSpreadOverMoreThanTheMaximumIsNotSent()
        {
            SegmentationMaskUnion union = new();
            union.Add(new Rectangle(new Vector2(0, 0), new Vector2(10, 10)));
            union.Add(new Rectangle(new Vector2(100000, 100000), new Vector2(100010, 100010)));

            Assert.IsNull(union.ToMask(1));
        }

        [TestMethod]
        public void ACircleBecomesItsInscribedSquare()
        {
            // Radius 100 at the origin: the square's half side is 100 / sqrt 2.
            Assert.IsTrue(CircleSegmentationPrompts.TryCreateStartingPrompt(
                [new Vector2(500, 500), new Vector2(600, 500), new Vector2(500, 600), new Vector2(400, 500), new Vector2(500, 400)],
                out CircleSegmentationPrompts.StartingPrompt prompt));
            SegmentationMaskUnion union = new();
            union.Add(prompt.Box);

            var mask = union.ToMask(1)!;
            using Image<L8> image = Decode(mask);

            Assert.IsTrue(On(image, mask, 500, 500));
            Assert.IsTrue(On(image, mask, 440, 440), "inside the square");
            Assert.AreEqual(429, mask.Bounds.XMin, 1);
        }
    }
}

using Geometry;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;
using IntPoint = Viking.gRPC.Geometry.V1.IntPoint;
using IntSize = Viking.gRPC.Geometry.V1.IntSize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// SAM2 logits sent without upscaling are traced at their own resolution and scaled up by the world transform.
    /// </summary>
    [TestClass]
    public class SegmentationLogitFieldTests
    {
        [TestMethod]
        public void HalfPrecisionDecodesTheValuesSam2Logits()
        {
            // Bit patterns from IEEE 754 binary16.
            Assert.AreEqual(0f, SegmentationLogitField.HalfToSingle(0x0000));
            Assert.AreEqual(1f, SegmentationLogitField.HalfToSingle(0x3C00));
            Assert.AreEqual(-2f, SegmentationLogitField.HalfToSingle(0xC000));
            Assert.AreEqual(5.5f, SegmentationLogitField.HalfToSingle(0x4580));
            Assert.AreEqual(-20f, SegmentationLogitField.HalfToSingle(0xCD00));
            Assert.AreEqual(0.5f, SegmentationLogitField.HalfToSingle(0x3800));
            Assert.IsTrue(float.IsPositiveInfinity(SegmentationLogitField.HalfToSingle(0x7C00)));
            Assert.IsTrue(float.IsNaN(SegmentationLogitField.HalfToSingle(0x7E00)));
            Assert.AreEqual(6.1035156e-5f, SegmentationLogitField.HalfToSingle(0x0400), 1e-9f, "smallest normal");
            Assert.AreEqual(5.9604645e-8f, SegmentationLogitField.HalfToSingle(0x0001), 1e-12f, "smallest subnormal");
        }

        [TestMethod]
        public void SoftBytesPutTheIsoLevelAtTheBoundaryAndSaturateFarFromIt()
        {
            const double iso = 1.0;

            byte atIso = SegmentationLogitField.ToSoftByte((float)iso, iso);
            byte justInside = SegmentationLogitField.ToSoftByte((float)iso + 0.1f, iso);
            byte justOutside = SegmentationLogitField.ToSoftByte((float)iso - 0.1f, iso);

            Assert.IsTrue(Math.Abs(atIso - SegmentationMaskPolygonizer.SoftMaskIsoLevel) <= 0.5);
            Assert.IsTrue(justInside >= SegmentationMaskPolygonizer.SoftMaskForeground);
            Assert.IsTrue(justOutside < SegmentationMaskPolygonizer.SoftMaskForeground);
            Assert.AreEqual(255, SegmentationLogitField.ToSoftByte(20f, iso));
            Assert.AreEqual(0, SegmentationLogitField.ToSoftByte(-20f, iso));
            Assert.AreEqual(0, SegmentationLogitField.ToSoftByte(float.NaN, iso));
        }

        [TestMethod]
        public void ASegmentWithLogitsDecodesAtNativeResolutionWithItsPlacement()
        {
            SegmentationServiceTypes.SegmentResult segment = SegmentWithLogits(
                width: 3,
                height: 2,
                scale: 4,
                x: 8,
                y: 12,
                (_, _) => 5f);

            SegmentField field = SegmentationLogitField.Decode(segment, iso: 1.0);

            Assert.AreEqual(3, field.Width);
            Assert.AreEqual(2, field.Height);
            Assert.AreEqual(4, field.Scale);
            Assert.AreEqual(8, field.X);
            Assert.AreEqual(12, field.Y);
            Assert.AreEqual(12, field.PixelWidth);
            Assert.AreEqual(8, field.PixelHeight);
            Assert.IsTrue(field.Data!.All(value => value > SegmentationMaskPolygonizer.SoftMaskForeground));
        }

        [TestMethod]
        public void TheServerFieldKeepsTheLogitsAsSentWhileTheTracedFieldIsSmoothed()
        {
            // One isolated high sample in a low field: smoothing lowers it, the server field must not.
            SegmentationServiceTypes.SegmentResult segment = SegmentWithLogits(
                width: 5,
                height: 5,
                scale: 4,
                x: 0,
                y: 0,
                (x, y) => x == 2 && y == 2 ? 3f : -3f);

            SegmentField smoothed = SegmentationLogitField.Decode(segment, iso: 1.0, sigma: 0.75);
            SegmentField unsmoothed = SegmentationLogitField.Decode(segment, iso: 1.0, sigma: 0);

            int centre = (2 * 5) + 2;
            Assert.IsNotNull(smoothed.ServerData);
            Assert.AreEqual(SegmentationLogitField.ToSoftByte(3f, 1.0), smoothed.ServerData![centre], "as sent");
            Assert.IsTrue(smoothed.Data![centre] < smoothed.ServerData[centre], "the traced field was blurred");
            CollectionAssert.AreEqual(unsmoothed.ServerData, unsmoothed.Data, "sigma 0 traces the logits as sent");
            CollectionAssert.AreEqual(smoothed.ServerData, unsmoothed.ServerData, "the server field does not depend on sigma");
        }

        [TestMethod]
        public void ASegmentWithoutMatchingLogitsHasNoField()
        {
            SegmentationServiceTypes.SegmentResult segment = new()
            {
                Logits = ByteString.CopyFrom(new byte[3]),
                LogitsSize = new IntSize { Width = 2, Height = 2 },
                LogitsOrigin = new IntPoint { X = 3, Y = 4 }
            };

            Assert.IsFalse(SegmentationLogitField.HasLogits(segment));
            Assert.AreEqual(1, SegmentationLogitField.PixelScale(segment));
            Assert.IsNull(SegmentationLogitField.Decode(segment, 1.0).Data);
            Assert.IsFalse(SegmentationLogitField.HasLogits(new SegmentationServiceTypes.SegmentResult()), "no size, no origin");
        }

        [TestMethod]
        public void CenterSampleBoundsMovesAFourPixelSampleByHalfItsWidthAndLeavesAPixelMaskAlone()
        {
            Rectangle bounds = new(new Vector2(0, 0), new Vector2(400, 200));

            Rectangle reduced = SegmentationMaskPolygonizer.CenterSampleBounds(bounds, 100, 50, 4);
            Rectangle pixels = SegmentationMaskPolygonizer.CenterSampleBounds(bounds, 400, 200, 1);

            // One sample is 4 world units here, so half a sample is 2: right and down.
            Assert.AreEqual(2.0, reduced.Left, 1e-9);
            Assert.AreEqual(402.0, reduced.Right, 1e-9);
            Assert.AreEqual(-2.0, reduced.Bottom, 1e-9);
            Assert.AreEqual(198.0, reduced.Top, 1e-9);
            Assert.AreEqual(bounds, pixels);
        }

        [TestMethod]
        public void CleanupRadiusIsMeasuredInSamplesOfTheFieldItRunsOn()
        {
            Assert.AreEqual(0, SegmentationMaskPolygonizer.ScaleCleanupRadius(0, 4));
            Assert.AreEqual(3, SegmentationMaskPolygonizer.ScaleCleanupRadius(3, 1));
            Assert.AreEqual(1, SegmentationMaskPolygonizer.ScaleCleanupRadius(1, 4));
            Assert.AreEqual(2, SegmentationMaskPolygonizer.ScaleCleanupRadius(8, 4));
        }

        [TestMethod]
        public void ACircleOfLogitsTracesToItsTrueEdgeInWorldUnitsNotTheSampleGrid()
        {
            // A 40x40 sample grid, four pixels per sample, so a 160x160 pixel frame mapped 1:1 onto world.
            const int samples = 40;
            const int scale = 4;
            const double radiusPixels = 50.3; // not a multiple of the 4 px sample
            const double centre = (samples * scale) / 2.0;
            SegmentationServiceTypes.SegmentResult segment = SegmentWithLogits(
                samples,
                samples,
                scale,
                0,
                0,
                (x, y) =>
                {
                    double px = ((x + 0.5) * scale) - centre;
                    double py = ((y + 0.5) * scale) - centre;
                    return (float)((radiusPixels - Math.Sqrt((px * px) + (py * py))) * 0.5);
                });
            SegmentField field = SegmentationLogitField.Decode(segment, iso: 0.0);
            Rectangle world = SegmentationMaskPolygonizer.CenterSampleBounds(
                new Rectangle(new Vector2(0, 0), new Vector2(samples * scale, samples * scale)),
                samples,
                samples,
                scale);

            var polygons = SegmentationMaskPolygonizer.CreatePolygons(
                field.Data!,
                field.Width,
                field.Height,
                0,
                0,
                samples,
                samples,
                world,
                0.03,
                null,
                0,
                out _);

            Assert.AreEqual(1, polygons.Count);
            Polygon polygon = polygons[0];
            Vector2 mid = new(centre, centre);
            double[] distances = polygon.ExteriorRing.Select(point => Vector2.Distance(point, mid)).ToArray();
            // Vertices sit on the true circle to well under a sample (4 px); a snap to the sample grid would be up to 2 px off.
            Assert.IsTrue(distances.Max() - radiusPixels < 0.6, $"max {distances.Max()}");
            Assert.IsTrue(radiusPixels - distances.Min() < 0.6, $"min {distances.Min()}");
        }

        [TestMethod]
        public void SmoothingRemovesASingleSampleHoleAndKeepsAStraightEdgeWhereItWas()
        {
            const int width = 20;
            const int height = 12;
            float[] field = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    field[(y * width) + x] = x < 10 ? 4f : -4f;
            }

            // A one-sample hole just under the iso level, deep inside the object.
            field[(6 * width) + 4] = 0.4f;

            float[] smooth = SegmentationLogitField.Smooth(field, width, height, SegmentationLogitField.DefaultSmoothingSigma);

            Assert.IsTrue(smooth[(6 * width) + 4] > 1.0f, "the hole is filled in");
            // The edge between sample 9 (4) and 10 (-4) stays between them: the crossing sits at x = 9.5.
            Assert.IsTrue(smooth[(3 * width) + 9] > 0f && smooth[(3 * width) + 10] < 0f);
            Assert.AreEqual(0.0, smooth[(3 * width) + 9] + smooth[(3 * width) + 10], 0.05, "symmetric about the edge");
        }

        [TestMethod]
        public void SmoothingLeavesAFlatFieldAloneAndZeroSigmaIsTheIdentity()
        {
            float[] flat = Enumerable.Repeat(3f, 5 * 7).ToArray();

            float[] smooth = SegmentationLogitField.Smooth(flat, 5, 7, 0.75);

            Assert.IsTrue(smooth.All(value => Math.Abs(value - 3f) < 1e-4f));
            Assert.AreSame(flat, SegmentationLogitField.Smooth(flat, 5, 7, 0));
            Assert.AreSame(flat, SegmentationLogitField.Smooth(flat, 4, 7, 0.75), "a wrongly sized grid is returned as it came");
        }

        [TestMethod]
        public void ANonFiniteSampleDoesNotSpreadThroughTheField()
        {
            float[] field = Enumerable.Repeat(-5f, 25).ToArray();
            field[12] = float.NaN;

            float[] smooth = SegmentationLogitField.Smooth(field, 5, 5, 0.75);

            Assert.IsTrue(smooth.All(value => !float.IsNaN(value) && value < 0f));
        }

        /// <summary>
        /// One island of <paramref name="width"/> x <paramref name="height"/> samples, sample (0, 0)'s top-left corner at grid
        /// pixel (<paramref name="x"/>, <paramref name="y"/>), Y up; <paramref name="logit"/> takes (column, row), row 0 the top.
        /// </summary>
        internal static SegmentationServiceTypes.SegmentResult SegmentWithLogits(
            int width,
            int height,
            int scale,
            int x,
            int y,
            Func<int, int, float> logit)
        {
            byte[] half = new byte[2 * width * height];
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    ushort bits = SingleToHalf(logit(column, row));
                    int index = 2 * ((row * width) + column);
                    half[index] = (byte)(bits & 0xFF);
                    half[index + 1] = (byte)(bits >> 8);
                }
            }

            return new SegmentationServiceTypes.SegmentResult
            {
                Logits = ByteString.CopyFrom(half),
                LogitsSize = new IntSize { Width = width, Height = height },
                LogitsScale = scale,
                LogitsOrigin = new IntPoint { X = x, Y = y }
            };
        }

        /// <summary>Round-to-nearest binary16 for values in the range logits use.</summary>
        private static ushort SingleToHalf(float value)
        {
            if (value == 0f)
                return 0;

            int sign = value < 0 ? 0x8000 : 0;
            double magnitude = Math.Abs((double)value);
            int exponent = (int)Math.Floor(Math.Log(magnitude, 2));
            if (exponent < -14)
                return (ushort)(sign | (int)Math.Round(magnitude / Math.Pow(2, -24)));

            int mantissa = (int)Math.Round((magnitude / Math.Pow(2, exponent) - 1.0) * 1024);
            if (mantissa == 1024)
            {
                mantissa = 0;
                exponent++;
            }

            return (ushort)(sign | ((exponent + 15) << 10) | mantissa);
        }
    }
}

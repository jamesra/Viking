using Geometry;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotationTests.Tools;
using GeometryProto = Viking.gRPC.Geometry.V1;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// The client side of <c>SegmentLocation</c>: which volumes the server reads, the mosaic result redrawn on the volume grid
    /// (<see cref="SegmentationFieldResampler"/>), the half-precision encoding that redraw writes, and the reviewed outline.
    /// </summary>
    [TestClass]
    public class SegmentLocationClientTests
    {
        /// <summary>
        /// A field whose logit is a linear ramp of mosaic X: logit = (x - 100) / 10 at each sample centre. Bilinear interpolation
        /// reproduces a ramp exactly, so a resampled sample can be checked against the ramp at the mosaic point it maps to.
        /// </summary>
        private static SegmentationServiceTypes.SegmentResult Ramp(int originX, int originY, int width, int height, int scale)
        {
            byte[] half = new byte[width * height * 2];
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    double x = originX + ((column + 0.5) * scale);
                    ushort bits = SegmentationLogitField.SingleToHalf((float)((x - 100) / 10));
                    int i = (row * width) + column;
                    half[2 * i] = (byte)(bits & 0xFF);
                    half[(2 * i) + 1] = (byte)(bits >> 8);
                }
            }

            return new SegmentationServiceTypes.SegmentResult
            {
                Island = 1,
                Score = 0.9f,
                Logits = ByteString.CopyFrom(half),
                LogitsSize = new GeometryProto.IntSize { Width = width, Height = height },
                LogitsOrigin = new GeometryProto.IntPoint { X = originX, Y = originY },
                LogitsScale = scale
            };
        }

        private static float LogitAt(SegmentationServiceTypes.SegmentResult segment, int column, int row)
        {
            byte[] bytes = segment.Logits.ToByteArray();
            int i = (row * segment.LogitsSize.Width) + column;
            return SegmentationLogitField.HalfToSingle((ushort)(bytes[2 * i] | (bytes[(2 * i) + 1] << 8)));
        }

        [TestMethod]
        public void HalfPrecisionRoundTripsLogitsWithinItsPrecision()
        {
            foreach (float value in new[] { 0f, 1f, -1f, 0.5f, 3.14159f, -17.25f, 1e-6f, -1e-5f, 2048f, -30000f })
            {
                float back = SegmentationLogitField.HalfToSingle(SegmentationLogitField.SingleToHalf(value));
                Assert.AreEqual(value, back, Math.Max(1e-7, Math.Abs(value) / 1024), $"value {value}");
            }

            // Beyond the half range the value is clamped, never made infinite.
            Assert.AreEqual(65504f, SegmentationLogitField.HalfToSingle(SegmentationLogitField.SingleToHalf(1e9f)));
            Assert.AreEqual(-65504f, SegmentationLogitField.HalfToSingle(SegmentationLogitField.SingleToHalf(float.NegativeInfinity)));
            Assert.IsTrue(float.IsNaN(SegmentationLogitField.HalfToSingle(SegmentationLogitField.SingleToHalf(float.NaN))));
        }

        [TestMethod]
        public void OnlyVolumesTheServerReadsAreReadable()
        {
            SegmentationServiceTypes.ServerStatusResponse status = new();
            status.ReadableVolumes.Add(new SegmentationServiceTypes.ReadableVolume { AnnotationContextId = 3, Name = "RC2", Readable = true });
            status.ReadableVolumes.Add(new SegmentationServiceTypes.ReadableVolume { AnnotationContextId = 4, Name = "NM", Readable = false });

            CollectionAssert.AreEquivalent(new long[] { 3 }, SegmentationViewportSession.ReadableContextsOf(status).ToArray());
            Assert.AreEqual(0, SegmentationViewportSession.ReadableContextsOf(new SegmentationServiceTypes.ServerStatusResponse()).Count);
            Assert.AreEqual(0, SegmentationViewportSession.ReadableContextsOf(null).Count);
        }

        /// <summary>
        /// Under a scale-and-shift transform each volume sample holds the mosaic logit at the point it maps back to, and the
        /// volume footprint covers the mapped mosaic footprint.
        /// </summary>
        [TestMethod]
        public void ResampledSamplesHoldTheMosaicLogitWhereTheyMapBack()
        {
            ScaleTransform transform = new(2, new Vector2(50, -20));
            SegmentationServiceTypes.SegmentResult mosaic = Ramp(100, 199, 40, 30, 4);

            SegmentationServiceTypes.SegmentResult? volume = SegmentationFieldResampler.TryResample(mosaic, transform);

            Assert.IsNotNull(volume);
            Assert.AreEqual(4, (int)volume.LogitsScale);
            Assert.AreEqual(1u, volume.Island);
            // Mosaic footprint x 100..260, y 80..200 -> volume x 250..570, y 140..380.
            Assert.AreEqual(250, volume.LogitsOrigin.X);
            Assert.AreEqual(379, volume.LogitsOrigin.Y);
            Assert.AreEqual(80, volume.LogitsSize.Width);
            Assert.AreEqual(60, volume.LogitsSize.Height);

            for (int row = 1; row < volume.LogitsSize.Height - 1; row += 7)
            {
                for (int column = 1; column < volume.LogitsSize.Width - 1; column += 5)
                {
                    double volumeX = volume.LogitsOrigin.X + ((column + 0.5) * 4);
                    double mosaicX = transform.Inverse(new Vector2(volumeX, 0)).X;
                    // Clamped at the outer half sample, where the field holds its edge value.
                    double expectedX = Math.Max(102, Math.Min(258, mosaicX));
                    Assert.AreEqual((expectedX - 100) / 10, LogitAt(volume, column, row), 0.02, $"sample ({column}, {row})");
                }
            }
        }

        [TestMethod]
        public void SamplesOutsideTheMosaicFieldHoldItsLowestLogit()
        {
            // A rotation leaves the corners of the volume bounding box outside the mosaic footprint.
            AffineTransform transform = AffineTransform.RotateScaleShear();
            SegmentationServiceTypes.SegmentResult mosaic = Ramp(100, 199, 20, 20, 4);
            float lowest = Enumerable.Range(0, 20).Min(column => LogitAt(mosaic, column, 0));

            SegmentationServiceTypes.SegmentResult? volume = SegmentationFieldResampler.TryResample(mosaic, transform);

            Assert.IsNotNull(volume);
            Assert.AreEqual(lowest, LogitAt(volume, 0, 0), 1e-3);
        }

        [TestMethod]
        public void TheResponseKeepsItsOtherFieldsAndDropsIslandsWithoutLogits()
        {
            SegmentationServiceTypes.SegmentationResponse mosaic = new() { RequestId = 7, GrowthThreshold = 0.25f };
            mosaic.Segments.Add(Ramp(0, 99, 10, 10, 4));
            mosaic.Segments.Add(new SegmentationServiceTypes.SegmentResult { Island = 2 });

            SegmentationServiceTypes.SegmentationResponse volume = SegmentationFieldResampler.ToVolume(mosaic, new ScaleTransform(1, new Vector2(10, 10)));

            Assert.AreEqual(7ul, volume.RequestId);
            Assert.AreEqual(0.25f, volume.GrowthThreshold);
            Assert.AreEqual(1, volume.Segments.Count);
            Assert.AreEqual(10, volume.Segments[0].LogitsOrigin.X);
            Assert.AreEqual(2, mosaic.Segments.Count, "the mosaic response is not changed");
        }

        [TestMethod]
        public void TheReviewedOutlineIsMappedIntoViewerSpace()
        {
            SegmentationServiceTypes.SegmentationResponse response = new()
            {
                Reviewed = new SegmentationServiceTypes.ReviewedOutline
                {
                    LocationId = 5,
                    State = "approved",
                    Outline = new GeometryProto.Shape { Polygon = new GeometryProto.Polygon { Exterior = new GeometryProto.Ring() } }
                }
            };
            foreach ((double x, double y) in new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0) })
                response.Reviewed.Outline.Polygon.Exterior.Points.Add(new GeometryProto.Vector2 { X = x, Y = y });

            Polygon? mosaic = SegmentationViewportSession.ReviewedOutline(response, null);
            Polygon? volume = SegmentationViewportSession.ReviewedOutline(response, new ScaleTransform(2, new Vector2(100, 0)));

            Assert.IsNotNull(mosaic);
            Assert.AreEqual(100, mosaic.Area, 1e-9);
            Assert.IsNotNull(volume);
            Assert.AreEqual(400, volume.Area, 1e-9);
            Assert.AreEqual(100, volume.BoundingBox.Left, 1e-9);
            Assert.IsNull(SegmentationViewportSession.ReviewedOutline(new SegmentationServiceTypes.SegmentationResponse(), null));
        }
    }
}

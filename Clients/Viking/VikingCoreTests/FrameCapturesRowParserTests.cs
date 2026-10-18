using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.Forms;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="FrameCapturesForm.ParseFrameRow"/>, which turns one row of the Export Frames grid into a frame.
    /// The grid's new-row placeholder has every cell null; it must not become a frame at X=0, Y=0, Z=0.
    /// </summary>
    [TestClass]
    public class FrameCapturesRowParserTests
    {
        private const string File = @"C:\frames\f1.png";

        [TestMethod]
        public void BlankRow_IsSkipped()
        {
            Assert.IsNull(FrameCapturesForm.ParseFrameRow(new object?[6], true, File));
            Assert.IsNull(FrameCapturesForm.ParseFrameRow(new object?[] { "", " ", null, "", null, "" }, true, File));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void MissingRequiredCell_IsRejected(int blankColumn)
        {
            object?[] cells = { "1000", "2000", "12", null, null, null };
            cells[blankColumn] = null;
            Assert.ThrowsException<FormatException>(() => FrameCapturesForm.ParseFrameRow(cells, false, File));
        }

        [TestMethod]
        public void OptionalCells_Default()
        {
            FrameCapture frame = FrameCapturesForm.ParseFrameRow(new object?[] { "1000", "2000", "12", null, null, null }, true, File)!.Value;

            Assert.AreEqual(12.0, frame.Z);
            Assert.AreEqual(1.0, frame.downsample);
            Assert.IsTrue(frame.IncludeOverlay);
            Assert.AreEqual(File, frame.Filename);
            Assert.AreEqual(512.0, frame.Rect.Width);
            Assert.AreEqual(512.0, frame.Rect.Height);
            Assert.AreEqual(744.0, frame.Rect.Left);
            Assert.AreEqual(1744.0, frame.Rect.Bottom);
        }

        /// <summary>
        /// Pasting "1000\t2000\t12\t\t\t" from a spreadsheet stores "" (not null) in the trailing cells;
        /// those must default like null cells instead of failing to parse.
        /// </summary>
        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        public void BlankStringOptionalCells_Default(string blank)
        {
            FrameCapture frame = FrameCapturesForm.ParseFrameRow(new object?[] { "1000", "2000", "12", blank, blank, blank }, true, File)!.Value;

            Assert.AreEqual(1.0, frame.downsample);
            Assert.AreEqual(512.0, frame.Rect.Width);
            Assert.AreEqual(512.0, frame.Rect.Height);
            Assert.AreEqual(744.0, frame.Rect.Left);
            Assert.AreEqual(1744.0, frame.Rect.Bottom);
        }

        /// <summary>Each optional cell defaults on its own when only it is blank.</summary>
        [DataTestMethod]
        [DataRow(3, 512.0, 50.0, 4.0)]
        [DataRow(4, 100.0, 512.0, 4.0)]
        [DataRow(5, 100.0, 50.0, 1.0)]
        public void SingleBlankOptionalCell_Defaults(int blankColumn, double width, double height, double downsample)
        {
            object?[] cells = { "0", "0", "1", "100", "50", "4" };
            cells[blankColumn] = "";
            FrameCapture frame = FrameCapturesForm.ParseFrameRow(cells, false, File)!.Value;

            Assert.AreEqual(downsample, frame.downsample);
            Assert.AreEqual(width * downsample, frame.Rect.Width);
            Assert.AreEqual(height * downsample, frame.Rect.Height);
        }

        [TestMethod]
        public void ExplicitSizeAndDownsample_ScaleRegionCenteredOnPoint()
        {
            FrameCapture frame = FrameCapturesForm.ParseFrameRow(new object?[] { "-5000", "300", "7", "100", "50", "4" }, false, File)!.Value;

            Assert.AreEqual(4.0, frame.downsample);
            Assert.IsFalse(frame.IncludeOverlay);
            Assert.AreEqual(400.0, frame.Rect.Width);
            Assert.AreEqual(200.0, frame.Rect.Height);
            Assert.AreEqual(-5200.0, frame.Rect.Left);
            Assert.AreEqual(200.0, frame.Rect.Bottom);
        }

        /// <summary>
        /// For any point, size, and downsample, the region is size * downsample and centered on the point.
        /// Coordinates span real volume magnitudes (hundreds of thousands of pixels, negative allowed).
        /// </summary>
        [TestMethod]
        public void Region_IsCenteredOnPoint_Property()
        {
            Prop.ForAll(
                Gen.Choose(-500000, 500000).Two().ToArbitrary(),
                Gen.Choose(1, 8192).Two().ToArbitrary(),
                Gen.Elements(0.25, 1.0, 2.0, 16.0, 64.0).ToArbitrary(),
                (xy, size, ds) =>
                {
                    FrameCapture frame = FrameCapturesForm.ParseFrameRow(
                        new object?[] { xy.Item1, xy.Item2, 3, size.Item1, size.Item2, ds }, false, File)!.Value;
                    return Math.Abs(frame.Rect.Center.X - xy.Item1) < 1e-6
                        && Math.Abs(frame.Rect.Center.Y - xy.Item2) < 1e-6
                        && frame.Rect.Width == size.Item1 * ds
                        && frame.Rect.Height == size.Item2 * ds;
                }).QuickCheckThrowOnFailure();
        }
    }
}

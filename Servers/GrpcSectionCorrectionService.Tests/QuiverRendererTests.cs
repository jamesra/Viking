using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;
using Path = System.IO.Path;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// The preview contact sheet (index.png) is drawn with SkiaSharp; these render one on this machine's native build.
    /// </summary>
    [TestClass]
    public class QuiverRendererTests
    {
        string _directory = string.Empty;

        [TestInitialize]
        public void Create()
        {
            _directory = Path.Combine(Path.GetTempPath(), "quiver-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Remove() => Directory.Delete(_directory, true);

        static SectionVectorField Field(long z)
        {
            List<LatticeNode> nodes = [];
            for (int gx = 0; gx < 6; gx++)
            {
                for (int gy = 0; gy < 6; gy++)
                    nodes.Add(new LatticeNode(gx, gy, 300 + gx * 100, -200 + gy * 50, 3));
            }

            return new SectionVectorField(z, 2000, 8000, nodes);
        }

        [TestMethod]
        public void ContactSheetIsAPngWithOneThumbnailPerSection()
        {
            QuiverRenderer.WriteSectionQuivers(_directory, [Field(5), Field(6)], 0, new Dictionary<long, Geometry.Rectangle>());

            Assert.IsTrue(File.Exists(Path.Combine(_directory, "5.quiver.svg")));
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "6.quiver.svg")));
            using SKBitmap sheet = SKBitmap.Decode(Path.Combine(_directory, "index.png"));
            Assert.IsNotNull(sheet, "index.png does not decode");
            Assert.AreEqual(1024, sheet.Width, "two 512 px thumbnails side by side");
            Assert.AreEqual(512, sheet.Height);

            bool arrowDrawn = false;
            for (int y = 0; y < sheet.Height && !arrowDrawn; y++)
            {
                for (int x = 0; x < 512 && !arrowDrawn; x++)
                {
                    SKColor c = sheet.GetPixel(x, y);
                    arrowDrawn = c.Red > 40 && (c.Green > 40 || c.Blue > 40);
                }
            }

            Assert.IsTrue(arrowDrawn, "the first thumbnail has no coloured arrows");
        }

        [TestMethod]
        public void MissingContactSheetIsWritten()
        {
            QuiverRenderer.WriteSectionQuivers(_directory, [Field(5)], 0, new Dictionary<long, Geometry.Rectangle>());
            File.Delete(Path.Combine(_directory, "index.png"));

            int written = QuiverRenderer.WriteMissingQuivers(_directory, [Field(5)], 0, new Dictionary<long, Geometry.Rectangle>());

            Assert.AreEqual(1, written, "only the contact sheet was missing");
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "index.png")));
        }
    }
}

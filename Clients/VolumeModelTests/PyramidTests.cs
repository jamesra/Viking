using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="Pyramid"/>: level bookkeeping (<see cref="Pyramid.AddLevel"/>,
    /// <see cref="Pyramid.GetLevels"/>, <see cref="Pyramid.PathForLevel"/>) and
    /// <see cref="Pyramid.CreateFromElement"/> parsing of the VikingXML <c>Pyramid</c> element.
    /// </summary>
    [TestClass]
    public class PyramidTests
    {
        private static Pyramid Create(string xml) => Pyramid.CreateFromElement(XElement.Parse(xml), null);

        [TestMethod]
        public void CreateFromElement_NoLevelElements_ReturnsNull()
        {
            Assert.IsNull(Create("<Pyramid name=\"p\" path=\"x\"><Scale UnitsOfMeasure=\"nm\" UnitsPerPixel=\"2\" /></Pyramid>"));
        }

        [TestMethod]
        public void CreateFromElement_NoLevelElements_DoesNotRequireNameOrPath()
        {
            Assert.IsNull(Create("<Pyramid />"));
        }

        [TestMethod]
        public void CreateFromElement_ReadsNameAndPath_CaseInsensitive()
        {
            Pyramid p = Create("<Pyramid NAME=\"Mosaic\" Path=\"dir\"><Level Downsample=\"1\" path=\"a\" /></Pyramid>");
            Assert.AreEqual("Mosaic", p.Name);
            Assert.AreEqual("dir", p.Path);
        }

        [TestMethod]
        public void CreateFromElement_LevelAttributes_CaseInsensitive()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level DOWNSAMPLE=\"4\" PATH=\"lvl4\" /></Pyramid>");
            Assert.AreEqual("lvl4", p.PathForLevel(4));
        }

        [TestMethod]
        public void CreateFromElement_NoScaleElement_XYScaleIsNull()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level Downsample=\"1\" path=\"a\" /></Pyramid>");
            Assert.IsNull(p.XYScale);
        }

        [TestMethod]
        public void CreateFromElement_ScaleElement_ParsesUnits()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Scale UnitsOfMeasure=\"nm\" UnitsPerPixel=\"2.5\" /><Level Downsample=\"1\" path=\"a\" /></Pyramid>");
            Assert.IsNotNull(p.XYScale);
            Assert.AreEqual(2.5, p.XYScale.Value, 0.0);
            Assert.AreEqual("nm", p.XYScale.Units);
        }

        [TestMethod]
        public void CreateFromElement_MissingLevelDownsample_Throws()
        {
            Assert.ThrowsException<XMLMissingDataException>(() =>
                Create("<Pyramid name=\"n\" path=\"p\"><Level path=\"a\" /></Pyramid>"));
        }

        [TestMethod]
        public void CreateFromElement_DuplicateDownsample_KeepsFirstPath()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level Downsample=\"2\" path=\"first\" /><Level Downsample=\"2\" path=\"second\" /></Pyramid>");
            CollectionAssert.AreEqual(new[] { 2 }, p.GetLevels().ToArray());
            Assert.AreEqual("first", p.PathForLevel(2));
        }

        [TestMethod]
        public void GetLevels_AreAscendingRegardlessOfDocumentOrder()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level Downsample=\"16\" path=\"c\" /><Level Downsample=\"1\" path=\"a\" /><Level Downsample=\"4\" path=\"b\" /></Pyramid>");
            CollectionAssert.AreEqual(new[] { 1, 4, 16 }, p.GetLevels().ToArray());
        }

        [TestMethod]
        public void AddLevel_NewLevel_ReturnsTrueAndIsStored()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level Downsample=\"1\" path=\"a\" /></Pyramid>");
            Assert.IsTrue(p.AddLevel(8, "eight"));
            Assert.AreEqual("eight", p.PathForLevel(8));
            CollectionAssert.AreEqual(new[] { 1, 8 }, p.GetLevels().ToArray());
        }

        [TestMethod]
        public void AddLevel_DuplicateLevel_ReturnsFalseAndKeepsOriginalPath()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level Downsample=\"1\" path=\"a\" /></Pyramid>");
            Assert.IsFalse(p.AddLevel(1, "other"));
            Assert.AreEqual("a", p.PathForLevel(1));
            Assert.AreEqual(1, p.GetLevels().Count);
        }

        [TestMethod]
        public void PathForLevel_UnknownLevel_ReturnsNull()
        {
            Pyramid p = Create("<Pyramid name=\"n\" path=\"p\"><Level Downsample=\"1\" path=\"a\" /></Pyramid>");
            Assert.IsNull(p.PathForLevel(2));
            Assert.IsNull(p.PathForLevel(0));
            Assert.IsNull(p.PathForLevel(-1));
        }
    }
}

using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="TileGridMappingBase.GridTileFormatStringFromPythonString"/> used when
    /// <see cref="Pyramid"/> and <see cref="TileGridMapping"/> read VikingXML <c>CoordFormat</c> attributes
    /// from Python-generated volume metadata (digit-first formats become C# letter-first).
    /// </summary>
    [TestClass]
    public class GridTileFormatFromPythonStringTests
    {
        [TestMethod]
        public void GridTileFormatStringFromPythonString_PythonDigitFirst_SwapsLetterToFront()
        {
            Assert.AreEqual("d03", TileGridMappingBase.GridTileFormatStringFromPythonString("03d"));
            Assert.AreEqual("D000", TileGridMappingBase.GridTileFormatStringFromPythonString("000D"));
        }

        [TestMethod]
        public void GridTileFormatStringFromPythonString_CSharpLetterFirst_Unchanged()
        {
            Assert.AreEqual("d03", TileGridMappingBase.GridTileFormatStringFromPythonString("d03"));
            Assert.AreEqual("D000", TileGridMappingBase.GridTileFormatStringFromPythonString("D000"));
        }

        [TestMethod]
        public void GridTileFormatStringFromPythonString_DigitOnlyOrNoTrailingLetter_Unchanged()
        {
            Assert.AreEqual("000", TileGridMappingBase.GridTileFormatStringFromPythonString("000"));
            Assert.AreEqual("03", TileGridMappingBase.GridTileFormatStringFromPythonString("03"));
        }

        [TestMethod]
        public void GridTileFormatStringFromPythonString_IdempotentOnCSharpFormats()
        {
            string[] csharpFormats = { "d03", "D000", "X0000", "000", "03" };
            foreach (string format in csharpFormats)
            {
                string once = TileGridMappingBase.GridTileFormatStringFromPythonString(format);
                string twice = TileGridMappingBase.GridTileFormatStringFromPythonString(once);
                Assert.AreEqual(once, twice, format);
            }
        }

        [TestMethod]
        public void GridTileFormatStringFromPythonString_MatchesReferenceOnGeneratedInputs()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Elements('d', 'D', 'X', 'x')),
                Arb.From(Gen.Elements("0", "00", "000", "03", "004")),
                (letter, prefix) =>
                {
                    string pythonStyle = prefix + letter;
                    string expected = letter + prefix;
                    return TileGridMappingBase.GridTileFormatStringFromPythonString(pythonStyle) == expected;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        [TestMethod]
        public void GridTileFormatStringFromPythonString_LetterFirstFormats_AreFixedPoints()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Elements('d', 'D', 'X')),
                Arb.From(Gen.Elements("0", "00", "03", "004")),
                (letter, digits) =>
                {
                    string format = letter + digits;
                    return TileGridMappingBase.GridTileFormatStringFromPythonString(format) == format;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }
    }
}

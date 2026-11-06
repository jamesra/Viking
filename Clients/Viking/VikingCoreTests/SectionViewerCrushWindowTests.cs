using System.Runtime.Serialization;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI;
using Viking.UI.Controls;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// <see cref="SectionViewerControl.CrushDisplayWindowFor"/> picks the shader's display window for a channel: the whole
    /// range for anything that is not a decodable crushed channel, otherwise the channel's display range narrowed by the
    /// user's contrast. <see cref="State.CrushContrast"/> is process-wide, so every test restores it.
    /// </summary>
    [TestClass]
    public class SectionViewerCrushWindowTests
    {
        private const double Tolerance = 1e-6;

        private double _savedContrast;

        [TestInitialize]
        public void SaveContrast() => _savedContrast = State.CrushContrast;

        [TestCleanup]
        public void RestoreContrast() => State.CrushContrast = _savedContrast;

        /// <summary>
        /// A section with no tiles. The volume is never read: <see cref="Section"/> only formats its host into a path.
        /// </summary>
        private static Section SectionWith(params (string Channel, CrushInfo Crush)[] crushed)
        {
            Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
            Section section = new(volume, "0007", XElement.Parse("<Section number=\"7\" />"));
            foreach ((string channel, CrushInfo crush) in crushed)
                section.CrushedChannels[channel] = crush;

            return section;
        }

        /// <summary>A crushed channel whose Nornir display range is 51..102 codes, which is 0.2..0.4 of an 8-bit PNG.</summary>
        private static CrushInfo Supported(int bits = 8) => new(step: 10, offset: 0, bits: bits, displayRange: (51.0, 102.0), codeHistogram: null);

        private static void AssertWindow(double expectedMin, double expectedMax, (float Min, float Max) actual)
        {
            Assert.AreEqual(expectedMin, actual.Min, Tolerance, "window minimum");
            Assert.AreEqual(expectedMax, actual.Max, Tolerance, "window maximum");
        }

        [TestMethod]
        public void AChannelThatIsNotCrushedUsesTheWholeRange()
        {
            Section section = SectionWith(("Crushed", Supported()));

            AssertWindow(0, 1, SectionViewerControl.CrushDisplayWindowFor(section, "Raw"));
            AssertWindow(0, 1, SectionViewerControl.CrushDisplayWindowFor(SectionWith(), "Crushed"));
        }

        [TestMethod]
        public void AnEmptyChannelNameIsNotACrushedChannel()
        {
            Section section = SectionWith(("Crushed", Supported()));

            AssertWindow(0, 1, SectionViewerControl.CrushDisplayWindowFor(section, ""));
        }

        [TestMethod]
        public void CrushedCodesWiderThanEightBitsUseTheWholeRange()
        {
            State.CrushContrast = 4.0;
            Section section = SectionWith(("Wide", Supported(bits: 9)), ("Wider", Supported(bits: 16)));

            AssertWindow(0, 1, SectionViewerControl.CrushDisplayWindowFor(section, "Wide"));
            AssertWindow(0, 1, SectionViewerControl.CrushDisplayWindowFor(section, "Wider"));
        }

        [TestMethod]
        public void ASupportedChannelAtUnitContrastShowsItsDefaultWindow()
        {
            State.CrushContrast = 1.0;
            CrushInfo crush = Supported();
            (float expectedMin, float expectedMax) = crush.DefaultWindow();
            Section section = SectionWith(("Crushed", crush));

            AssertWindow(0.2, 0.4, SectionViewerControl.CrushDisplayWindowFor(section, "Crushed"));
            AssertWindow(expectedMin, expectedMax, SectionViewerControl.CrushDisplayWindowFor(section, "Crushed"));
        }

        [TestMethod]
        public void ContrastNarrowsTheWindowFromItsMinimum()
        {
            Section section = SectionWith(("Crushed", Supported()));

            foreach ((double contrast, double expectedMax) in new[] { (2.0, 0.3), (4.0, 0.25), (0.5, 0.6) })
            {
                State.CrushContrast = contrast;
                AssertWindow(0.2, expectedMax, SectionViewerControl.CrushDisplayWindowFor(section, "Crushed"));
            }
        }

        [TestMethod]
        public void ContrastIsClampedToTheLimitsTheKeysAlsoEnforce()
        {
            Section section = SectionWith(("Crushed", Supported()));

            State.CrushContrast = State.MaxCrushContrast;
            (float Min, float Max) atMax = SectionViewerControl.CrushDisplayWindowFor(section, "Crushed");
            State.CrushContrast = State.MinCrushContrast;
            (float Min, float Max) atMin = SectionViewerControl.CrushDisplayWindowFor(section, "Crushed");

            State.CrushContrast = State.MaxCrushContrast * 1000;
            Assert.AreEqual(atMax, SectionViewerControl.CrushDisplayWindowFor(section, "Crushed"), "above the maximum");
            State.CrushContrast = State.MinCrushContrast / 1000;
            Assert.AreEqual(atMin, SectionViewerControl.CrushDisplayWindowFor(section, "Crushed"), "below the minimum");

            AssertWindow(0.2, 0.2 + 0.2 / State.MaxCrushContrast, atMax);
            AssertWindow(0.2, 0.2 + 0.2 / State.MinCrushContrast, atMin);
        }

        [TestMethod]
        public void TheChannelLookupIgnoresCase()
        {
            State.CrushContrast = 2.0;
            Section section = SectionWith(("Crushed", Supported()));

            AssertWindow(0.2, 0.3, SectionViewerControl.CrushDisplayWindowFor(section, "CRUSHED"));
            AssertWindow(0.2, 0.3, SectionViewerControl.CrushDisplayWindowFor(section, "crushed"));
        }

        [TestMethod]
        public void EachChannelUsesItsOwnDisplayRange()
        {
            State.CrushContrast = 1.0;
            CrushInfo other = new(step: 10, offset: 0, bits: 8, displayRange: (0.0, 25.5), codeHistogram: null);
            Section section = SectionWith(("A", Supported()), ("B", other));

            AssertWindow(0.2, 0.4, SectionViewerControl.CrushDisplayWindowFor(section, "A"));
            AssertWindow(0.0, 0.1, SectionViewerControl.CrushDisplayWindowFor(section, "B"));
        }
    }
}

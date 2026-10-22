using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Models;
using Viking.VolumeModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the channel color round trip of <see cref="SectionReferenceSettings"/>: a channel saved to
    /// section-references.json must load back with the same color, including alpha, which the section viewer
    /// uses as the channel's blend weight.
    /// </summary>
    [TestClass]
    public class SectionReferenceSettingsTests
    {
        private static ChannelInfo Channel(byte a, byte r, byte g, byte b) => new()
        {
            ChannelName = "ch",
            SectionSource = ChannelInfo.SectionInfo.FIXED,
            FixedSectionNumber = 7,
            Color = new Geometry.Graphics.Color(r, g, b, a),
        };

        [TestMethod]
        public void ToDto_WritesAlphaFirstHex()
        {
            Assert.AreEqual("#FF123456", SectionReferenceSettings.ToDto(Channel(255, 0x12, 0x34, 0x56)).ColorHex);
        }

        [TestMethod]
        [DataRow((byte)255)]
        [DataRow((byte)128)]
        [DataRow((byte)127)]
        [DataRow((byte)1)]
        [DataRow((byte)0)]
        public void RoundTrip_KeepsColorAndAlpha(byte alpha)
        {
            ChannelInfo restored = SectionReferenceSettings.FromDto(SectionReferenceSettings.ToDto(Channel(alpha, 0x12, 0x34, 0x56)));

            Assert.AreEqual(alpha, restored.Color.A);
            Assert.AreEqual(0x12, restored.Color.R);
            Assert.AreEqual(0x34, restored.Color.G);
            Assert.AreEqual(0x56, restored.Color.B);
            Assert.AreEqual("ch", restored.ChannelName);
            Assert.AreEqual(ChannelInfo.SectionInfo.FIXED, restored.SectionSource);
            Assert.AreEqual(7, restored.FixedSectionNumber);
        }

        [TestMethod]
        public void FromDto_BlankOrBadColor_FallsBackToOpaqueWhite()
        {
            foreach (string hex in new[] { null, "  ", "#ZZZZZZZZ" })
            {
                Geometry.Graphics.Color color = SectionReferenceSettings.FromDto(new ChannelInfoDto { ColorHex = hex }).Color;
                Assert.AreEqual((byte)255, color.A);
                Assert.AreEqual((byte)255, color.R);
                Assert.AreEqual((byte)255, color.G);
                Assert.AreEqual((byte)255, color.B);
            }
        }

        /// <summary>Runs <paramref name="body"/> with an empty scratch directory standing in for a volume's local cache directory.</summary>
        private static void WithVolumeDir(System.Action<string> body)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SectionRefTests_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                body(dir);
            }
            finally
            {
                if (System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void SaveThenLoad_RoundTripsReferencesAndChannels()
        {
            WithVolumeDir(dir =>
            {
                var saved = new System.Collections.Generic.Dictionary<int, SectionReferences>
                {
                    [12] = new SectionReferences { ReferenceAbove = 20, ReferenceBelow = null, Channels = [SectionReferenceSettings.ToDto(Channel(200, 1, 2, 3))] },
                    [-3] = new SectionReferences { ReferenceAbove = null, ReferenceBelow = 9 },
                };

                SectionReferenceSettings.SaveForVolume(dir, saved);
                var loaded = SectionReferenceSettings.LoadForVolume(dir);

                Assert.AreEqual(2, loaded.Count);
                Assert.AreEqual(20, loaded[12].ReferenceAbove);
                Assert.IsNull(loaded[12].ReferenceBelow);
                Assert.AreEqual("#C8010203", loaded[12].Channels[0].ColorHex);
                Assert.IsNull(loaded[-3].ReferenceAbove);
                Assert.AreEqual(9, loaded[-3].ReferenceBelow);
                Assert.IsNull(loaded[-3].Channels);
            });
        }

        [TestMethod]
        public void SaveForVolume_EmptySettings_DeletesTheFile()
        {
            WithVolumeDir(dir =>
            {
                SectionReferenceSettings.SaveForVolume(dir, new() { [1] = new SectionReferences { ReferenceAbove = 5 } });
                Assert.AreEqual(1, SectionReferenceSettings.LoadForVolume(dir).Count);

                SectionReferenceSettings.SaveForVolume(dir, new());

                Assert.AreEqual(0, SectionReferenceSettings.LoadForVolume(dir).Count);
                Assert.AreEqual(0, System.IO.Directory.GetFiles(dir).Length);
            });
        }

        [TestMethod]
        public void LoadForVolume_MissingDirectoryBlankPathOrCorruptFile_ReturnsEmpty()
        {
            Assert.AreEqual(0, SectionReferenceSettings.LoadForVolume(null).Count);
            Assert.AreEqual(0, SectionReferenceSettings.LoadForVolume("  ").Count);

            WithVolumeDir(dir =>
            {
                Assert.AreEqual(0, SectionReferenceSettings.LoadForVolume(dir).Count);

                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "section-references.json"), "{ not json");
                Assert.AreEqual(0, SectionReferenceSettings.LoadForVolume(dir).Count);
            });
        }

        [TestMethod]
        public void IsNonDefault_DefaultsAreOneAboveAndOneBelow()
        {
            Assert.AreEqual((11, 9), SectionReferenceSettings.GetDefaultReferences(10));
            Assert.IsFalse(SectionReferenceSettings.IsNonDefault(10, 11, 9));
            Assert.IsTrue(SectionReferenceSettings.IsNonDefault(10, 12, 9));
            Assert.IsTrue(SectionReferenceSettings.IsNonDefault(10, 11, 8));
            Assert.IsTrue(SectionReferenceSettings.IsNonDefault(10, null, 9));
            Assert.IsTrue(SectionReferenceSettings.IsNonDefault(10, 11, null));
        }
    }
}

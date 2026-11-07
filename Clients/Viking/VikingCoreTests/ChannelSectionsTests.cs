using System.Collections.Generic;
using System.Linq;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="ChannelSections.SkipMissing"/>, the rule the section viewer's texture preload, Export Tiles check
    /// and capture wait use to pair channels with sections. Before it, those loops read <c>section.Number</c> on a null
    /// section and threw for an ABOVE channel on the last section or a BELOW channel on the first.
    /// </summary>
    [TestClass]
    public class ChannelSectionsTests
    {
        /// <summary>Stand-in for <see cref="Section"/>, which needs a loaded <see cref="Volume"/> to construct.</summary>
        private sealed class FakeSection(int number)
        {
            public int Number { get; } = number;
        }

        /// <summary>
        /// Regression: on the first section of a volume a channel set of selected plus ABOVE plus BELOW has no BELOW
        /// section; only the two channels with a section come back, in order, each with its own section.
        /// </summary>
        [TestMethod]
        public void BelowChannelOnFirstSectionIsSkipped()
        {
            ChannelInfo selected = new() { SectionSource = ChannelInfo.SectionInfo.SELECTED };
            ChannelInfo above = new() { SectionSource = ChannelInfo.SectionInfo.ABOVE };
            ChannelInfo below = new() { SectionSource = ChannelInfo.SectionInfo.BELOW };
            FakeSection first = new(1);
            FakeSection second = new(2);
            Dictionary<ChannelInfo, FakeSection?> sections = new() { [selected] = first, [above] = second, [below] = null };

            var pairs = ChannelSections.SkipMissing([selected, above, below], c => sections[c]).ToList();

            CollectionAssert.AreEqual(new[] { selected, above }, pairs.Select(p => p.Channel).ToArray());
            CollectionAssert.AreEqual(new[] { first, second }, pairs.Select(p => p.Section).ToArray());
        }

        /// <summary>No channels, or no channel with a section, yields nothing rather than throwing.</summary>
        [TestMethod]
        public void EmptyOrAllMissingYieldsNothing()
        {
            Assert.AreEqual(0, ChannelSections.SkipMissing<FakeSection>([], _ => new FakeSection(1)).Count());
            ChannelInfo fixedMissing = new() { SectionSource = ChannelInfo.SectionInfo.FIXED, FixedSectionNumber = 99 };
            Assert.AreEqual(0, ChannelSections.SkipMissing<FakeSection>([fixedMissing, fixedMissing], _ => null).Count());
        }

        /// <summary>
        /// For any channel list where any channels lack a section (repeats included), the result is exactly the
        /// channels that have one, in their original order, each paired with the section looked up for it, and the
        /// lookup runs once per channel.
        /// </summary>
        [TestMethod]
        public void ResultIsChannelsWithSectionsInOrder()
        {
            Gen<bool[]> hasSectionGen = Gen.ArrayOf(Arb.Default.Bool().Generator);
            Prop.ForAll(Arb.From(hasSectionGen), (bool[] hasSection) =>
            {
                ChannelInfo[] channels = hasSection.Select((_, i) => new ChannelInfo { FixedSectionNumber = i }).ToArray();
                if (channels.Length > 1)
                    channels[channels.Length - 1] = channels[0];

                int lookups = 0;
                FakeSection? SectionFor(ChannelInfo c)
                {
                    lookups++;
                    int i = c.FixedSectionNumber!.Value;
                    return hasSection[i] ? new FakeSection(i) : null;
                }

                var pairs = ChannelSections.SkipMissing(channels, SectionFor).ToList();
                ChannelInfo[] expected = channels.Where(c => hasSection[c.FixedSectionNumber!.Value]).ToArray();

                return pairs.Select(p => p.Channel).SequenceEqual(expected)
                    && pairs.All(p => p.Section.Number == p.Channel.FixedSectionNumber)
                    && lookups == channels.Length;
            }).QuickCheckThrowOnFailure();
        }

        /// <summary>The lookup runs as the result is enumerated, so callers that await between channels see each
        /// channel's section at the time they reach it, as the loops did before.</summary>
        [TestMethod]
        public void LookupIsLazy()
        {
            int lookups = 0;
            IEnumerable<(ChannelInfo, FakeSection)> pairs = ChannelSections.SkipMissing(
                [new ChannelInfo(), new ChannelInfo()], _ => { lookups++; return new FakeSection(lookups); });

            Assert.AreEqual(0, lookups);
            using var e = pairs.GetEnumerator();
            Assert.IsTrue(e.MoveNext());
            Assert.AreEqual(1, lookups);
        }
    }
}

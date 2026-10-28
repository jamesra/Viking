using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Reading a volume's damaged sections from the nornir files next to its VikingXML, and the builder rebuilding a set
    /// when that list changes.
    /// </summary>
    [TestClass]
    public class DamagedSectionListTests
    {
        const string Root = """
            <Volume CreationDate="2013-03-07 22:06:06" Name="RC2" Path="Y:\Volumes\RC2">
              <Block_Link CreationDate="2013-03-07 22:06:06" Name="TEM" Path="TEM" Version="1.0" /></Volume>
            """;

        const string Block = """
            <Block Name="TEM">
              <StosGroup_Link Name="SliceToVolume1" Path="SliceToVolume1" />
              <NonStosSectionNumbers CreationDate="2019-04-04 16:42:08" Version="1.0">817,5,792, 11,
            792</NonStosSectionNumbers>
            </Block>
            """;

        static DamagedSectionList.FetchText Files(Dictionary<string, string> files) =>
            (url, _) => Task.FromResult(files.TryGetValue(url, out string text) ? text : null);

        [TestMethod]
        public async Task ReadsTheBlockList_FromTheFolderOfTheVikingXml()
        {
            SortedSet<int> damaged = await DamagedSectionList.LoadAsync(
                "http://storage/RC2/SliceToVolume.VikingXML", null, CancellationToken.None,
                Files(new() { ["http://storage/RC2/VolumeData.xml"] = Root, ["http://storage/RC2/TEM/VolumeData.xml"] = Block }));

            CollectionAssert.AreEqual(new[] { 5, 11, 792, 817 }, damaged.ToArray(), "unsorted with a duplicate and a line break, as written by hand");
        }

        [TestMethod]
        public async Task ABlockWithoutAList_HasNoDamagedSections()
        {
            SortedSet<int> damaged = await DamagedSectionList.LoadAsync(
                "http://storage/RC1/VolumeV2.VikingXML", null, CancellationToken.None,
                Files(new() { ["http://storage/RC1/VolumeData.xml"] = Root, ["http://storage/RC1/TEM/VolumeData.xml"] = "<Block Name=\"TEM\" />" }));

            Assert.IsNotNull(damaged);
            Assert.AreEqual(0, damaged.Count);
        }

        [TestMethod]
        public async Task NoVolumeFilesBesideTheVikingXml_TriesTheFolderNamedForTheVolume()
        {
            // RC1: the VikingXML is under RABBIT/, the nornir files under RC1/.
            SortedSet<int> damaged = await DamagedSectionList.LoadAsync(
                "http://rogue1/RABBIT/rogue1.vikingxml", null, CancellationToken.None,
                Files(new() { ["http://rogue1/RC1/VolumeData.xml"] = Root, ["http://rogue1/RC1/TEM/VolumeData.xml"] = Block }),
                volumeName: "RC1");

            CollectionAssert.AreEqual(new[] { 5, 11, 792, 817 }, damaged?.ToArray());
        }

        [TestMethod]
        public async Task MissingOrBrokenVolumeFiles_AreUnknownNotEmpty()
        {
            Assert.IsNull(await DamagedSectionList.LoadAsync("http://storage/X/v.VikingXML", null, CancellationToken.None, Files([])));
            Assert.IsNull(await DamagedSectionList.LoadAsync("http://storage/X/v.VikingXML", null, CancellationToken.None,
                Files(new() { ["http://storage/X/VolumeData.xml"] = "<Volume" })));
            Assert.IsNull(await DamagedSectionList.LoadAsync("http://storage/X/v.VikingXML", null, CancellationToken.None,
                Files(new() { ["http://storage/X/VolumeData.xml"] = Root })), "a block file that is missing");
        }

        static CorrectionManifestDto Manifest(List<int> damaged) => new()
        {
            BuilderVersion = CorrectionPublisher.BuilderVersion,
            SectionFingerprints = [],
            Provenance = new CorrectionProvenanceDto
            {
                Method = "joint-second-difference-v1",
                MinLocations = 3,
                PitchNm = new JointRegistrationOptions().PitchNm,
                KernelRadiusNm = new JointRegistrationOptions().KernelRadiusNm,
                DamagedSections = damaged
            }
        };

        [TestMethod]
        public void AChangedDamagedList_RebuildsTheSet_AnUnreadableOneKeepsIt()
        {
            JointRegistrationOptions joint = new();
            Assert.IsNull(CorrectionPublisher.RebuildReason(Manifest([792, 817]), 3, joint, [817, 792]), "same sections in another order");
            Assert.AreEqual("damaged sections changed", CorrectionPublisher.RebuildReason(Manifest([792]), 3, joint, [792, 817]));
            Assert.AreEqual("damaged sections changed", CorrectionPublisher.RebuildReason(Manifest(null), 3, joint, [792]),
                "a set built before the list was known");
            Assert.IsNull(CorrectionPublisher.RebuildReason(Manifest([792]), 3, joint, null), "the list could not be read this time");
        }
    }
}

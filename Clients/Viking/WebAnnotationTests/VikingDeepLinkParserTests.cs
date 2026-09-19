using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace WebAnnotationTests
{
    [TestClass]
    public class VikingDeepLinkParserTests
    {
        [TestMethod]
        public void Parse_LocationId_AndVolumeUrl()
        {
            const string url = "viking://open?code=abc&volume=https://example.com/RC1/&location=6743";
            Assert.IsTrue(VikingDeepLinkParser.TryParse(url, out VikingDeepLink link));
            Assert.AreEqual("abc", link.Code);
            Assert.AreEqual("https://example.com/RC1/", link.VolumeUrl);
            Assert.IsTrue(string.IsNullOrEmpty(link.VolumeName));
            Assert.AreEqual("6743", link.Place["Location"]);
        }

        [TestMethod]
        public void Parse_VolumeNameParameter_AndBareVolumeAsName()
        {
            Assert.IsTrue(VikingDeepLinkParser.TryParse(
                "viking://open?volumeName=RC1&location=6743", out VikingDeepLink named));
            Assert.AreEqual("RC1", named.VolumeName);
            Assert.IsTrue(string.IsNullOrEmpty(named.VolumeUrl));

            Assert.IsTrue(VikingDeepLinkParser.TryParse(
                "viking://open?volume=RC1&location=6743", out VikingDeepLink bare));
            Assert.AreEqual("RC1", bare.VolumeName);
            Assert.IsTrue(string.IsNullOrEmpty(bare.VolumeUrl));
        }

        [TestMethod]
        public void TryFindOpenUrl_ReassemblesSplitAmpersandArgs()
        {
            string[] args =
            [
                "viking://open?code=abc",
                "volume=https://example.com/RC1/",
                "location=6743"
            ];

            Assert.IsTrue(VikingDeepLinkParser.TryFindOpenUrl(args, out string assembled));
            Assert.IsTrue(VikingDeepLinkParser.TryParse(assembled, out VikingDeepLink link));
            Assert.AreEqual("abc", link.Code);
            Assert.AreEqual("https://example.com/RC1/", link.VolumeUrl);
            Assert.AreEqual("6743", link.Place["Location"]);
        }

        [TestMethod]
        public void VolumeTargetsMatch_UrlOrName()
        {
            const string urlA = "https://example.com/RC1";
            const string urlB = "https://example.com/RC1/volume.vikingxml";

            Assert.IsTrue(VikingDeepLinkParser.VolumeTargetsMatch(urlA, null, urlB, "RC1"));
            Assert.IsTrue(VikingDeepLinkParser.VolumeTargetsMatch(null, "RC1", urlB, "RC1"));
            Assert.IsFalse(VikingDeepLinkParser.VolumeTargetsMatch(urlA, "RC1", "https://example.com/RC2", "RC2"));
        }

        [TestMethod]
        public void MergePlace_DoesNotOverwriteExistingLocation()
        {
            var target = new NameValueCollection { ["Location"] = "1" };
            var source = new NameValueCollection { ["Location"] = "2", ["X"] = "10" };
            VikingDeepLinkParser.MergePlace(target, source);
            Assert.AreEqual("1", target["Location"]);
            Assert.AreEqual("10", target["X"]);
        }

        [TestMethod]
        public void BuildActivationUrl_IncludesVolumeNameAndLocation()
        {
            var link = new VikingDeepLink
            {
                VolumeUrl = "https://example.com/RC1/volume.vikingxml",
                VolumeName = "RC1",
                Place = new NameValueCollection { ["Location"] = "6743" }
            };

            string url = VikingDeepLinkParser.BuildActivationUrl(link);
            Assert.IsTrue(VikingDeepLinkParser.TryParse(url, out VikingDeepLink parsed));
            Assert.IsTrue(string.IsNullOrEmpty(parsed.Code));
            Assert.AreEqual("RC1", parsed.VolumeName);
            Assert.AreEqual("6743", parsed.Place["Location"]);
            Assert.IsTrue(VikingDeepLinkParser.VolumeUrlsEqual(link.VolumeUrl, parsed.VolumeUrl));
        }

        [TestMethod]
        public void ParsePlaceArguments_LocationWinsOverCoordinates()
        {
            var query = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = "6743",
                ["x"] = "1",
                ["y"] = "2",
                ["z"] = "3"
            };

            NameValueCollection place = VikingDeepLinkParser.ParsePlaceArguments(query);
            Assert.AreEqual("6743", place["Location"]);
            Assert.IsTrue(string.IsNullOrEmpty(place["X"]));
        }
    }
}

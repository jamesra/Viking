using System;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="Util.AppendDefaultVolumeFilenameIfMissing"/>, used when opening volumes from
    /// Program, deep-link normalization, and the WPF volume picker: null stays null, paths that already
    /// look like files (a dot in the URI path) are left alone, and bare directory URLs gain
    /// <c>volume.vikingxml</c> with a slash when the string did not already end in one.
    /// </summary>
    [TestClass]
    public class UtilAppendDefaultVolumeFilenameTests
    {
        private const string DefaultFile = "volume.vikingxml";

        private static readonly char[] AlnumAndDash =
            "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();

        private static Gen<char> AlnumChar() => Gen.Elements(AlnumAndDash);

        private static Gen<string> VolumeLikeSegment() =>
            from len in Gen.Choose(1, 16)
            from chars in Gen.ArrayOf(len, AlnumChar())
            select new string(chars);

        private static readonly Arbitrary<string> HttpUrlsWithoutDotInPath =
            (from host in Gen.Elements("example.com", "connectomes.utah.edu", "data.local")
             from segment in VolumeLikeSegment()
             from trailingSlash in Gen.Elements(false, true)
             select $"https://{host}/{segment}" + (trailingSlash ? "/" : string.Empty))
            .ToArbitrary();

        private static readonly Arbitrary<string> HttpUrlsWithDotInPath =
            (from host in Gen.Elements("example.com", "storage.test")
             from folder in VolumeLikeSegment()
             from ext in Gen.Elements(".vikingxml", ".xml", ".txt")
             from fileStem in VolumeLikeSegment()
             select $"https://{host}/{folder}/{fileStem}{ext}")
            .ToArbitrary();

        [TestMethod]
        public void Null_ReturnsNull()
        {
            Assert.IsNull(Util.AppendDefaultVolumeFilenameIfMissing(null));
        }

        [TestMethod]
        public void BareVolumePath_AppendsDefaultFilenameWithSlash()
        {
            Assert.AreEqual(
                "https://connectomes.utah.edu/kasthuri11/volume.vikingxml",
                Util.AppendDefaultVolumeFilenameIfMissing("https://connectomes.utah.edu/kasthuri11"));
        }

        [TestMethod]
        public void TrailingSlash_AppendsDefaultFilenameWithoutExtraSlash()
        {
            Assert.AreEqual(
                "https://connectomes.utah.edu/kasthuri11/volume.vikingxml",
                Util.AppendDefaultVolumeFilenameIfMissing("https://connectomes.utah.edu/kasthuri11/"));
        }

        [TestMethod]
        public void PathAlreadyHasVolumeXml_Unchanged()
        {
            const string url = "https://example.com/data/volume.vikingxml";
            Assert.AreEqual(url, Util.AppendDefaultVolumeFilenameIfMissing(url));
        }

        [TestMethod]
        public void DotAnywhereInPath_LeavesUrlUnchanged()
        {
            const string url = "https://example.com/vol.1/subdir";
            Assert.AreEqual(url, Util.AppendDefaultVolumeFilenameIfMissing(url));
        }

        [TestMethod]
        public void HttpScheme_RoundTripsThroughUri()
        {
            const string input = "http://127.0.0.1:8080/testvolume";
            string result = Util.AppendDefaultVolumeFilenameIfMissing(input);
            Assert.AreEqual("http://127.0.0.1:8080/testvolume/volume.vikingxml", result);
            _ = new Uri(result);
        }

        [TestMethod]
        public void RootHttpUrl_AppendsDefaultUnderHost()
        {
            Assert.AreEqual(
                "https://example.com/volume.vikingxml",
                Util.AppendDefaultVolumeFilenameIfMissing("https://example.com/"));
        }

        [TestMethod]
        public void PathWithoutDot_AlwaysEndsWithDefaultFile()
        {
            Prop.ForAll(HttpUrlsWithoutDotInPath, url =>
            {
                string inputPath = new Uri(url).GetComponents(UriComponents.Path, UriFormat.SafeUnescaped);
                if (inputPath.Contains("."))
                    return;

                string result = Util.AppendDefaultVolumeFilenameIfMissing(url);
                string expectedSuffix = url.EndsWith("/", StringComparison.Ordinal) ? DefaultFile : "/" + DefaultFile;
                Assert.IsTrue(result.EndsWith(expectedSuffix, StringComparison.Ordinal));
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void PathWithDot_IsIdempotent()
        {
            Prop.ForAll(HttpUrlsWithDotInPath, url =>
            {
                Assert.AreEqual(url, Util.AppendDefaultVolumeFilenameIfMissing(url));
                Assert.AreEqual(url, Util.AppendDefaultVolumeFilenameIfMissing(Util.AppendDefaultVolumeFilenameIfMissing(url)));
            }).QuickCheckThrowOnFailure();
        }
    }
}

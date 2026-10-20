using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="VolumePath"/> URL vs local path joining used when building tile and STOS URIs
    /// from volume host hints and relative mosaic/grid segments.
    /// </summary>
    [TestClass]
    public class VolumePathTests
    {
        [TestMethod]
        public void IsHttp_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(VolumePath.IsHttp(null));
            Assert.IsFalse(VolumePath.IsHttp(string.Empty));
        }

        [TestMethod]
        public void IsHttp_HttpAndHttps_AreCaseInsensitive()
        {
            Assert.IsTrue(VolumePath.IsHttp("http://host/vol"));
            Assert.IsTrue(VolumePath.IsHttp("HTTP://HOST"));
            Assert.IsTrue(VolumePath.IsHttp("https://host/vol"));
            Assert.IsTrue(VolumePath.IsHttp("HTTPS://HOST"));
        }

        [TestMethod]
        public void IsHttp_NonHttpSchemesAndLocalPaths_ReturnFalse()
        {
            Assert.IsFalse(VolumePath.IsHttp("ftp://files.example/vol"));
            Assert.IsFalse(VolumePath.IsHttp("file:///C:/vol"));
            Assert.IsFalse(VolumePath.IsHttp(@"C:\volumes\rc2"));
            Assert.IsFalse(VolumePath.IsHttp("/mnt/volumes/rc2"));
            Assert.IsFalse(VolumePath.IsHttp("http"));
        }

        [TestMethod]
        public void Separator_HttpHost_IsForwardSlash()
        {
            Assert.AreEqual('/', VolumePath.Separator("https://example.com/vol"));
            Assert.AreEqual('/', VolumePath.Separator("HTTP://EXAMPLE"));
        }

        [TestMethod]
        public void Separator_LocalHostHint_UsesOsSeparator()
        {
            Assert.AreEqual(System.IO.Path.DirectorySeparatorChar, VolumePath.Separator(@"D:\data"));
            Assert.AreEqual(System.IO.Path.DirectorySeparatorChar, VolumePath.Separator(string.Empty));
        }

        [TestMethod]
        public void JoinRelative_NullParts_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, VolumePath.JoinRelative("https://example.com", null));
        }

        [TestMethod]
        public void JoinRelative_SkipsNullAndEmptyParts()
        {
            Assert.AreEqual("a/b", VolumePath.JoinRelative("https://h", "a", null, string.Empty, "b"));
        }

        [TestMethod]
        public void JoinRelative_NormalizesBackslashesForHttp()
        {
            Assert.AreEqual("Grid/002/tile.png", VolumePath.JoinRelative("http://host", @"Grid\002", "tile.png"));
        }

        [TestMethod]
        public void JoinRelative_TrimsLeadingAndTrailingSeparatorsOnParts()
        {
            Assert.AreEqual("Mosaic/004", VolumePath.JoinRelative("https://host", "/Mosaic/", "\\004\\"));
        }

        [TestMethod]
        public void JoinRelative_DoesNotPrependHost()
        {
            Assert.AreEqual("Grid/001/X001.png", VolumePath.JoinRelative("https://tiles.example/vol", "Grid", "001", "X001.png"));
        }

        [TestMethod]
        public void JoinRelative_LocalHost_NormalizesToOsSeparator()
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            Assert.AreEqual($"Grid{sep}001{sep}tile.png", VolumePath.JoinRelative(@"C:\vol", "Grid/001", @"tile.png"));
        }

        [TestMethod]
        public void Combine_NullHost_ReturnsJoinRelativeTailOnly()
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            Assert.AreEqual($"a{sep}b", VolumePath.Combine(null, "a", "b"));
        }

        [TestMethod]
        public void Combine_TrimsHostTrailingSeparators()
        {
            Assert.AreEqual(
                "http://host/vol/Mosaic/004/tile.png",
                VolumePath.Combine("http://host/vol/", "Mosaic", "004", "tile.png"));
        }

        [TestMethod]
        public void Combine_NoRelativeParts_ReturnsTrimmedHost()
        {
            Assert.AreEqual("http://host/vol", VolumePath.Combine("http://host/vol/", Array.Empty<string>()));
            Assert.AreEqual(@"C:\vol", VolumePath.Combine(@"C:\vol\", Array.Empty<string>()));
        }

        [TestMethod]
        public void Combine_LocalHost_JoinsWithOsSeparator()
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            string path = VolumePath.Combine(@"C:\cache\vol", "Grid", "002", "tile.png");
            Assert.AreEqual($@"C:\cache\vol{sep}Grid{sep}002{sep}tile.png", path);
        }

        [TestMethod]
        public void JoinRelative_MatchesReferenceImplementation()
        {
            var hostHints = new[] { "https://example.com/vol", @"C:\volumes\rc2", string.Empty, "ftp://ignored" };
            var partGen = Gen.OneOf(
                Gen.Constant<string>(null),
                Gen.Constant(string.Empty),
                Arb.Generate<NonEmptyString>().Select(s => s.Get).Where(x => x.Length <= 32),
                Gen.Elements("Grid", "Mosaic", "004", @"seg\001", "/trim/", "\\both\\"));

            var prop = Prop.ForAll(
                Arb.From(Gen.Elements(hostHints)),
                Arb.From(Gen.ArrayOf(partGen)),
                (hostHint, parts) =>
                {
                    char sep = VolumePath.Separator(hostHint);
                    string expected = ReferenceJoinRelative(sep, parts);
                    return VolumePath.JoinRelative(hostHint, parts) == expected;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        [TestMethod]
        public void Combine_HttpHost_NeverContainsBackslash()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Elements("http://tiles.example/vol", "https://HOST/VOL/")),
                Arb.From(Gen.ArrayOf(Arb.Generate<NonEmptyString>().Select(s => s.Get).Where(x => x.Length <= 16))),
                (host, segments) =>
                {
                    string combined = VolumePath.Combine(host, segments.ToArray());
                    return !combined.Contains('\\');
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        private static string ReferenceJoinRelative(char sep, string[] parts)
        {
            if (parts == null)
                return string.Empty;

            string result = string.Empty;
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                string normalized = part.Replace('\\', sep).Replace('/', sep).Trim(sep);
                if (normalized.Length == 0)
                    continue;

                result = result.Length == 0 ? normalized : result + sep + normalized;
            }

            return result;
        }
    }
}

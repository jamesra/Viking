using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Viking.VolumeModel;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="Volume.IsVolumePathLocal"/> used when opening volumes to classify remote
    /// HTTP(S) URLs versus local or non-web paths (file shares, disk paths, other URI schemes).
    /// </summary>
    [TestClass]
    public class VolumeIsVolumePathLocalTests
    {
        [TestMethod]
        public void IsVolumePathLocal_HttpAndHttps_ReturnFalse()
        {
            Assert.IsFalse(Volume.IsVolumePathLocal("http://host/vol"));
            Assert.IsFalse(Volume.IsVolumePathLocal("HTTP://HOST"));
            Assert.IsFalse(Volume.IsVolumePathLocal("https://host/vol"));
            Assert.IsFalse(Volume.IsVolumePathLocal("HTTPS://HOST/VOL/"));
        }

        [TestMethod]
        public void IsVolumePathLocal_FileAndDiskPaths_ReturnTrue()
        {
            Assert.IsTrue(Volume.IsVolumePathLocal(@"C:\volumes\rc2"));
            Assert.IsTrue(Volume.IsVolumePathLocal("file:///C:/vol/volume.vikingxml"));
        }

        [TestMethod]
        public void IsVolumePathLocal_NonHttpSchemes_ReturnTrue()
        {
            Assert.IsTrue(Volume.IsVolumePathLocal("ftp://files.example/vol"));
        }

        [TestMethod]
        public void IsVolumePathLocal_HttpUrls_AlwaysFalse()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Elements("http", "https", "HTTP", "HTTPS")),
                Arb.From(Gen.Elements("host", "tiles.example", "HOST", "127.0.0.1")),
                Arb.From(Gen.Elements("", "/vol", "/vol/volume.vikingxml")),
                (scheme, host, tail) =>
                {
                    string path = $"{scheme}://{host}{tail}";
                    return Volume.IsVolumePathLocal(path) == false;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        [TestMethod]
        public void IsVolumePathLocal_NonHttpAbsoluteUris_ReturnTrue()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Elements(
                    "file:///C:/data/vol",
                    "file:///tmp/vol",
                    "ftp://files.example/vol",
                    @"C:\volumes\rc2")),
                path => Volume.IsVolumePathLocal(path));

            prop.Check(Configuration.QuickThrowOnFailure);
        }
    }
}

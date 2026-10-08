using System;
using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;
using Viking.UI;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins bookmark and copy-link helpers on <see cref="Util"/>:
    /// <see cref="Util.CoordinatesToURI"/> (deep-link query string with the open volume host),
    /// <see cref="Util.CoordinatesToCopyPaste"/> (tab-separated coordinate clipboard text), and
    /// <see cref="Util.GetAttribute"/> (single custom attribute lookup on a type).
    /// </summary>
    [TestClass]
    public class UtilCoordinatesAndAttributeTests
    {
        private VolumeViewModel? _savedVolume;

        [TestInitialize]
        public void SaveVolume()
        {
            _savedVolume = State.volume;
        }

        [TestCleanup]
        public void RestoreVolume()
        {
            State.volume = _savedVolume;
        }

        private static VolumeViewModel CreateVolumeWithHost(string host)
        {
            Volume volume = (Volume)FormatterServices.GetUninitializedObject(typeof(Volume));
            FieldInfo? hostField = typeof(Volume).GetField("_Host", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(hostField, "Volume._Host field must exist for URI tests.");
            hostField.SetValue(volume, host);

            VolumeViewModel viewModel =
                (VolumeViewModel)FormatterServices.GetUninitializedObject(typeof(VolumeViewModel));
            FieldInfo? volumeField = typeof(VolumeViewModel).GetField(
                "_Volume",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(volumeField, "VolumeViewModel._Volume field must exist for URI tests.");
            volumeField.SetValue(viewModel, volume);
            return viewModel;
        }

        [PropertyPage(typeof(string))]
        private sealed class TypeWithOnePropertyPage
        {
        }

        private sealed class TypeWithNoCustomAttributes
        {
        }

        private sealed class UnrelatedMarkerAttribute : Attribute
        {
        }

        [UnrelatedMarker]
        private sealed class TypeWithUnrelatedAttribute
        {
        }

        [TestMethod]
        public void CoordinatesToCopyPaste_Example_UsesTabSeparatedLabelsAndFormats()
        {
            const string expected = "X: 123456.8\tY: 89012.3\tZ: 42\tDS: 16.00";
            Assert.AreEqual(expected, Util.CoordinatesToCopyPaste(123456.789, 89012.345, 42, 16));
        }

        [TestMethod]
        public void CoordinatesToURI_Example_IncludesHostAndQueryKeys()
        {
            State.volume = CreateVolumeWithHost("connectomes.utah.edu/kasthuri11");

            const string expected =
                "http://connectomes.utah.edu/software/viking.application?" +
                "Volume=connectomes.utah.edu/kasthuri11&" +
                "X=123456.8&Y=89012.3&Z=42&DS=16.00";

            Assert.AreEqual(expected, Util.CoordinatesToURI(123456.789, 89012.345, 42, 16));
        }

        [TestMethod]
        public void GetAttribute_SinglePropertyPage_ReturnsInstance()
        {
            Attribute? attrib = Util.GetAttribute(typeof(TypeWithOnePropertyPage), typeof(PropertyPageAttribute));
            Assert.IsNotNull(attrib);
            Assert.IsInstanceOfType(attrib, typeof(PropertyPageAttribute));
            Assert.AreEqual(typeof(string), ((PropertyPageAttribute)attrib).TargetType);
        }

        [TestMethod]
        public void GetAttribute_NoMatchingAttribute_ReturnsNull()
        {
            Assert.IsNull(Util.GetAttribute(typeof(TypeWithNoCustomAttributes), typeof(PropertyPageAttribute)));
            Assert.IsNull(Util.GetAttribute(typeof(TypeWithUnrelatedAttribute), typeof(PropertyPageAttribute)));
        }

        private static Gen<double> VolumeCoordinateGen() =>
            Gen.Choose(0, 999_999_999).Select(i => i / 10.0);

        private static Gen<double> DownsampleGen() =>
            Gen.Choose(1, 10_000).Select(i => i / 100.0);

        private static Gen<(double X, double Y, int Z, double Downsample)> CoordinateSampleGen() =>
            from x in VolumeCoordinateGen()
            from y in VolumeCoordinateGen()
            from z in Gen.Choose(-1000, 5000)
            from ds in DownsampleGen()
            select (x, y, z, ds);

        [TestMethod]
        public void CoordinatesToCopyPaste_AlwaysUsesExpectedLabelsAndTabSeparators()
        {
            Prop.ForAll(Arb.From(CoordinateSampleGen()), sample =>
                {
                    (double x, double y, int z, double ds) = sample;
                    string clip = Util.CoordinatesToCopyPaste(x, y, z, ds);
                    Assert.IsTrue(clip.StartsWith("X: ", StringComparison.Ordinal));
                    Assert.IsTrue(clip.Contains("\tY: ", StringComparison.Ordinal));
                    Assert.IsTrue(clip.Contains("\tZ: ", StringComparison.Ordinal));
                    Assert.IsTrue(clip.Contains("\tDS: ", StringComparison.Ordinal));
                    Assert.AreEqual(
                        $"X: {x.ToString("F1")}\tY: {y.ToString("F1")}\tZ: {z.ToString()}\tDS: {ds.ToString("F2")}",
                        clip);
                }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void CoordinatesToURI_WhenVolumeHostSet_ContainsVolumeQueryParameter()
        {
            const string host = "data.example.org/testvolume";
            State.volume = CreateVolumeWithHost(host);

            Prop.ForAll(Arb.From(CoordinateSampleGen()), sample =>
                {
                    (double x, double y, int z, double ds) = sample;
                    string uri = Util.CoordinatesToURI(x, y, z, ds);
                    Assert.IsTrue(uri.StartsWith(
                        "http://connectomes.utah.edu/software/viking.application?",
                        StringComparison.Ordinal));
                    Assert.IsTrue(uri.Contains("Volume=" + host + "&", StringComparison.Ordinal));
                    Assert.IsTrue(uri.Contains("X=" + x.ToString("F1") + "&", StringComparison.Ordinal));
                    Assert.IsTrue(uri.Contains("Y=" + y.ToString("F1") + "&", StringComparison.Ordinal));
                    Assert.IsTrue(uri.Contains("Z=" + z.ToString() + "&", StringComparison.Ordinal));
                    Assert.IsTrue(uri.EndsWith("DS=" + ds.ToString("F2"), StringComparison.Ordinal));
                }).QuickCheckThrowOnFailure();
        }
    }
}

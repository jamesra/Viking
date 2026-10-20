using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.UI.WPF.Models;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="VolumeInfo"/>: each bound property raises PropertyChanged once per real change and not on a
    /// same-value set, ToString falls back to "Volume {Id}" when Name is null, and the correction-service fields default to null.
    /// </summary>
    [TestClass]
    public class VolumeInfoTests
    {
        private static List<string?> Record(VolumeInfo info)
        {
            var raised = new List<string?>();
            info.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
            return raised;
        }

        [TestMethod]
        public void SettingEachProperty_RaisesPropertyChangedOnceWithItsName()
        {
            var info = new VolumeInfo();
            var raised = Record(info);

            info.Id = 7;
            info.Name = "n";
            info.Organization = "o";
            info.VolumeXmlUrl = "u";
            info.Description = "d";

            CollectionAssert.AreEqual(new[] { "Id", "Name", "Organization", "VolumeXmlUrl", "Description" }, raised);
        }

        [TestMethod]
        public void SettingSameValue_DoesNotRaisePropertyChanged()
        {
            var info = new VolumeInfo { Id = 7, Name = "n", Organization = "o", VolumeXmlUrl = "u", Description = "d" };
            var raised = Record(info);

            info.Id = 7;
            info.Name = "n";
            info.Organization = "o";
            info.VolumeXmlUrl = "u";
            info.Description = "d";

            Assert.AreEqual(0, raised.Count);
        }

        [TestMethod]
        public void SettingNullOnNullProperty_DoesNotRaisePropertyChanged()
        {
            var info = new VolumeInfo();
            var raised = Record(info);

            info.Name = null!;
            info.Description = null!;
            info.Organization = null!;
            info.VolumeXmlUrl = null!;

            Assert.AreEqual(0, raised.Count);
        }

        [TestMethod]
        public void ChangingValueTwice_RaisesTwice()
        {
            var info = new VolumeInfo();
            var raised = Record(info);

            info.Name = "a";
            info.Name = "b";

            CollectionAssert.AreEqual(new[] { "Name", "Name" }, raised);
        }

        [TestMethod]
        public void ToString_ReturnsNameWhenSet_ElseVolumeId()
        {
            var info = new VolumeInfo { Id = 42 };
            Assert.AreEqual("Volume 42", info.ToString());

            info.Name = "Rabbit";
            Assert.AreEqual("Rabbit", info.ToString());
        }

        [TestMethod]
        public void CorrectionServiceFields_DefaultToNullAndDoNotRaise()
        {
            var info = new VolumeInfo();
            var raised = Record(info);

            Assert.IsNull(info.CorrectionServiceUrl);
            Assert.IsNull(info.AnnotationDatabaseName);

            info.CorrectionServiceUrl = "http://x";
            info.AnnotationDatabaseName = "db";

            Assert.AreEqual(0, raised.Count);
        }
    }
}

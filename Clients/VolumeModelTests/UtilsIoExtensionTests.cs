using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Xml.Linq;
using Utils;

namespace VolumeModelTests
{
    /// <summary>
    /// Pins <see cref="IO"/> XElement helpers used when parsing Viking volume XML (case-insensitive
    /// attributes, root-to-leaf paths, and debug path printing).
    /// </summary>
    [TestClass]
    public class UtilsIoExtensionTests
    {
        [TestMethod]
        public void HasAttributeCaseInsensitive_MatchesMixedCaseAttributeName()
        {
            var element = new XElement("Channel", new XAttribute("Color", "Red"));
            Assert.IsTrue(element.HasAttributeCaseInsensitive("color"));
            Assert.IsTrue(element.HasAttributeCaseInsensitive("COLOR"));
            Assert.IsFalse(element.HasAttributeCaseInsensitive("name"));
        }

        [TestMethod]
        public void GetAttributeCaseInsensitive_ReturnsAttributeIgnoringCase()
        {
            var element = new XElement("Section", new XAttribute("Number", "42"));
            XAttribute attrib = element.GetAttributeCaseInsensitive("number");
            Assert.AreEqual("Number", attrib.Name.LocalName);
            Assert.AreEqual("42", attrib.Value);
        }

        [TestMethod]
        public void GetAttributeCaseInsensitive_MissingAttribute_ThrowsXmlMissingDataException()
        {
            var volume = new XElement("Volume");
            var mapping = new XElement("Mapping");
            volume.Add(mapping);

            var ex = Assert.ThrowsException<XMLMissingDataException>(() => mapping.GetAttributeCaseInsensitive("path"));
            StringAssert.Contains(ex.Message, "path");
            StringAssert.Contains(ex.Message, "Mapping");
        }

        [TestMethod]
        public void GetRootToElementList_OrdersRootToLeafIncludingTarget()
        {
            var root = new XElement("Volume");
            var channels = new XElement("Channels");
            var channel = new XElement("Channel", new XAttribute("name", "R"));
            root.Add(channels);
            channels.Add(channel);

            var list = channel.GetRootToElementList();
            CollectionAssert.AreEqual(new[] { root, channels, channel }, list);
        }

        [TestMethod]
        public void GetRootToElementList_RootElement_ReturnsSingleton()
        {
            var root = new XElement("Volume");
            CollectionAssert.AreEqual(new[] { root }, root.GetRootToElementList());
        }

        [TestMethod]
        public void PrintVikingXMLElementList_IncludesNameAndPathAttributes()
        {
            var root = new XElement("Volume", new XAttribute("name", "RC1"));
            var section = new XElement("Section", new XAttribute("path", "Mosaic/001"));
            root.Add(section);

            string text = section.GetRootToElementList().PrintVikingXMLElementList();

            StringAssert.Contains(text, "<Volume name=");
            StringAssert.Contains(text, "RC1");
            StringAssert.Contains(text, "<Section path=");
            StringAssert.Contains(text, "Mosaic/001");
        }

        [TestMethod]
        public void HasAttributeCaseInsensitive_MatchesReferenceOnGeneratedNames()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Elements("name", "path", "Color", "NUMBER", "SectionNumber")),
                Arb.From(Arb.Generate<NonEmptyString>().Select(s => s.Get).Where(x => x.Length <= 24)),
                (queryName, value) =>
                {
                    var element = new XElement("Elem", new XAttribute(queryName, value));
                    bool fromHelper = element.HasAttributeCaseInsensitive(queryName.ToUpperInvariant());
                    bool reference = element.Attributes().Any(a =>
                        string.Compare(a.Name.LocalName, queryName, StringComparison.OrdinalIgnoreCase) == 0);
                    return fromHelper == reference;
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }

        [TestMethod]
        public void GetRootToElementList_EveryStepIsParentOfNextEndingAtTarget()
        {
            var prop = Prop.ForAll(
                Arb.From(Gen.Choose(1, 6)),
                depth =>
                {
                    XElement root = new XElement("Volume");
                    XElement cursor = root;
                    for (int i = 0; i < depth; i++)
                    {
                        var child = new XElement($"Level{i}");
                        cursor.Add(child);
                        cursor = child;
                    }

                    var list = cursor.GetRootToElementList();
                    if (list.Count != depth + 1 || !ReferenceEquals(list[^1], cursor))
                        return false;

                    for (int i = 0; i < list.Count - 1; i++)
                    {
                        if (!ReferenceEquals(list[i + 1].Parent, list[i]))
                            return false;
                    }

                    return ReferenceEquals(list[0].Parent, null);
                });

            prop.Check(Configuration.QuickThrowOnFailure);
        }
    }
}

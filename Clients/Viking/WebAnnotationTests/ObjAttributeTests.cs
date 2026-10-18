using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the parse, serialize, equality and list-helper behavior of <see cref="ObjAttribute"/>
    /// and <see cref="AttributeExtensions"/>, which structure tags are stored through.
    /// </summary>
    [TestClass]
    public class ObjAttributeTests
    {
        [TestMethod]
        public void Parse_Null_ReturnsEmpty() => Assert.AreEqual(0, ObjAttribute.Parse(null).Count);

        [TestMethod]
        public void Parse_XmlWithoutStructureRoot_ReturnsEmpty() =>
            Assert.AreEqual(0, ObjAttribute.Parse("<Other/>").Count);

        [TestMethod]
        public void Parse_Xml_ReadsNameAndOptionalValueAndSkipsNamelessEntries()
        {
            List<ObjAttribute> list = ObjAttribute.Parse(
                "<Structure><Attrib Name=\"b\" Value=\"2\"/><Attrib Name=\"a\"/><Attrib Value=\"x\"/><Attrib Name=\"\"/></Structure>");

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("a", list[0].Name);
            Assert.AreEqual("", list[0].Value, "A missing Value attribute keeps the default empty value.");
            Assert.AreEqual("b", list[1].Name);
            Assert.AreEqual("2", list[1].Value);
        }

        [TestMethod]
        public void Parse_Xml_ResultIsSortedByNameThenValue()
        {
            List<ObjAttribute> list = ObjAttribute.Parse(
                "<Structure><Attrib Name=\"b\"/><Attrib Name=\"a\" Value=\"2\"/><Attrib Name=\"a\" Value=\"1\"/></Structure>");

            CollectionAssert.AreEqual(
                new[] { "a=1", "a=2", "b=" },
                list.Select(a => a.Name + "=" + a.Value).ToArray());
        }

        [TestMethod]
        public void Parse_LegacySemicolonString_TrimsAndSkipsEmptyKeys()
        {
            List<ObjAttribute> list = ObjAttribute.Parse(" b = 2 ; a ;; =x ;  ");

            // "=x" has an empty key after trimming but is not skipped: only a fully blank entry is.
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("", list[0].Name);
            Assert.AreEqual("x", list[0].Value);
            Assert.AreEqual("a", list[1].Name);
            Assert.AreEqual("", list[1].Value);
            Assert.AreEqual("b", list[2].Name);
            Assert.AreEqual("2", list[2].Value);
        }

        [TestMethod]
        public void TagStringsToList_Null_ReturnsEmpty() =>
            Assert.AreEqual(0, ObjAttribute.TagStringsToList(null).Count);

        [TestMethod]
        public void TagStringsToList_OnlyFirstEqualsSplitsKeyFromValue()
        {
            List<ObjAttribute> list = ObjAttribute.TagStringsToList(["k=v=w"]);

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("k", list[0].Name);
            Assert.AreEqual("v=w", list[0].Value);
        }

        [TestMethod]
        public void TagStringsToList_BlankEntriesAreDropped() =>
            Assert.AreEqual(0, ObjAttribute.TagStringsToList(["", "   "]).Count);

        [TestMethod]
        public void ToXml_Empty_ReturnsNull() => Assert.IsNull(ObjAttribute.ToXml([]));

        [TestMethod]
        public void ToXml_OmitsValueWhenNullOrEmpty()
        {
            string xml = ObjAttribute.ToXml([new ObjAttribute("a", null), new ObjAttribute("b", ""), new ObjAttribute("c", "3")]);

            Assert.IsFalse(xml.Contains("Name=\"a\" Value"));
            Assert.IsFalse(xml.Contains("Name=\"b\" Value"));
            Assert.IsTrue(xml.Contains("Name=\"c\" Value=\"3\""));
        }

        [TestMethod]
        public void ToXml_ThenParse_RoundTripsNamesAndValues()
        {
            List<ObjAttribute> original =
            [
                new ObjAttribute("alpha", "1 & <2>"),
                new ObjAttribute("beta", ""),
                new ObjAttribute("gamma", "quote\"d"),
            ];

            List<ObjAttribute> parsed = ObjAttribute.Parse(ObjAttribute.ToXml(original));

            CollectionAssert.AreEqual(
                original.Select(a => a.Name + "|" + a.Value).ToArray(),
                parsed.Select(a => a.Name + "|" + a.Value).ToArray());
        }

        [TestMethod]
        public void ToString_ShowsValueOnlyWhenNonBlank()
        {
            Assert.AreEqual("n", new ObjAttribute(" n ", null).ToString());
            Assert.AreEqual("n", new ObjAttribute("n", "  ").ToString());
            Assert.AreEqual("n: v", new ObjAttribute("n", " v ").ToString());
        }

        [TestMethod]
        public void Equality_UsesNameOnly()
        {
            ObjAttribute a = new("n", "1");
            ObjAttribute b = new("n", "2");

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a.Equals((object)b));
            Assert.IsFalse(a.Equals((object)new ObjAttribute("m", "1")));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a == new ObjAttribute("m", "1"));
            Assert.IsTrue(a != new ObjAttribute("m", "1"));
        }

        [TestMethod]
        public void Equality_NullHandling()
        {
            ObjAttribute? none = null;
            ObjAttribute a = new("n", "1");

            Assert.IsTrue(none == null);
            Assert.IsFalse(none != null);
            Assert.IsFalse(a == null);
            Assert.IsTrue(a != null);
            Assert.IsFalse(a.Equals((object?)null));
            Assert.IsFalse(a.Equals((object)5));
        }

        [TestMethod]
        public void Equality_AgainstString_ComparesName()
        {
            ObjAttribute a = new("n", "1");

            Assert.IsTrue(a.Equals("n"));
            Assert.IsTrue(a.Equals((object)"n"));
            Assert.IsFalse(a.Equals("1"));
            Assert.AreEqual(0, a.CompareTo("n"));
        }

        [TestMethod]
        public void CompareTo_OrdersByNameThenValue()
        {
            Assert.IsTrue(new ObjAttribute("a", "9").CompareTo(new ObjAttribute("b", "1")) < 0);
            Assert.IsTrue(new ObjAttribute("a", "1").CompareTo(new ObjAttribute("a", "2")) < 0);
            Assert.AreEqual(0, new ObjAttribute("a", "1").CompareTo(new ObjAttribute("a", "1")));
        }

        [TestMethod]
        public void Contains_MatchesNameAndIgnoresValue()
        {
            List<ObjAttribute> list = [new ObjAttribute("a", "1")];

            Assert.IsTrue(list.Contains("a"));
            Assert.IsFalse(list.Contains("b"));
            Assert.IsFalse(list.Contains("1"));
        }

        [TestMethod]
        public void RemoveAll_RemovesEveryEntryWithTheName()
        {
            List<ObjAttribute> list = [new ObjAttribute("a", "1"), new ObjAttribute("b", "x"), new ObjAttribute("a", "2")];

            Assert.AreEqual(2, list.RemoveAll("a"));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("b", list[0].Name);
            Assert.AreEqual(0, list.RemoveAll("zzz"));
        }

        [TestMethod]
        public void ToggleAttribute_AddsWithValueThenRemoves()
        {
            List<ObjAttribute> list = [];

            Assert.IsTrue(list.ToggleAttribute("t", "v"));
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("v", list[0].Value);

            Assert.IsFalse(list.ToggleAttribute("t", "other"));
            Assert.AreEqual(0, list.Count);
        }
    }
}

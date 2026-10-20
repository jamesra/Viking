using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
        public void Parse_Null_ReturnsEmpty() => Assert.AreEqual(0, ObjAttributeParser.ParseAttributes(null).Count);

        [TestMethod]
        public void Parse_XmlWithoutStructureRoot_ReturnsEmpty() =>
            Assert.AreEqual(0, ObjAttributeParser.ParseAttributes("<Other/>").Count);

        [TestMethod]
        public void Parse_Xml_ReadsNameAndOptionalValueAndSkipsNamelessEntries()
        {
            List<ObjAttribute> list = ObjAttributeParser.ParseAttributes(
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
            List<ObjAttribute> list = ObjAttributeParser.ParseAttributes(
                "<Structure><Attrib Name=\"b\"/><Attrib Name=\"a\" Value=\"2\"/><Attrib Name=\"a\" Value=\"1\"/></Structure>");

            CollectionAssert.AreEqual(
                new[] { "a=1", "a=2", "b=" },
                list.Select(a => a.Name + "=" + a.Value).ToArray());
        }

        /// <summary>
        /// The XML parser's reading of an attribute string, as ParseAttributes did before it scanned the common shape
        /// itself. The scanner must give the same list for every input, falling back to XML for what it does not cover.
        /// </summary>
        static string[] ReferenceParse(string xml)
        {
            var root = System.Xml.Linq.XDocument.Parse(xml).Element("Structure");
            if (root == null)
                return new string[0];

            return root.Elements("Attrib")
                .Select(e => new ObjAttribute(e.Attribute("Name")?.Value ?? "", e.Attribute("Value")?.Value ?? ""))
                .Where(a => a.Name.Length > 0)
                .OrderBy(a => a)
                .Select(a => a.Name + "=" + a.Value)
                .ToArray();
        }

        [DataTestMethod]
        [DataRow("<Structure><Attrib Name=\"Conventional\" /><Attrib Name=\"Glutamate\" Value=\"0.5\" /></Structure>")]
        [DataRow("<?xml version=\"1.0\" encoding=\"utf-16\"?><Structure><Attrib Name=\"a\" /></Structure>")]
        [DataRow("<Structure />")]
        [DataRow("<Structure></Structure>")]
        [DataRow("<Structure>\r\n  <Attrib Name='single' Value='q'></Attrib>\r\n</Structure>  ")]
        [DataRow("<Structure><Attrib Value=\"v\" Name=\"reversed\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"a&amp;b\" Value=\"&lt;&gt;&quot;&apos;&#65;&#x42;\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"\"/><Attrib Value=\"x\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"tab\" Value=\"a\tb\"/></Structure>")]
        [DataRow("<Structure><!-- note --><Attrib Name=\"c\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"x\" Extra=\"1\"/></Structure>")]
        [DataRow("<Structure><Other/><Attrib Name=\"y\"/></Structure>")]
        [DataRow("<Other><Attrib Name=\"z\"/></Other>")]
        [DataRow("<Structure><Attrib Name=\"b\" Value=\"2\"/><Attrib Name=\"a\" Value=\"1\"/><Attrib Name=\"a\"/></Structure>")]
        [DataRow("<?xml-stylesheet href=\"s.xsl\"?><Structure><Attrib Name=\"pi\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"emoji\" Value=\"&#x1F600;\U0001F600\"/></Structure>")]
        public void Parse_Xml_MatchesTheXmlParser(string xml)
        {
            CollectionAssert.AreEqual(
                ReferenceParse(xml),
                ObjAttributeParser.ParseAttributes(xml).Select(a => a.Name + "=" + a.Value).ToArray());
        }

        [DataTestMethod]
        [DataRow("<Structure><Attrib Name=\"a\"Value=\"b\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"a\" Value=\"&#xFFFE;\"/></Structure>")]
        [DataRow("<Structure><Attrib Name=\"a\" Value=\"\u0001\"/></Structure>")]
        [DataRow("<?xml?><Structure><Attrib Name=\"a\"/></Structure>")]
        public void Parse_MalformedXml_IsRejectedAsTheXmlParserDoes(string xml) =>
            Assert.ThrowsException<System.Xml.XmlException>(() => ObjAttributeParser.ParseAttributes(xml));

        [TestMethod]
        public void Parse_Xml_RoundTripsToXml()
        {
            List<ObjAttribute> original = ObjAttributeParser.ParseAttributes(
                "<Structure><Attrib Name=\"a &amp; b\" Value=\"x&quot;y\"/><Attrib Name=\"c\"/></Structure>");

            List<ObjAttribute> again = ObjAttributeParser.ParseAttributes(original.ToXml());

            CollectionAssert.AreEqual(original.Select(a => a.Name + "=" + a.Value).ToArray(), again.Select(a => a.Name + "=" + a.Value).ToArray());
        }

        [TestMethod]
        public void Parse_LegacySemicolonString_TrimsAndSkipsEmptyKeys()
        {
            List<ObjAttribute> list = ObjAttributeParser.ParseAttributes(" b = 2 ; a ;; =x ;  ");

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
            Assert.AreEqual(0, ObjAttributeParser.TagStringsToList(null).Count);

        [TestMethod]
        public void TagStringsToList_OnlyFirstEqualsSplitsKeyFromValue()
        {
            List<ObjAttribute> list = ObjAttributeParser.TagStringsToList(["k=v=w"]);

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual("k", list[0].Name);
            Assert.AreEqual("v=w", list[0].Value);
        }

        [TestMethod]
        public void TagStringsToList_BlankEntriesAreDropped() =>
            Assert.AreEqual(0, ObjAttributeParser.TagStringsToList(["", "   "]).Count);

        [TestMethod]
        public void ToXml_Empty_ReturnsNull() => Assert.IsNull(ObjAttributeParser.ToXml([]));

        [TestMethod]
        public void ToXml_OmitsValueWhenNullOrEmpty()
        {
            string xml = ObjAttributeParser.ToXml([new ObjAttribute("a", null), new ObjAttribute("b", ""), new ObjAttribute("c", "3")]);

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

            List<ObjAttribute> parsed = ObjAttributeParser.ParseAttributes(ObjAttributeParser.ToXml(original));

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

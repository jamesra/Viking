using AnnotationService.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="StructureObj.TagsXML"/> to the attribute XML that <see cref="StructureObj.Attributes"/> parses.
    /// The getter once returned itself and overflowed the stack, which kills the process. Structures are built from
    /// plain data with no links, so no store or server is touched.
    /// </summary>
    [TestClass]
    public class StructureObjTagsXmlTests
    {
        private const string Xml = "<Structure><Attrib Name=\"Bipolar\" Value=\"\"/><Attrib Name=\"Rod\" Value=\"2\"/></Structure>";

        [TestMethod]
        public void TagsXmlReturnsTheAttributeXmlThatAttributesParses()
        {
            StructureObj structure = new(new Structure { ID = 7, AttributesXml = Xml });

            Assert.AreEqual(Xml, structure.TagsXML);
            Assert.AreEqual(Xml, ((IStructureReadOnly)structure).TagsXML);
            CollectionAssert.AreEqual(ObjAttribute.Parse(structure.TagsXML).Select(a => a.Name).ToArray(),
                                      structure.Attributes.Select(a => a.Name).ToArray());
        }

        /// <summary>
        /// A structure without attributes reports null, as the WCF structure adapter does, rather than throwing.
        /// </summary>
        [TestMethod]
        public void TagsXmlIsNullWhenTheStructureHasNoAttributes()
        {
            StructureObj structure = new(new Structure { ID = 8, AttributesXml = null });

            Assert.IsNull(structure.TagsXML);
        }

        /// <summary>
        /// Changing <see cref="StructureObj.Attributes"/> is visible through TagsXML.
        /// </summary>
        [TestMethod]
        public void TagsXmlFollowsAnAttributeChange()
        {
            StructureObj structure = new(new Structure { ID = 9, AttributesXml = null });

            structure.ToggleAttribute("Bipolar");

            StringAssert.Contains(structure.TagsXML, "Bipolar");
        }
    }
}

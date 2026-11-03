using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="StructureObj.TagsXML"/> to the structure's attributes. The getter once returned itself and
    /// overflowed the stack, which kills the process. Structures are built from an ID and type ID, so no store or
    /// server is touched.
    /// </summary>
    [TestClass]
    public class StructureObjTagsXmlTests
    {
        [TestMethod]
        public async Task TagsXmlHoldsTheStructureAttributes()
        {
            StructureObj structure = new(7, 1);
            await structure.SetAttributes([new ObjAttribute("Bipolar", ""), new ObjAttribute("Rod", "2")]);

            string xml = structure.TagsXML;

            Assert.IsNotNull(xml);
            Assert.AreEqual(xml, ((IStructureReadOnly)structure).TagsXML);
            CollectionAssert.AreEqual(structure.Attributes.Select(a => a.Name).ToArray(),
                                      xml.ParseAttributes().Select(a => a.Name).ToArray());
        }

        /// <summary>
        /// A structure without attributes reports null rather than throwing.
        /// </summary>
        [TestMethod]
        public void TagsXmlIsNullWhenTheStructureHasNoAttributes()
        {
            StructureObj structure = new(8, 1);

            Assert.IsNull(structure.TagsXML);
        }

        /// <summary>
        /// Changing <see cref="StructureObj.Attributes"/> is visible through TagsXML.
        /// </summary>
        [TestMethod]
        public async Task TagsXmlFollowsAnAttributeChange()
        {
            StructureObj structure = new(9, 1);

            await structure.ToggleAttribute("Bipolar");

            StringAssert.Contains(structure.TagsXML, "Bipolar");
        }
    }
}

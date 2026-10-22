using Microsoft.VisualStudio.TestTools.UnitTesting;
using rogue1.codepharm.net.XSD.WebAnnotationUserSettings.xsd;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins how a hand-edited <c>Tags</c> attribute of a hotkey's CreateStructureCommandAction becomes the
    /// list of tags toggled onto the new structure.
    /// </summary>
    [TestClass]
    public class CreateStructureCommandActionAttributeListTests
    {
        [TestMethod]
        public void MissingTagsGiveNoAttributes()
        {
            CreateStructureCommandAction action = new();
            Assert.AreEqual(0, action.AttributeList.Length);
        }

        [TestMethod]
        [DataRow("", new string[0])]
        [DataRow("  ", new string[0])]
        [DataRow(";", new string[0])]
        [DataRow("Conventional", new[] { "Conventional" })]
        [DataRow("Bipolar;Ribbon;Glutamate", new[] { "Bipolar", "Ribbon", "Glutamate" })]
        [DataRow("Bipolar; Ribbon ;Glutamate;", new[] { "Bipolar", "Ribbon", "Glutamate" })]
        [DataRow("Ribbon;;Ribbon", new[] { "Ribbon" })]
        public void TagsAreTrimmedAndBlankAndRepeatedEntriesDropped(string tags, string[] expected)
        {
            CreateStructureCommandAction action = new() { Tags = tags };
            CollectionAssert.AreEqual(expected, action.AttributeList);
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Common;

namespace VikingTests
{
    [TestClass]
    public class ResourceScopeNamesTests
    {
        [TestMethod]
        public void ToScope_ReplacesSpacesInVolumeAndPermission()
        {
            Assert.AreEqual("Rabbit-Retina.Read", ResourceScopeNames.ToScope("Rabbit Retina", "Read"));
        }

        [TestMethod]
        public void ToScopePrefix_UnchangedWhenNoSpaces()
        {
            Assert.AreEqual("RC1", ResourceScopeNames.ToScopePrefix("RC1"));
        }
    }
}

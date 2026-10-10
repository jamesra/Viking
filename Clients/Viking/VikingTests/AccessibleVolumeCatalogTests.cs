using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Tokens;

namespace VikingTests
{
    [TestClass]
    public class AccessibleVolumeCatalogTests
    {
        [TestMethod]
        public void FindMatch_ByRegistrationName()
        {
            var volumes = new[]
            {
                new UserResourcePermissions
                {
                    Name = "InferiorMonkey",
                    RegistrationName = "6250-6A",
                    AnnotationServerName = "6250-6A",
                    AnnotationEndpoint = "https://example.com/6250/Annotate.svc"
                }
            };

            UserResourcePermissions match = AccessibleVolumeCatalog.FindMatch(volumes, "6250-6A");
            Assert.IsNotNull(match);
            Assert.AreEqual("InferiorMonkey", match.Name);
        }

        [TestMethod]
        public void FindMatch_ReturnsNullWhenNoCandidates()
        {
            Assert.IsNull(AccessibleVolumeCatalog.FindMatch(new UserResourcePermissions[0], "RC1"));
        }
    }
}

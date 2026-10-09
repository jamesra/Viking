using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.Properties;

namespace WebAnnotationTests.Properties
{
    /// <summary>
    /// The 75 nm auto-polygonize default was stored in user.config and copied on every version upgrade.
    /// These tests lock the one-time replacement with 40 nm and the rule that a chosen radius is kept.
    /// </summary>
    [TestClass]
    public class AutoPolygonizeMinRadiusSettingsTests
    {
        [TestMethod]
        public void LegacyDefaultBecomesFortyNanometersOnce()
        {
            double resolved = Settings.ResolveAutoPolygonizeMinRadiusNanometers(
                Settings.LegacyAutoPolygonizeMinRadiusNanometers,
                alreadyMigrated: false);

            Assert.AreEqual(Settings.DefaultAutoPolygonizeMinRadiusNanometers, resolved, 0.001);
            Assert.AreEqual(40.0, resolved, 0.001);
        }

        [TestMethod]
        public void ChosenRadiusIsKept()
        {
            Assert.AreEqual(10.0, Settings.ResolveAutoPolygonizeMinRadiusNanometers(10.0, alreadyMigrated: false), 0.001);
            Assert.AreEqual(25.0, Settings.ResolveAutoPolygonizeMinRadiusNanometers(25.0, alreadyMigrated: false), 0.001);
            Assert.AreEqual(0.0, Settings.ResolveAutoPolygonizeMinRadiusNanometers(0.0, alreadyMigrated: false), 0.001);
        }

        [TestMethod]
        public void ExplicitSeventyFiveStaysAfterMigration()
        {
            double resolved = Settings.ResolveAutoPolygonizeMinRadiusNanometers(
                Settings.LegacyAutoPolygonizeMinRadiusNanometers,
                alreadyMigrated: true);

            Assert.AreEqual(75.0, resolved, 0.001);
        }
    }
}

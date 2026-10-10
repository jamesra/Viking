using System.Globalization;
using System.Xml.Linq;
using MeasurementExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SIMeasurement;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins that <see cref="Global"/> reads the server-supplied UnitsPerPixel independent of the UI culture.
    /// </summary>
    [TestClass]
    public class MeasurementGlobalScaleParsingTests
    {
        private static XElement Volume(string unitsPerPixel) =>
            new XElement("Volume",
                new XElement("Scale",
                    new XAttribute("UnitsPerPixel", unitsPerPixel),
                    new XAttribute("UnitsOfMeasure", "nm")));

        private static void AssertParsesUnderCulture(string cultureName)
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            double savedScale = Global._UnitsPerPixel;
            SILengthUnits savedUnit = Global._UnitOfMeasure;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);
                Assert.IsTrue(Global.GetScaleFromXML(Volume("2.5")));
                Assert.AreEqual(2.5, Global._UnitsPerPixel, 0);
                Assert.IsTrue(Global.GetScaleFromXML(Volume("0.0025")));
                Assert.AreEqual(0.0025, Global._UnitsPerPixel, 0);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
                Global._UnitsPerPixel = savedScale;
                Global._UnitOfMeasure = savedUnit;
            }
        }

        [TestMethod]
        public void GetScaleFromXML_CommaDecimalCulture_ParsesInvariantNumber() => AssertParsesUnderCulture("de-DE");

        [TestMethod]
        public void GetScaleFromXML_InvariantCulture_ParsesNumber() => AssertParsesUnderCulture("en-US");
    }
}

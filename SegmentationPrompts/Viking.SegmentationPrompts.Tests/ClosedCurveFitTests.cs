using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.SegmentationPrompts.Geometry;

namespace Viking.SegmentationPrompts.Tests
{
    [TestClass]
    public class ClosedCurveFitTests
    {
        /// <summary>The rings in <c>Fixtures/curve_fixtures.json</c>, fitted by the training exporter (make_curve_fixtures.py).</summary>
        internal static Dictionary<string, (List<Point> Control, List<Point> Fitted)> Fixtures()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "curve_fixtures.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            static List<Point> Ring(JsonElement ring) =>
                ring.EnumerateArray().Select(p => new Point(p[0].GetDouble(), p[1].GetDouble())).ToList();
            return document.RootElement.EnumerateObject().ToDictionary(
                entry => entry.Name,
                entry => (Ring(entry.Value.GetProperty("control")), Ring(entry.Value.GetProperty("fitted"))));
        }

        [TestMethod]
        public void EveryRingIsFittedAsTheTrainingExporterFittedIt()
        {
            foreach (var (name, (control, expected)) in Fixtures())
            {
                List<Point> fitted = ClosedCurveFit.FitClosedRing(control);

                Assert.AreEqual(expected.Count, fitted.Count, $"{name}: sample count");
                for (int i = 0; i < expected.Count; i++)
                {
                    // Python's math.dist and C#'s Math.Sqrt may differ in the last bit; nothing a raster can see.
                    Assert.AreEqual(expected[i].X, fitted[i].X, 1e-9, $"{name}[{i}].X");
                    Assert.AreEqual(expected[i].Y, fitted[i].Y, 1e-9, $"{name}[{i}].Y");
                }
            }
        }

        [TestMethod]
        public void AFittedRingIsClosedOnItsExactFirstPoint()
        {
            List<Point> fitted = ClosedCurveFit.FitClosedRing(Fixtures()["irregular"].Control);

            Assert.AreEqual(fitted[0].X, fitted[fitted.Count - 1].X);
            Assert.AreEqual(fitted[0].Y, fitted[fitted.Count - 1].Y);
        }
    }
}

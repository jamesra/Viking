using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using WebAnnotation.UI.AutoPolygonize;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Which circles of the same structure a result must be checked against for a merge: any circle within the result's
    /// average diameter of its outline, wherever it is. The result is a 10 x 10 square at the origin (area 100), so its
    /// average diameter is 2 * sqrt(100 / pi) = 11.28; a circle of radius 1 whose centre is x away from the nearest edge
    /// has a gap of x - 1.
    /// </summary>
    [TestClass]
    public class AutoPolygonizeMergeCandidateTests
    {
        private const long Structure = 7;
        private const int Section = 40;

        private static readonly Polygon Result = new([
            new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(0, 0)
        ]);

        private static AutoPolygonizeSelection.CircleFootprint Circle(
            long id, double x, double y, long? structure = Structure, int section = Section) =>
            new(id, structure, section, new Vector2(x, y), 1);

        private static List<long> Found(params AutoPolygonizeSelection.CircleFootprint[] circles) =>
            [.. AutoPolygonizeSelection.CollectMergeCandidates(Result, Structure, Section, circles, [1]).Select(c => c.Id)];

        [TestMethod]
        public void AverageDiameterIsThatOfTheCircleOfEqualArea()
        {
            Assert.AreEqual(11.2838, AutoPolygonizeSelection.AverageDiameter(Result), 1e-3);
        }

        [TestMethod]
        public void CircleWithinTheDiameterIsACandidateEvenFarOffScreen()
        {
            // Centre 10 from the edge: gap 9 < 11.28.
            CollectionAssert.AreEqual(new List<long> { 2 }, Found(Circle(2, 20, 5)));
        }

        [TestMethod]
        public void CircleBeyondTheDiameterIsNot()
        {
            // Centre 15 from the edge: gap 14 > 11.28.
            Assert.AreEqual(0, Found(Circle(2, 25, 5)).Count);
        }

        [TestMethod]
        public void CircleInsideTheResultIsACandidate()
        {
            CollectionAssert.AreEqual(new List<long> { 2 }, Found(Circle(2, 5, 5)));
        }

        [TestMethod]
        public void OtherStructureOtherSectionAndTheResultsOwnLocationsAreSkipped()
        {
            Assert.AreEqual(0, Found(
                Circle(2, 12, 5, structure: 8),
                Circle(3, 12, 5, section: Section + 1),
                Circle(1, 12, 5)).Count);
        }

        [TestMethod]
        public void ResultWithoutAStructureFindsNothing()
        {
            Assert.AreEqual(0, AutoPolygonizeSelection.CollectMergeCandidates(Result, null, Section, [Circle(2, 12, 5)], []).Count);
        }
    }
}

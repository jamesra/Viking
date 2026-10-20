using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// Pins the pure statics behind the pen "add control point" and "remove control point" actions for lines, curves
    /// and closed curves: <see cref="AddLineControlPointCommand.AddControlPoint"/> inserts into the nearest segment,
    /// <see cref="RemoveLineControlPointCommand.RemoveControlPoint"/> drops the nearest vertex and, for closed rings
    /// (first vertex repeated as the last), keeps the ring closed when the first vertex goes.
    /// </summary>
    [TestClass]
    public class LineControlPointTests
    {
        private static readonly Vector2[] Open = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];

        private static readonly Vector2[] Ring = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];

        private static void AssertPoints(Vector2[] expected, Vector2[] actual) =>
            CollectionAssert.AreEqual(expected, actual, "expected " + string.Join(";", expected) + " but was " + string.Join(";", actual));

        [TestMethod]
        public void AddInsertsIntoFirstSegment() =>
            AssertPoints([new(0, 0), new(5, 1), new(10, 0), new(10, 10), new(0, 10)],
                AddLineControlPointCommand.AddControlPoint(Open, new(5, 1), out _));

        [TestMethod]
        public void AddInsertsIntoMiddleSegment() =>
            AssertPoints([new(0, 0), new(10, 0), new(11, 5), new(10, 10), new(0, 10)],
                AddLineControlPointCommand.AddControlPoint(Open, new(11, 5), out _));

        [TestMethod]
        public void AddInsertsIntoLastSegmentBeforeTheEndpoint() =>
            AssertPoints([new(0, 0), new(10, 0), new(10, 10), new(5, 11), new(0, 10)],
                AddLineControlPointCommand.AddControlPoint(Open, new(5, 11), out _));

        [TestMethod]
        public void AddOnTwoPointLineInsertsBetweenTheEndpoints() =>
            AssertPoints([new(0, 0), new(3, 4), new(10, 0)],
                AddLineControlPointCommand.AddControlPoint([new(0, 0), new(10, 0)], new(3, 4), out _));

        [TestMethod]
        public void AddOnClosedRingKeepsTheClosingVertex()
        {
            Vector2[] result = AddLineControlPointCommand.AddControlPoint(Ring, new(5, 11), out _);
            AssertPoints([new(0, 0), new(10, 0), new(10, 10), new(5, 11), new(0, 10), new(0, 0)], result);
        }

        [TestMethod]
        public void AddDoesNotModifyTheInput()
        {
            Vector2[] copy = [.. Open];
            AddLineControlPointCommand.AddControlPoint(Open, new(5, 1), out _);
            AssertPoints(copy, Open);
        }

        [TestMethod]
        public void AddAtAnExistingVertexLeavesTheLineUnchanged() =>
            AssertPoints(Open,
                AddLineControlPointCommand.AddControlPoint(Open, new(10, 0), out _));

        [TestMethod]
        public void RemoveDropsFirstVertexOfOpenLine() =>
            AssertPoints([new(10, 0), new(10, 10), new(0, 10)],
                RemoveLineControlPointCommand.RemoveControlPoint(Open, new(0.5, 0.5), false));

        [TestMethod]
        public void RemoveDropsMiddleVertexOfOpenLine() =>
            AssertPoints([new(0, 0), new(10, 10), new(0, 10)],
                RemoveLineControlPointCommand.RemoveControlPoint(Open, new(9, 1), false));

        [TestMethod]
        public void RemoveDropsLastVertexOfOpenLine() =>
            AssertPoints([new(0, 0), new(10, 0), new(10, 10)],
                RemoveLineControlPointCommand.RemoveControlPoint(Open, new(1, 9), false));

        [TestMethod]
        public void RemoveFirstVertexOfClosedRingReclosesOnTheNewFirst() =>
            AssertPoints([new(10, 0), new(10, 10), new(0, 10), new(10, 0)],
                RemoveLineControlPointCommand.RemoveControlPoint(Ring, new(1, 1), true));

        [TestMethod]
        public void RemoveMiddleVertexOfClosedRingKeepsBothEndsOfTheRing() =>
            AssertPoints([new(0, 0), new(10, 0), new(0, 10), new(0, 0)],
                RemoveLineControlPointCommand.RemoveControlPoint(Ring, new(9, 9), true));

        [TestMethod]
        public void RemoveNearTheClosingVertexTargetsTheFirstVertex()
        {
            // The ring's first and last vertex coincide; the first one wins the tie, so the ring is re-closed.
            Vector2[] result = RemoveLineControlPointCommand.RemoveControlPoint(Ring, new(0, 0), true);
            Assert.AreEqual(result[0], result[result.Length - 1]);
            Assert.AreEqual(4, result.Length);
        }

        [TestMethod]
        public void RemoveFirstVertexOfOpenLineDoesNotCloseIt()
        {
            Vector2[] result = RemoveLineControlPointCommand.RemoveControlPoint(Open, new(0, 0), false);
            Assert.AreNotEqual(result[0], result[result.Length - 1]);
        }

        [TestMethod]
        public void RemoveEquidistantPicksTheEarlierVertex() =>
            AssertPoints([new(10, 0), new(20, 0)],
                RemoveLineControlPointCommand.RemoveControlPoint([new(0, 0), new(10, 0), new(20, 0)], new(5, 0), false));

        [TestMethod]
        public void RemoveDoesNotModifyTheInput()
        {
            Vector2[] copy = [.. Ring];
            RemoveLineControlPointCommand.RemoveControlPoint(Ring, new(1, 1), true);
            AssertPoints(copy, Ring);
        }
    }
}

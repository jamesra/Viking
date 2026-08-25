using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;

namespace MorphologyMeshTest
{
    [TestClass]
    public class ConnectContoursTests
    {
        /// <summary>
        /// Two open fragments whose joiners are the diagonals of a square must reverse the incoming
        /// fragment so the assembled perimeter does not hourglass. Enumerable.Reverse used to leave
        /// the copy in original order because the result was discarded.
        /// </summary>
        [TestMethod]
        public void OrderContourToAvoidCrossing_ReversesIncomingWhenJoinersCross()
        {
            PolygonIndex first = new(0, 0, 2);
            PolygonIndex second = new(0, 1, 2);
            PolygonIndex[] incoming = [first, second];

            PolygonIndex[] ordered = MorphMeshRegion.OrderContourToAvoidCrossing(
                incoming,
                previousStart: new Vector2(0, 0),
                previousEnd: new Vector2(1, 0),
                incomingStart: new Vector2(0, 1),
                incomingEnd: new Vector2(1, 1));

            Assert.AreEqual(second, ordered[0], "Crossing joiners must start the incoming fragment at its former end.");
            Assert.AreEqual(first, ordered[1], "Crossing joiners must reverse vertex order, not leave Enumerable.Reverse as a no-op.");
            Assert.AreNotSame(incoming, ordered);
        }

        /// <summary>
        /// Parallel joiners (a rectangle, not an X) must keep the incoming fragment in the given order.
        /// </summary>
        [TestMethod]
        public void OrderContourToAvoidCrossing_KeepsOrderWhenJoinersDoNotCross()
        {
            PolygonIndex first = new(0, 0, 2);
            PolygonIndex second = new(0, 1, 2);
            PolygonIndex[] incoming = [first, second];

            PolygonIndex[] ordered = MorphMeshRegion.OrderContourToAvoidCrossing(
                incoming,
                previousStart: new Vector2(0, 0),
                previousEnd: new Vector2(1, 0),
                incomingStart: new Vector2(1, 1),
                incomingEnd: new Vector2(0, 1));

            Assert.AreSame(incoming, ordered);
            Assert.AreEqual(first, ordered[0]);
            Assert.AreEqual(second, ordered[1]);
        }
    }
}

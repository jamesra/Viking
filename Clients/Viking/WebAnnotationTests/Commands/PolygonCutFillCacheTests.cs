using Geometry;
using Geometry.Meshing;
using GeometryTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebAnnotation.UI.Commands;

namespace WebAnnotationTests.Commands
{
    /// <summary>
    /// <see cref="PolygonCutFillCache.Begin"/> starts one background triangulation per polygon instance and
    /// must triangulate a snapshot, so a pen edit of the live ring after Begin cannot change the mesh.
    /// </summary>
    [TestClass]
    public class PolygonCutFillCacheTests
    {
        /// <summary>Upper bound for a box triangulation; a hang fails the test instead of the run.</summary>
        private const int TimeoutMs = 30000;

        private static PolygonCutFill Await(Task<PolygonCutFill> task)
        {
            Assert.IsTrue(task.Wait(TimeoutMs), "triangulation did not finish");
            return task.Result;
        }

        private static void AssertSameMesh(PolygonCutFill expected, PolygonCutFill actual)
        {
            Assert.AreEqual(expected.Centroid, actual.Centroid);
            CollectionAssert.AreEqual(expected.Positions.ToArray(), actual.Positions.ToArray());
            CollectionAssert.AreEqual(expected.TriangleIndices.ToArray(), actual.TriangleIndices.ToArray());
        }

        [TestMethod, Timeout(60000)]
        public void NullPolygonThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => PolygonCutFillCache.Begin(null));
        }

        [TestMethod, Timeout(60000)]
        public void SamePolygonInstanceReturnsSameTask()
        {
            Polygon polygon = Primitives.BoxPolygon(10);

            var first = PolygonCutFillCache.Begin(polygon);
            var second = PolygonCutFillCache.Begin(polygon);

            Assert.AreSame(first, second);
            Await(first);
        }

        [TestMethod, Timeout(60000)]
        public void DifferentInstancesWithEqualGeometryReturnDifferentTasks()
        {
            var first = PolygonCutFillCache.Begin(Primitives.BoxPolygon(10));
            var second = PolygonCutFillCache.Begin(Primitives.BoxPolygon(10));

            Assert.AreNotSame(first, second);
            Await(first);
            Await(second);
        }

        [TestMethod, Timeout(60000)]
        public void ResultMatchesCreateOfClone()
        {
            Polygon polygon = Primitives.BoxPolygon(10);
            PolygonCutFill expected = PolygonCutFill.Create((Polygon)polygon.Clone());

            PolygonCutFill actual = Await(PolygonCutFillCache.Begin(polygon));

            Assert.IsTrue(actual.TriangleCount > 0);
            AssertSameMesh(expected, actual);
        }

        [TestMethod, Timeout(60000)]
        public void MutatingLivePolygonAfterBeginLeavesMeshUnchanged()
        {
            Polygon polygon = Primitives.BoxPolygon(10);
            PolygonCutFill expected = PolygonCutFill.Create((Polygon)polygon.Clone());

            var task = PolygonCutFillCache.Begin(polygon);
            polygon.ExteriorRing = Primitives.BoxPolygon(500).ExteriorRing; // the pen edits the live ring

            AssertSameMesh(expected, Await(task));
            Assert.AreSame(task, PolygonCutFillCache.Begin(polygon), "cache entry is keyed by instance, not by ring contents");
        }
    }
}

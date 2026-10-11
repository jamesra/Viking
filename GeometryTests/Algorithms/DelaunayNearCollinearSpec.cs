using FsCheck;
using Geometry;
using Geometry.Meshing;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace GeometryTests.Algorithms
{
    /// <summary>
    /// Delaunay merge with a column of exactly collinear vertices and one vertex a fraction of a pixel off
    /// that column. The circumcircle of such a near-collinear triple has a radius of ~10^5 px, so the next
    /// column vertex can sit less than <see cref="Tolerance.Epsilon"/> outside it. Treating that band as
    /// inside used to delete a column edge and later build an edge through a column vertex
    /// (<see cref="ArgumentException"/> from <see cref="Circle.CircleFromThreePoints(Vector2, Vector2, Vector2)"/>
    /// or <see cref="EdgesIntersectTriangulationException"/>).
    /// </summary>
    [TestClass]
    public class DelaunayNearCollinearSpec
    {
        /// <summary>Shrunk from baseline TriangulatePolygonTestWithInteriorPoints: column 1-2-3 at one X, vertex 4 0.14 px right of it.</summary>
        private static readonly Vector2[] FivePointColumn =
        [
            new(-123.37591574838498, -74.17107038206481),
            new(61.62408425161502, -49.17107038206482),
            new(61.62408425161502, -34.17107038206482),
            new(61.62408425161502, -29.17107038206482),
            new(61.76858719645466, 123.34621647486375),
        ];

        /// <summary>Second shrink of the same seed once the candidate-removal test was fixed: the merge step picked the edge 1-3 through vertex 2.</summary>
        private static readonly Vector2[] SixPointColumn =
        [
            new(-92.53115540438048, -93.15473725500021),
            new(92.46884459561952, -68.15473725500021),
            new(92.46884459561952, -53.15473725500021),
            new(92.46884459561952, -48.15473725500021),
            new(92.61334754045916, 104.36254960192835),
            new(-102.53115540438048, 51.84526274499979),
        ];

        [TestMethod]
        public void FivePointColumnKeepsEveryColumnEdge() => AssertColumnTriangulation(FivePointColumn, columnStart: 1, columnLength: 3);

        [TestMethod]
        public void SixPointColumnKeepsEveryColumnEdge() => AssertColumnTriangulation(SixPointColumn, columnStart: 1, columnLength: 3);

        /// <summary>
        /// Consecutive column vertices are Gabriel neighbors by construction (no other vertex within the circle
        /// on their gap as diameter), so every Delaunay triangulation contains each column edge and no edge may
        /// skip over a column vertex.
        /// </summary>
        [TestMethod]
        public void ColumnWithNearCollinearNeighborTriangulates() =>
            CoreCheck.Run(
                Prop.ForAll(Arb.From(NearCollinearColumn()), sample =>
                {
                    (Vector2[] points, int columnLength) = sample;
                    try
                    {
                        TriangulationMesh<IVertex2D> mesh = Triangulate(points);
                        bool columnEdges = Enumerable.Range(0, columnLength - 1).All(i => mesh.Contains(new EdgeKey(i, i + 1)));
                        return (mesh.AnyMeshEdgesIntersect() == false).Label("Edges intersect")
                            .And(columnEdges.Label("Missing column edge"))
                            .And(mesh.AreTriangulatedFacesCCW().Label("Face is clockwise"))
                            .And((mesh.AreTriangulatedFacesColinear() == false).Label("Face is colinear"))
                            .Label(Describe(points));
                    }
                    catch (Exception e)
                    {
                        return false.Label(e.GetType().Name + ": " + e.Message).Label(Describe(points));
                    }
                }),
                nameof(ColumnWithNearCollinearNeighborTriangulates));

        /// <summary>
        /// Column vertices first (indices 0..k-1, increasing along the column, shared exact X), then one vertex
        /// 0.01-1 px off the column 50-200 px past its end, then 1-3 vertices 50+ px to one side. Real-magnitude
        /// origin up to ±1000 px; optionally transposed so the column is horizontal (shared exact Y).
        /// </summary>
        private static Gen<(Vector2[] points, int columnLength)> NearCollinearColumn() =>
            from x0 in Gen.Choose(-100000, 100000).Select(i => i / 100.0)
            from y0 in Gen.Choose(-100000, 100000).Select(i => i / 100.0)
            from gaps in Gen.Choose(2, 5).SelectMany(n => Gen.ArrayOf(n, Gen.Choose(100, 2000).Select(i => i / 100.0)))
            from offX in Gen.Choose(1, 100).Select(i => i / 100.0)
            from offSide in Gen.Elements(-1.0, 1.0)
            from offY in Gen.Choose(50, 200)
            from sideCount in Gen.Choose(1, 3)
            from sideXs in Gen.ArrayOf(sideCount, Gen.Choose(0, 30))
            from sideYs in Gen.ArrayOf(sideCount, Gen.Choose(-100, 100))
            from transpose in Gen.Elements(false, true)
            select Build(x0, y0, gaps, offSide * offX, offY, sideXs, sideYs, transpose);

        private static (Vector2[] points, int columnLength) Build(double x0, double y0, double[] gaps, double offX, int offY, int[] sideXs, int[] sideYs, bool transpose)
        {
            double[] columnY = new double[gaps.Length + 1];
            columnY[0] = y0;
            for (int i = 0; i < gaps.Length; i++)
                columnY[i + 1] = columnY[i] + gaps[i];

            double top = columnY[columnY.Length - 1];
            Vector2[] points =
            [
                .. columnY.Select(y => new Vector2(x0, y)),
                new(x0 + offX, top + offY),
                .. sideXs.Select((dx, j) => new Vector2(x0 - 50 - (40 * j) - dx, y0 + sideYs[j])),
            ];

            if (transpose)
                points = [.. points.Select(p => new Vector2(p.Y, p.X))];

            return (points, columnY.Length);
        }

        private static TriangulationMesh<IVertex2D> Triangulate(Vector2[] points) =>
            GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh([.. points.Select((p, i) => (IVertex2D)new Vertex2D(i, p))]);

        private static void AssertColumnTriangulation(Vector2[] points, int columnStart, int columnLength)
        {
            TriangulationMesh<IVertex2D> mesh = Triangulate(points);

            Assert.IsFalse(mesh.AnyMeshEdgesIntersect(), "Edges intersect");
            Assert.IsFalse(mesh.AreTriangulatedFacesColinear(), "Face is colinear");
            Assert.IsTrue(mesh.AreTriangulatedFacesCCW(), "Face is clockwise");
            for (int i = columnStart; i < columnStart + columnLength - 1; i++)
                Assert.IsTrue(mesh.Contains(new EdgeKey(i, i + 1)), $"Missing column edge {i}-{i + 1}");
            Assert.IsFalse(mesh.Contains(new EdgeKey(columnStart, columnStart + columnLength - 1)), "Edge skips over a column vertex");
        }

        private static string Describe(Vector2[] points) => string.Join("; ", points.Select(p => p.X.ToString("R") + "," + p.Y.ToString("R")));
    }
}

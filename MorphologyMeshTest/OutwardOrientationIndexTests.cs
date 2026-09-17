using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MorphologyMesh;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMeshTest
{
    /// <summary>
    /// Pins the Z-bucketed shape lookup used by the outward-winding pass against the linear filter it replaced.
    ///
    /// The orientation pass asks "does any contour on this side, at roughly this Z, contain the face centre?" once
    /// per face.  That was a linear scan of every accumulated contour, which on an assembled composite pairs a
    /// six-figure face count with a four-figure shape count.  The bucketed form must select exactly the same
    /// shapes, or the winding decisions change and the reconstruction changes with them.
    /// </summary>
    [TestClass]
    public class OutwardOrientationIndexTests
    {
        /// <summary>The tolerance the production caller uses; the index is only equivalent for a fixed tolerance.</summary>
        private static double ZTol => Math.Max(Global.Epsilon * 1000, 0.5);

        private static Polygon SquareAt(double centerX, double centerY, double halfWidth) =>
            new(
            [
                new Vector2(centerX - halfWidth, centerY - halfWidth),
                new Vector2(centerX + halfWidth, centerY - halfWidth),
                new Vector2(centerX + halfWidth, centerY + halfWidth),
                new Vector2(centerX - halfWidth, centerY + halfWidth),
                new Vector2(centerX - halfWidth, centerY - halfWidth),
            ]);

        /// <summary>
        /// Contours at several Z on both sides, including the same instance repeated at the same Z, which is what
        /// adjacent slices actually contribute to the accumulated list.
        /// </summary>
        private static List<MorphMeshOutwardOrientation.ShapeAtZ> BuildShapes()
        {
            List<MorphMeshOutwardOrientation.ShapeAtZ> shapes = [];

            for (int z = 0; z <= 3; z++)
            {
                Polygon lower = SquareAt(z * 10, 0, 4);
                Polygon upper = SquareAt(z * 10, 20, 4);

                foreach (bool isUpper in new[] { false, true })
                {
                    Polygon shape = isUpper ? upper : lower;

                    //Added twice on purpose: the accumulated list carries each shared contour once per slice.
                    shapes.Add(new MorphMeshOutwardOrientation.ShapeAtZ { Shape = shape, IsUpper = isUpper, Z = z });
                    shapes.Add(new MorphMeshOutwardOrientation.ShapeAtZ { Shape = shape, IsUpper = isUpper, Z = z });
                }
            }

            //A second, disjoint contour sharing a Z with an existing one, so a bucket holds more than one shape.
            shapes.Add(new MorphMeshOutwardOrientation.ShapeAtZ { Shape = SquareAt(100, 0, 4), IsUpper = false, Z = 2 });

            //A half-integer Z, so bucket boundaries are exercised against the 0.5 tolerance.
            shapes.Add(new MorphMeshOutwardOrientation.ShapeAtZ { Shape = SquareAt(0, 40, 4), IsUpper = true, Z = 1.5 });

            return shapes;
        }

        private static bool LinearScanReference(
            IReadOnlyList<MorphMeshOutwardOrientation.ShapeAtZ> shapes,
            bool isUpper, double faceZ, double zTol, Vector2 point) =>
            shapes
                .Where(s => s.IsUpper == isUpper && Math.Abs(s.Z - faceZ) <= zTol)
                .Any(s => s.Shape.GetRelation((IPoint2D)point) == ShapeRelation.Contained);

        [TestMethod]
        public void AnyShapeContains_MatchesLinearScanAcrossZAndSides()
        {
            List<MorphMeshOutwardOrientation.ShapeAtZ> shapes = BuildShapes();
            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(shapes, new Dictionary<int, bool>());

            double[] faceZValues = [-2, -0.5, 0, 0.4, 0.5, 0.6, 1, 1.25, 1.5, 2, 2.5, 3, 3.5, 4, 10];
            double[] coords = [-10, -4, -2, 0, 2, 4, 6, 10, 18, 20, 22, 30, 40, 100];

            int comparisons = 0;
            foreach (bool isUpper in new[] { false, true })
            {
                foreach (double faceZ in faceZValues)
                {
                    foreach (double x in coords)
                    {
                        foreach (double y in coords)
                        {
                            Vector2 point = new(x, y);
                            bool expected = LinearScanReference(shapes, isUpper, faceZ, ZTol, point);
                            bool actual = ctx.AnyShapeContains(isUpper, faceZ, ZTol, (IPoint2D)point);

                            Assert.AreEqual(expected, actual,
                                $"Index disagreed with the linear scan for isUpper={isUpper}, faceZ={faceZ}, point=({x},{y}).");
                            comparisons++;
                        }
                    }
                }
            }

            //Guards against the assertions silently never running if the fixture loops are edited.
            Assert.AreEqual(faceZValues.Length * coords.Length * coords.Length * 2, comparisons);
        }

        /// <summary>
        /// The whole point of the change is that a query stops depending on how many shapes were accumulated.
        /// Repeating the same contours must not change any answer.
        /// </summary>
        [TestMethod]
        public void AnyShapeContains_IsUnaffectedByDuplicateShapeEntries()
        {
            List<MorphMeshOutwardOrientation.ShapeAtZ> once = BuildShapes();
            List<MorphMeshOutwardOrientation.ShapeAtZ> many = [.. once, .. once, .. once];

            var ctxOnce = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(once, new Dictionary<int, bool>());
            var ctxMany = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(many, new Dictionary<int, bool>());

            foreach (bool isUpper in new[] { false, true })
            {
                for (double faceZ = -1; faceZ <= 4; faceZ += 0.25)
                {
                    for (double x = -8; x <= 44; x += 2)
                    {
                        Vector2 point = new(x, 0);
                        Assert.AreEqual(
                            ctxOnce.AnyShapeContains(isUpper, faceZ, ZTol, (IPoint2D)point),
                            ctxMany.AnyShapeContains(isUpper, faceZ, ZTol, (IPoint2D)point),
                            $"Duplicated contours changed the answer at isUpper={isUpper}, faceZ={faceZ}, x={x}.");
                    }
                }
            }
        }

        [TestMethod]
        public void AnyShapeContains_EmptyContextContainsNothing()
        {
            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated([], new Dictionary<int, bool>());

            Assert.IsFalse(ctx.AnyShapeContains(true, 0, ZTol, (IPoint2D)new Vector2(0, 0)));
            Assert.IsFalse(ctx.AnyShapeContains(false, 0, ZTol, (IPoint2D)new Vector2(0, 0)));
        }

        /// <summary>
        /// A shape is selected on Z distance alone, so a contour on the opposite side must never satisfy a query
        /// even when it sits at the same Z and contains the point.
        /// </summary>
        [TestMethod]
        public void AnyShapeContains_DoesNotCrossSides()
        {
            Polygon shape = SquareAt(0, 0, 4);
            List<MorphMeshOutwardOrientation.ShapeAtZ> shapes =
            [
                new() { Shape = shape, IsUpper = false, Z = 5 }
            ];

            var ctx = MorphMeshOutwardOrientation.ShapeContext.FromAccumulated(shapes, new Dictionary<int, bool>());
            IPoint2D inside = (IPoint2D)new Vector2(0, 0);

            Assert.IsTrue(ctx.AnyShapeContains(false, 5, ZTol, inside), "The lower contour should match a lower query.");
            Assert.IsFalse(ctx.AnyShapeContains(true, 5, ZTol, inside), "A lower contour must not satisfy an upper query.");
        }
    }
}

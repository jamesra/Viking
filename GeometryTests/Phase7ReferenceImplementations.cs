using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Verbatim copies of the Phase 7 code as it was before the allocation and speed changes, so the equivalence tests
    /// can require the new code to return the same arrays, in the same order, with bit-identical doubles.
    /// Do not "improve" anything in this file: its only job is to stay the same.
    /// </summary>
    internal static class Phase7ReferenceImplementations
    {
        #region Edge and face hashes

        /// <summary>The EdgeKey and EndpointPair hash before Phase 7.</summary>
        public static int OldEdgeHash(int a, int b) => (int)(((long)a * (long)b) & int.MaxValue);

        /// <summary>The Face hash before Phase 7.</summary>
        public static int OldFaceHash(IFace face) => (int)(((ulong)face.iVerts[0] * (ulong)face.iVerts[1] * (ulong)face.iVerts[2]) & uint.MaxValue);

        #endregion

        #region Delaunay2D (Bowyer-Watson)

        /// <summary>
        /// Delaunay2D.Triangulate(Vector2[], Vector2[]) as it was before Phase 7: RemoveAt in the middle of the triangle
        /// list and pairwise edge de-duplication.
        /// </summary>
        public static int[] DelaunayTriangulate(Vector2[] points, Vector2[] BoundingPoints)
        {
            if (BoundingPoints is null)
            {
                throw new ArgumentNullException(nameof(BoundingPoints));
            }

            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Length < 3)
                return [];

            Vector2[] sortedPoints = (Vector2[])points.Clone();
            Array.Sort(sortedPoints, new Vector2Comparer(xyOrder: true));

            for (int i = 1; i < sortedPoints.Length; i++)
            {
                if (Vector2.Distance(in sortedPoints[i - 1], in sortedPoints[i]) < Geometry.Global.Epsilon)
                    throw new ArgumentException($"Duplicate points, this breaks delaunay: #{i - 1} and #{i}");
            }

            points = sortedPoints;

            List<GridIndexTriangle> triangles = new(points.Length);
            List<GridIndexTriangle> safeTriangles = [];

            int iNumPoints = points.Length;
            Vector2[] allpoints = new Vector2[iNumPoints + 4];

            points.CopyTo(allpoints, 0);
            BoundingPoints.CopyTo(allpoints, iNumPoints);

            triangles.AddRange([new(iNumPoints, iNumPoints + 1, iNumPoints + 2, ref allpoints),
                                new(iNumPoints + 1, iNumPoints + 2, iNumPoints + 3, ref allpoints)]);

            IndexEdge[] Edges = new IndexEdge[(triangles.Count * 3) * 2];
            for (int iPoint = 0; iPoint < points.Length; iPoint++)
            {
                Vector2 P = points[iPoint];

                int maxEdges = triangles.Count * 3;
                if (Edges.Length < maxEdges)
                    Edges = new IndexEdge[maxEdges * 2];

                int iTri = 0;
                int iEdge = 0;
                while (iTri < triangles.Count)
                {
                    GridIndexTriangle tri = triangles[iTri];
                    Circle circle = tri.Circle;
                    if (circle.Covers(in P))
                    {
                        Edges[iEdge++] = new IndexEdge(tri.i1, tri.i2);
                        Edges[iEdge++] = new IndexEdge(tri.i2, tri.i3);
                        Edges[iEdge++] = new IndexEdge(tri.i3, tri.i1);
                        triangles.RemoveAt(iTri);
                    }
                    else if (circle.Center.X + circle.Radius + Geometry.Global.Epsilon < P.X)
                    {
                        safeTriangles.Add(tri);
                        triangles.RemoveAt(iTri);
                    }
                    else
                    {
                        iTri++;
                    }
                }

                int numEdges = iEdge;

                for (int iA = 0; iA < numEdges; iA++)
                {
                    if (Edges[iA].IsValid == false)
                        continue;

                    for (int iB = iA + 1; iB < numEdges; iB++)
                    {
                        if (Edges[iB].IsValid == false)
                            continue;

                        if (Edges[iA] == Edges[iB])
                        {
                            Edges[iB].IsValid = false;
                            Edges[iA].IsValid = false;
                            break;
                        }
                    }
                }

                for (iEdge = 0; iEdge < numEdges; iEdge++)
                {
                    IndexEdge E = Edges[iEdge];

                    if (!E.IsValid)
                        continue;

                    GridIndexTriangle newTri = new(E.iA, E.iB, iPoint, ref allpoints);
                    triangles.Add(newTri);
                }
            }

            triangles.AddRange(safeTriangles);

            for (int iTri = 0; iTri < triangles.Count; iTri++)
            {
                GridIndexTriangle tri = triangles[iTri];
                if (tri.i1 >= iNumPoints ||
                   tri.i2 >= iNumPoints ||
                   tri.i3 >= iNumPoints)
                {
                    triangles.RemoveAt(iTri);
                    iTri--;
                }
            }

            int[] TriangleIndicies = new int[triangles.Count * 3];
            for (int iTri = 0; iTri < triangles.Count; iTri++)
            {
                GridIndexTriangle tri = triangles[iTri];
                int iPoint = iTri * 3;
                TriangleIndicies[iPoint] = tri.i1;
                TriangleIndicies[iPoint + 1] = tri.i2;
                TriangleIndicies[iPoint + 2] = tri.i3;
            }

            return TriangleIndicies;
        }

        /// <summary>Delaunay2D.GetCorners as it was.</summary>
        public static Vector2[] DelaunayCorners(Vector2[] points)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                minX = points[i].X < minX ? points[i].X : minX;
                maxX = points[i].X > maxX ? points[i].X : maxX;
                minY = points[i].Y < minY ? points[i].Y : minY;
                maxY = points[i].Y > maxY ? points[i].Y : maxY;
            }

            double width = maxX - minX;
            double height = maxY - minY;

            minX -= width;
            maxX += width;
            minY -= height;
            maxY += height;

            return [new(minX, minY), new(maxX, minY), new(minX, maxY), new(maxX, maxY)];
        }

        #endregion

        #region Catmull-Rom

        private static double tj(double ti, in Vector2 Pi, in Vector2 Pj, double Alpha = 0.5) => Math.Pow(Vector2.Distance(in Pi, in Pj), Alpha) + ti;

        /// <summary>CatmullRom.FitCurveSegmentWithTValues as it was: thirteen LINQ arrays per call.</summary>
        public static Vector2[] FitCurveSegmentWithTValues(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, double[] tvalues)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            double t3 = tj(t2, p2, p3, alpha);

            double[] A1X = [.. tvalues.Select(t => (t1 - t) / (t1 - t0) * p0.X + (t - t0) / (t1 - t0) * p1.X)];
            double[] A1Y = [.. tvalues.Select(t => (t1 - t) / (t1 - t0) * p0.Y + (t - t0) / (t1 - t0) * p1.Y)];

            double[] A2X = [.. tvalues.Select(t => (t2 - t) / (t2 - t1) * p1.X + (t - t1) / (t2 - t1) * p2.X)];
            double[] A2Y = [.. tvalues.Select(t => (t2 - t) / (t2 - t1) * p1.Y + (t - t1) / (t2 - t1) * p2.Y)];

            double[] A3X = [.. tvalues.Select(t => (t3 - t) / (t3 - t2) * p2.X + (t - t2) / (t3 - t2) * p3.X)];
            double[] A3Y = [.. tvalues.Select(t => (t3 - t) / (t3 - t2) * p2.Y + (t - t2) / (t3 - t2) * p3.Y)];

            double[] B1X = [.. tvalues.Select((t, i) => ((t2 - t) / (t2 - t0)) * A1X[i] + ((t - t0) / (t2 - t0)) * A2X[i])];
            double[] B1Y = [.. tvalues.Select((t, i) => ((t2 - t) / (t2 - t0)) * A1Y[i] + ((t - t0) / (t2 - t0)) * A2Y[i])];

            double[] B2X = [.. tvalues.Select((t, i) => ((t3 - t) / (t3 - t1)) * A2X[i] + ((t - t1) / (t3 - t1)) * A3X[i])];
            double[] B2Y = [.. tvalues.Select((t, i) => ((t3 - t) / (t3 - t1)) * A2Y[i] + ((t - t1) / (t3 - t1)) * A3Y[i])];

            double[] CX = [.. tvalues.Select((t, i) => ((t2 - t) / (t2 - t1)) * B1X[i] + ((t - t1) / (t2 - t1)) * B2X[i])];
            double[] CY = [.. tvalues.Select((t, i) => ((t2 - t) / (t2 - t1)) * B1Y[i] + ((t - t1) / (t2 - t1)) * B2Y[i])];

            return [.. CX.Select((cx, i) => new Vector2(cx, CY[i]))];
        }

        /// <summary>CatmullRom.FitCurveSegment(p0..p3, int) as it was.</summary>
        public static Vector2[] FitCurveSegment(in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3, int NumInterpolations)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, in p0, in p1, alpha);
            double t2 = tj(t1, in p1, in p2, alpha);

            double[] tvalues = new double[NumInterpolations];

            double[] tPointsArray = NumInterpolations == 1 ? [0.5] : [.. tvalues.Select((t, i) => ((double)i / ((double)NumInterpolations - 1.0)))];
            tvalues = [.. tPointsArray.Select((t, i) => t1 + tPointsArray[i] * (t2 - t1))];

            return FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);
        }

        /// <summary>CatmullRom.FitCurveSegment(p0..p3, double[]) as it was.</summary>
        public static Vector2[] FitCurveSegment(in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3, double[] tPointsArray)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, in p0, in p1, alpha);
            double t2 = tj(t1, in p1, in p2, alpha);

            double[] tvalues = TScalarsToTValues(tPointsArray, t1, t2);

            return FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);
        }

        /// <summary>CatmullRom.RecursivelyFitCurveSegment(p0..p3, uint) as it was.</summary>
        public static Vector2[] RecursivelyFitCurveSegment(in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3, uint NumInterpolations = 5)
        {
            double[] tvalues = new double[NumInterpolations];
            SortedSet<double> tPoints = [.. tvalues.Select((t, i) => ((double)i / ((double)NumInterpolations - 1.0)))];

            return RecursivelyFitCurveSegment(in p0, in p1, in p2, in p3, tPoints);
        }

        /// <summary>CatmullRom.RecursivelyFitCurveSegment(p0..p3, SortedSet) as it was.</summary>
        public static Vector2[] RecursivelyFitCurveSegment(in Vector2 p0, in Vector2 p1, in Vector2 p2, in Vector2 p3, SortedSet<double> tPoints)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, in p0, in p1, alpha);
            double t2 = tj(t1, in p1, in p2, alpha);
            double t3 = tj(t2, in p2, in p3, alpha);

            double[] tPointsArray = [.. tPoints];
            double[] tvalues = TScalarsToTValues(tPoints.ToArray(), t1, t2);

            Vector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);

            if (!TryAddTPointsAboveThreshold(output, ref tPoints))
            {
                return output;
            }

            return RecursivelyFitCurveSegment(in p0, in p1, in p2, in p3, tPoints);
        }

        private static double[] TScalarsToTValues(IReadOnlyList<double> tpoints, double t1, double t2)
        {
            double[] tvalues = [.. tpoints.Select((t, i) => t1 + tpoints[i] * (t2 - t1))];
            return tvalues;
        }

        /// <summary>CurveExtensions.TryAddTPointsAboveThreshold as it was.</summary>
        public static bool TryAddTPointsAboveThreshold(Vector2[] output, ref SortedSet<double> TPoints, double angleThresholdInDegrees = 10.0)
        {
            double[] TPointsArray = [.. TPoints];

            for (int i = 1; i < output.Length - 1; i++)
            {
                if (Vector2.DistanceSquared(in output[i - 1], in output[i]) < Tolerance.EpsilonSquared ||
                   Vector2.DistanceSquared(in output[i], in output[i + 1]) < Tolerance.EpsilonSquared)
                {
                    output = output.RemoveAt(i);
                    TPoints.Remove(TPointsArray[i]);
                    TPointsArray = TPointsArray.RemoveAt(i);

                    i--;
                }
            }

            double[] degrees;

            degrees = output.MeasureCurvature();
            degrees = [.. degrees.Select(d => Math.Abs(d))];

            const double onedegree = (Math.PI * 2.0 / 360);
            double threshold = onedegree * angleThresholdInDegrees;
            const double distance_threshold = 0.0625;

            int StartingPoints = TPointsArray.Length;
            bool[] NeedsInterpolation = [.. TPointsArray.Select(t => false)];

            for (int i = TPointsArray.Length - 2; i > 0; i--)
            {
                if (degrees[i] > threshold)
                {
                    double distance = Vector2.DistanceSquared(in output[i - 1], in output[i]) + Vector2.DistanceSquared(in output[i], in output[i + 1]);
                    NeedsInterpolation[i] = distance > distance_threshold;
                }
            }

            for (int i = 0; i < NeedsInterpolation.Length - 1; i++)
            {
                if (NeedsInterpolation[i] || NeedsInterpolation[i + 1])
                {
                    double nextTValue = (TPointsArray[i] + TPointsArray[i + 1]) / 2.0;
                    TPoints.Add(nextTValue);
                }
            }

            int EndingPoints = TPoints.Count;

            return StartingPoints != EndingPoints;
        }

        #endregion

        #region Face path search

        /// <summary>MeshBase.AdjacentFaces as it was.</summary>
        public static IFace[] AdjacentFaces<VERTEX>(MeshBase<VERTEX> mesh, IFace face) where VERTEX : IVertex
            => [.. face.Edges.SelectMany(e => mesh[e].Faces.Where(f => f.Equals(face) == false))];

        /// <summary>MeshBase.FindFacesInPath(start, can, meets) as it was.</summary>
        public static List<IFace> FindFacesInPath<VERTEX>(MeshBase<VERTEX> mesh, IFace start, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> MeetsCriteriaFunc) where VERTEX : IVertex
        {
            SortedSet<IFace> testedFaces = [];
            Dictionary<IFace, List<IFace>> PathCache = [];
            return RecurseFacePath(ref testedFaces, mesh, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        /// <summary>MeshBase.FindFacesInPath(start, can, meets, ref checked) as it was.</summary>
        public static List<IFace> FindFacesInPath<VERTEX>(MeshBase<VERTEX> mesh, IFace start, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> MeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces) where VERTEX : IVertex
        {
            Dictionary<IFace, List<IFace>> PathCache = [];
            return RecurseFacePath(ref CheckedFaces, mesh, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        private static List<IFace> RecurseFacePath<VERTEX>(ref SortedSet<IFace> testedFaces, MeshBase<VERTEX> mesh, IFace Origin, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> IsMatch, Dictionary<IFace, List<IFace>> PathCache) where VERTEX : IVertex
        {
            testedFaces.Add(Origin);

            List<IFace> path =
            [
                Origin
            ];

            if (IsMatch(Origin))
                return path;

            if (PathCache.TryGetValue(Origin, out var pathCacheResult))
            {
                return pathCacheResult;
            }

            SortedSet<IFace> untestedFaces = [.. AdjacentFaces(mesh, Origin)];
            untestedFaces.ExceptWith(testedFaces);

            if (untestedFaces.Count == 0)
                return null;
            else if (untestedFaces.Count == 1)
            {
                IFace adjacentFace = untestedFaces.First();

                if (!CanBePartOfPath(adjacentFace))
                {
                    testedFaces.Add(adjacentFace);
                    return null;
                }

                List<IFace> result = RecurseFacePath(ref testedFaces, mesh, adjacentFace, CanBePartOfPath, IsMatch, PathCache);
                if (result is null)
                    return null;

                path.AddRange(result);
                PathCache[Origin] = path;
                return path;
            }
            else
            {
                List<List<IFace>> listPotentialPaths = new(untestedFaces.Count);
                SortedSet<IFace> AllBranchesTested = [];
                foreach (IFace adjacentFace in untestedFaces)
                {
                    if (testedFaces.Contains(adjacentFace))
                        continue;

                    if (!CanBePartOfPath(adjacentFace))
                    {
                        testedFaces.Add(adjacentFace);
                        continue;
                    }

                    SortedSet<IFace> testedFacesCopy = [.. testedFaces];
                    List<IFace> result = RecurseFacePath(ref testedFacesCopy, mesh, adjacentFace, CanBePartOfPath, IsMatch, PathCache);
                    if (result is null)
                    {
                        testedFaces.UnionWith(testedFacesCopy);
                        continue;
                    }

                    AllBranchesTested.UnionWith(testedFacesCopy);
                    listPotentialPaths.Add(result);
                }

                testedFaces.UnionWith(AllBranchesTested);

                if (listPotentialPaths.Count == 0)
                    return null;

                int MinDistance = listPotentialPaths.Select(L => L.Count).Min();
                List<IFace> shortestPath = listPotentialPaths.First(L => L.Count == MinDistance);
                path.AddRange(shortestPath);
                PathCache[Origin] = path;
                return path;
            }
        }

        #endregion

        #region EdgesByAngle

        /// <summary>GenericDelaunayMeshGenerator2D.EdgesByAngle as it was: a throwaway key array and Array.Sort(keys, items).</summary>
        public static EdgeAngle[] EdgesByAngle<VERTEX>(TriangulationMesh<VERTEX> mesh, IVertex2D Origin, long origin_edge_target, bool clockwise) where VERTEX : IVertex2D
        {
            Vector2 target = mesh[origin_edge_target].Position;
            MeshEdgeAngleComparerFixedIndex<VERTEX> angleComparer = new(mesh, Origin.Index, new Line(Origin.Position, target - Origin.Position), clockwise);

            List<long> edge_list = [.. Origin.Edges.Select(e => e.OppositeEnd((long)Origin.Index)).Where(e => e != origin_edge_target)];

            EdgeAngle[] edgeAngles = [.. edge_list.Select(edge => new EdgeAngle(Origin.Index, edge, angleComparer.MeasureAngle(edge), clockwise))];
            EdgeAngle[] edgeAnglesFiltered = [.. edgeAngles.Where(edge => edge.Angle >= 0 && edge.Angle < Math.PI)];

            Array.Sort(edgeAnglesFiltered.Select(e => e.Angle).ToArray(), edgeAnglesFiltered);

            return edgeAnglesFiltered;
        }

        #endregion

        #region Smoothing and Douglas-Peucker

        /// <summary>CurveSimplificationExtensions.ApplyKernel as it was.</summary>
        public static double[] ApplyKernel(double[] values, double[] kernel)
        {
            int HalfKernelLength = kernel.Length / 2;
            int iStart = HalfKernelLength;
            int iStop = values.Length - HalfKernelLength;

            double[] window = new double[kernel.Length];

            double[] output = new double[values.Length];

            for (int iCenter = iStart; iCenter < iStop; iCenter++)
            {
                Array.Copy(values, iCenter - HalfKernelLength, window, 0, kernel.Length);

                double updated_value = window.Select((v, i) => v * kernel[i]).Sum();
                output[iCenter] = updated_value;
            }

            for (int i = 0; i < iStart; i++)
            {
                output[i] = values[i];
            }

            for (int i = iStop; i < values.Length; i++)
            {
                output[i] = values[i];
            }

            return output;
        }

        /// <summary>Smoothing.Gaussian as it was.</summary>
        public static Vector2[] Gaussian(Vector2[] points)
        {
            double[] kernel = [0.25, 0.5, 0.25];

            int kernelRadius = (kernel.Length - 1) / 2;

            Vector2[] output = new Vector2[points.Length];

            for (int i = 0; i < kernelRadius; i++)
            {
                output[i] = points[i];
            }

            for (int i = points.Length - kernelRadius; i < points.Length; i++)
            {
                output[i] = points[i];
            }

            for (int i = kernelRadius; i < points.Length - kernelRadius; i++)
            {
                output[i] = ApplyKernelToIndex(points, kernel, i);
            }

            return output;
        }

        private static Vector2 ApplyKernelToIndex(Vector2[] points, double[] kernel, int iCenter)
        {
            int kernelRadius = (kernel.Length - 1) / 2;
            int KernelSize = (kernelRadius * 2) + 1;
            Vector2[] items = new Vector2[KernelSize];
            Array.Copy(points, iCenter - kernelRadius, items, 0, KernelSize);

            double X = items.Select((p, i) => p.X * kernel[i]).Sum();
            double Y = items.Select((p, i) => p.Y * kernel[i]).Sum();

            return new Vector2(X, Y);
        }

        /// <summary>The index selection inside DouglasPeuckerReduction(Points, Tolerance, PointsToPreserve) as it was.</summary>
        public static int[] PointsToPreserveIndices(IList<Vector2> Points, ICollection<Vector2> PointsToPreserve)
            => [.. PointsToPreserve.Where(p => Points.Contains(p)).Select(p => Points.IndexOf(p))];

        #endregion
    }
}


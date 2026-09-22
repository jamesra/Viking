//#define TRACEMESH

using FsCheck;
using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GeometryTests.FSCheck
{

    public class LineSegmentGenerators
    {
        public static Arbitrary<LineSegment> ArbRandomLine() => Arb.From(GenLine());

        public static Arbitrary<Polyline> ArbPolyLine() => Arb.From(Fresh());

        public static Gen<LineSegment> GenLine()
        {
            return Vector2Generators.Fresh()
                .Two()
                .Where(t => t.Item1 != t.Item2)
                .Select(t => new LineSegment(t.Item1, t.Item2));


            var coords = Arb.Default.NormalFloat().Generator.Four();
            return coords.Select(t => new LineSegment(new Vector2((double)t.Item1, (double)t.Item2),
                                                           new Vector2((double)t.Item3, (double)t.Item4)));
        }

        /*
        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        public static Gen<Vector2[]> GenOpenPolyline(int nLines)
        {
            int nPoints = nLines + 1;
            List<Vector2> polyLinePts = new List<Vector2>(nPoints);
            List<LineSegment> polyLine = new List<LineSegment>(nLines);

            while(polyLine.Count < nLines+1)
            {
                var pts = Vector2Generators.GenDistinctPoints(nPoints-polyLinePts.Count);
                pts.Where(array => array.Where((p) => 
                    {
                        if (polyLine.Count == 0)
                            return true;

                        LineSegment ls = new LineSegment(polyLinePts.Last(), p);
                        Polyline 
                        if(polyLine.IntersectionPoint(ls, true))
                    }
                )
                )
                polyLine.Add()
            }

            while (polyLine < polyLine.Count)
            {

            }

        }*/

        public static Gen<Polyline> Fresh() => Gen.Sized(size => GenOpenPolylineSmart(size));

        /*

        private static Gen<Polyline> GenOpenPolylineFast(int nLines)
        {
            //Oversample the points in the mesh to speed up the process of locating a non-intersecting path
            return (from points in Vector2Generators.GenDistinctPoints(nLines + 1)
                    from polyline in GenOpenPolylineFast(points)
                    select polyline);
        }

        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<Polyline> GenOpenPolylineFast(Vector2[] points)
        {
            if (points.Length <= 0)
            {
                return Gen.Constant<Polyline>(new Polyline());
            }
            else if(points.Length <= 3)
            {
                //Any 3 distinct points will not intersect
                return Gen.Constant<Polyline>(new Polyline(points));
            }
            else
            {
                var polyline = (from shuffled_points in Gen.Shuffle(points)
                              let lastPoint = shuffled_points.Last()
                              from polyline_subset in GenOpenPolylineFast(shuffled_points.ToList().GetRange(0, points.Length - 1).ToArray())
                              where polyline_subset.CanAdd(lastPoint)
                              select new Polyline(shuffled_points));
                return polyline;
            }
        }
        */

        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect.
        /// This was a slow process using arbitrary random points, so this implementation uses a mesh to increase 
        /// the odds and speed of generating a polyline for high numbers of lines
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<Polyline> GenOpenPolylineSmart(int nLines)
        {
            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in Vector2Generators.GenDistinctPoints((nLines + 1) * 3)
                    from polyline in GenOpenPolylineSmart(points, nLines > 50 ? (int)Math.Round(Math.Sqrt(nLines - 50) + 50) : nLines)
                    select polyline);
        }



        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Gen<Polyline> GenOpenPolylineSmart(Vector2[] points, int nLines)
        {
            if (points.Length <= 0)
            {
                return Gen.Constant<Polyline>(new Polyline());
            }
            else if (points.Length <= 3)
            {
                //Any 3 distinct points will not intersect
                return Gen.Constant<Polyline>(new Polyline(points));
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh([.. points.Select(p => new Vertex2D(p))]);

                //Use verticies on the convex hull as starting points for our search
                //var startingPoints = mesh.Edges.Values.Where(e => e.Faces.Count == 1).SelectMany(e => new int[] { e.A, e.B }).Distinct();

                foreach (var startingVert in mesh.Vertices)
                {
                    Stack<int> VertStack = new();
                    VertStack.Push(startingVert.Index);
                    bool FoundPath = FindNonSelfIntersectingPath(mesh,
                                                                ref VertStack,
                                                                meets_path_inclusion_criteria: (mesh_, path_, vert_) => path_.Contains(vert_) == false,
                                                                meets_path_completion_criteria: (mesh_, path_) => PathLengthCriteria(mesh_, path_, nLines));

                    if (FoundPath)
                    {
                        return Gen.Constant(new Polyline(VertStack.Select(v => mesh[v].Position)));
                    }
                }

                throw new Exception("Unexpectedly unable to generate path from a set of points");
                return Gen.Constant<Polyline>(new Polyline());
            }
        }

        /// <summary>
        /// This function yields an arbitrary non-intersecting path using edges of a triangulated mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="PathLength"></param>
        /// <returns></returns>
        private static bool FindNonSelfIntersectingPath(IReadOnlyMesh2D<IVertex2D> mesh, Stack<int> path, int nLines)
        {
            if (path.Count == nLines + 1)
            {
                //We have found a path to all verticies that does not self intersect
                return true;
            }

            Debug.Assert(nLines < mesh.Vertices.Count);
            int currentVert = path.Peek();
            IVertex2D vertex = mesh[currentVert];

            foreach (var edgeKey in vertex.Edges)
            {
                int candidateVert = edgeKey.OppositeEnd(vertex.Index);

                if (path.Contains(candidateVert))
                    continue;
                else
                {
                    path.Push(candidateVert);
                    bool foundPath = FindNonSelfIntersectingPath(mesh, path, nLines);
                    if (foundPath)
                    {
                        //We have found a path to all verticies that does not self intersect
                        return true;
                    }
                    else
                    {
                        //We cannot add this edge to the path and still connect all verticies
                        path.Pop();
                    }
                }
            }

            return false;
        }


        /// <summary>
        /// This function yields an arbitrary non-intersecting path using edges of a triangulated mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="PathLength"></param>
        /// <returns>True if Path meets the completion criteria</returns>
        private static bool FindNonSelfIntersectingPath(TriangulationMesh<IVertex2D> mesh,
                                                        ref Stack<int> path,
                                                        Func<TriangulationMesh<IVertex2D>, Stack<int>, int, bool> meets_path_inclusion_criteria,
                                                        Func<TriangulationMesh<IVertex2D>, Stack<int>, bool> meets_path_completion_criteria)
        {
            if (meets_path_completion_criteria(mesh, path))
            {
                //We have found a path to all verticies that does not self intersect
                return true;
            }

            int currentVert = path.Peek();
            IVertex2D vertex = mesh[currentVert];

            Gen<IEdgeKey[]> edgeKeyGen = Gen.OneOf(Gen.Shuffle(vertex.Edges));

            foreach (var edgeKey in edgeKeyGen.Eval(mesh.Vertices.Count, Global.StdGenSeed))
            {
                int candidateVert = edgeKey.OppositeEnd(currentVert);

                if (false == meets_path_inclusion_criteria(mesh, path, candidateVert))
                    continue;
                else
                {
                    path.Push(candidateVert);
                    bool path_found = FindNonSelfIntersectingPath(mesh, ref path, meets_path_inclusion_criteria, meets_path_completion_criteria);
                    if (path_found)
                    {
                        //We have found a path to all verticies that does not self intersect
                        return true;
                    }
                    else
                    {
                        //We cannot add this edge to the path and still connect all verticies
                        path.Pop();
                    }
                }
            }

            return false;
        }




        /// <summary>
        /// Return true if the path has length of at least N
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static bool PathLengthCriteria(TriangulationMesh<IVertex2D> mesh, Stack<int> path, int nLines) => path.Count == nLines + 1;

        /// <summary>
        /// Return true if the path can be closed using an edge in the mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static bool PathClosedCriteria(TriangulationMesh<IVertex2D> mesh, Stack<int> path)
        {
            IVertex2D last = mesh[path.Last()];
            IVertex2D first = mesh[path.First()];

            if (last.Edges.Contains(new EdgeKey(last.Index, first.Index)))
                return true;

            return false;
        }


        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect
        /// This is the first reliable implementation with no optimization. It runs too slowly for nLines > 15 or
        /// so.
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<Polyline> GenOpenPolyline(int nLines)
        {
            return (from points in Vector2Generators.GenDistinctPoints(nLines + 1)
                    from shuffled_points in Gen.Shuffle(points)
                    where LineSegment.SegmentsFromPoints(shuffled_points).SelfIntersects(LineSetOrdering.Polyline) == false
                    select new Polyline(shuffled_points));
        }

        /*
            int nPoints = nLines + 1;

            Polyline polyLine = new Polyline(nLines, AllowSelfIntersection: false);

            var pGen = Vector2Generators.Fresh();

            while (polyLine.LineCount < nLines)
            { 
                Gen.
                /*
                pGen.ArrayOf((nLines + 1) - polyLine.PointCount).Where(p =>
                {
                    if (polyLine.CanAdd(p) == false)
                        return false;
                    else
                    {
                        polyLine.Add(p);
                        return true;
                    }
                }
                );
                */
        /*
        var pts = Vector2Generators.GenDistinctPoints((nLines - polyLine.LineCount));
        pts.Select((arr) =>
            arr.Where((p) =>
            {
                if (polyLine.CanAdd(p) == false)
                    return false;
                else
                {
                    polyLine.Add(p);
                    return true;
                }
            }
        ));
        */
        /*
    }

    return Gen.Constant<Polyline>(polyLine);
}
*/

        /*
    /// <summary>
    /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
    /// </summary>
    /// <param name="nLines"></param>
    /// <returns></returns>
    internal static Gen<Polygon> GenClosedPolylineSmart(Vector2[] points, int nLines)
    {
        if (points.Length < 3)
        {
            throw new ArgumentException("Insufficient lines to form a closed shape.");
        }
        else if (points.Length == 3)
        {
            //Any 3 distinct points will not intersect
            return Gen.Constant<Polygon>(new Polygon(points.EnsureClosedRing()));
        }
        else
        {
            var mesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray());

            foreach (var startingVert in mesh.Vertices)
            {
                Stack<int> VertStack = new Stack<int>();
                VertStack.Push(startingVert.Index);
                bool FoundPath = FindNonSelfIntersectingPath(mesh,
                                                             ref VertStack, 
                                                             meets_path_inclusion_criteria: (mesh_,path_,vert_) => path_.Contains(vert_) == false,
                                                             meets_path_completion_criteria: (mesh_, path_) => PathLengthCriteria(mesh_, path_, nLines));

                if (FoundPath)
                {
                    //Check if we can close the shape
                    bool FoundClosure = FindNonSelfIntersectingPath(mesh,
                                                             ref VertStack,
                                                             meets_path_inclusion_criteria: (mesh_, path_, vert_) => path_.Contains(vert_) == false,
                                                             meets_path_completion_criteria: (mesh_, path_) => PathClosedCriteria(mesh_, path_));
                    if (FoundClosure)
                    {
                        return Gen.Constant(new Polygon(VertStack.Select(v => mesh[v].Position).ToArray().EnsureClosedRing() ));
                    }
                    else
                    {
                        VertStack.Pop();
                    }
                }
            }

            throw new Exception("Unexpectedly unable to generate path from a set of points");
            //return Gen.Constant<Polygon>(new Polygon());
        }
    }
    */

        /*
        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Gen<Polygon> GenConvexPolygonSmarter(Vector2[] points, int nLines)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Insufficient lines to form a closed shape.");
            }
            else if (points.Length == 3)
            {
                //Any 3 distinct points will not intersect
                var output = new Polygon(points.EnsureClosedRing());
                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                return Gen.Constant<Polygon>(output);
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray());

                var startingEdge = mesh.Edges.Values.First(e => e.Faces.Count == 1);
                var targetVert = startingEdge.B;
                var startVert = startingEdge.A;
                Stack<int> path = new Stack<int>();
                path.Push(startVert);
                var convexHull = FindNonSelfIntersectingPath(mesh, 
                                                             ref path,
                                                            (mesh_, path_, vert_) => path_ is null ? true :  mesh[new EdgeKey(path_.Peek(), vert_)].Faces.Count == 1 && (path.Contains(vert_) == false), //Edges can be included if they have one face
                                                            (mesh_, path_) => path_.Count > 2 && path_.Peek() == targetVert);

                path.Push(startVert); //Close the loop

                var output = new Polygon(path.Select(v => mesh[v].Position).ToArray());

                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                //TODO: Remove edges from Convex hull to generate a concave polygon of arbitrary size, doubles as a shrinker function?
                return Gen.Constant(output);

            }
        }
        */

        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Polygon GenConcavePolygonFromPoints(Vector2[] points, int nLines)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Insufficient lines to form a closed shape.");
            }
            else if (points.Length == 3)
            {
                //Any 3 distinct points will not intersect
                Polygon output = new(points.EnsureClosedRing());
                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                return output;
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh([.. points.Select(p => new Vertex2D(p))]);
                Polygon output = GenConcavePolygonFromMesh(mesh, nLines, false, out TriangulationMesh<IVertex2D> output_mesh);
                return output;

                /*
                var startingEdge = mesh.Edges.Values.First(e => e.Faces.Count == 1);
                var targetVert = startingEdge.B;
                var startVert = startingEdge.A;
                Stack<int> path = new Stack<int>();
                path.Push(startVert); 
                var convexHull = FindNonSelfIntersectingPath(mesh, 
                                                             ref path,
                                                            (mesh_, path_, vert_) => path_ is null ? true :  mesh[new EdgeKey(path_.Peek(), vert_)].Faces.Count == 1 && (path.Contains(vert_) == false), //Edges can be included if they have one face
                                                            (mesh_, path_) => path_.Count > 2 && path_.Peek() == targetVert);

                path.Push(startVert); //Close the loop

                Polygon initial_output = new Polygon(path.Select(v => mesh[v].Position).ToArray());
                if (initial_output.IsValid() == false)
                    throw new ArgumentException("Invalid polygon generated");

                Stack<int> concave_path = GenConcavePolygon(mesh, path, nLines);

                //TODO: Remove edges from Convex hull to generate a concave polygon of arbitrary size, doubles as a shrinker function?
                Polygon output = new Polygon(concave_path.Select(v => mesh[v].Position).ToArray());
                if (output.IsValid() == false)
                    throw new ArgumentException("Invalid polygon generated");

                return output;*/
            }
        }

        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect,
        /// and has a non-zero area
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Polygon GenConcavePolygonWithInteriorHolesFromPoints(Vector2[] points, int nLines)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Insufficient lines to form a closed shape.");
            }
            else if (points.Length == 3)
            {
                //Any 3 distinct points will not intersect
                Polygon output = new(points.EnsureClosedRing());
                if (output is null || output.Area < Geometry.Global.Epsilon)
                {
                    return null;
                }

                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                return output;
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh([.. points.Select(p => new Vertex2D(p))]);
                Polygon output = GenConcavePolygonFromMesh(mesh, nLines, false, out TriangulationMesh<IVertex2D> output_mesh);
                if (output is null || output.Area < Geometry.Global.Epsilon)
                {
                    return null;
                }

                int maxHoles = (int)Math.Floor(Math.Sqrt(nLines));
                int maxInnerLines = (int)Math.Ceiling(Math.Sqrt(output_mesh.Vertices.Count));
                maxInnerLines = maxInnerLines < 3 ? 3 : maxInnerLines;

                while (output_mesh.Faces.Count > 0 && output.InteriorPolygons.Count < maxHoles)
                {
                    Polygon inner = GenConcavePolygonFromMesh(output_mesh, maxInnerLines, true, out output_mesh);
                    if (inner is null || inner.Area < Geometry.Global.Epsilon)
                    {
                        continue;
                    }

                    try
                    {
                        output.AddInteriorRing(inner);
                    }
                    catch (ArgumentException e)
                    {
                        Trace.WriteLine("Exception adding interior ring to generated polygon");
                    }
                }

                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                return output;

                /*
                var startingEdge = mesh.Edges.Values.First(e => e.Faces.Count == 1);
                var targetVert = startingEdge.B;
                var startVert = startingEdge.A;
                Stack<int> path = new Stack<int>();
                path.Push(startVert); 
                var convexHull = FindNonSelfIntersectingPath(mesh, 
                                                             ref path,
                                                            (mesh_, path_, vert_) => path_ is null ? true :  mesh[new EdgeKey(path_.Peek(), vert_)].Faces.Count == 1 && (path.Contains(vert_) == false), //Edges can be included if they have one face
                                                            (mesh_, path_) => path_.Count > 2 && path_.Peek() == targetVert);

                path.Push(startVert); //Close the loop

                Polygon initial_output = new Polygon(path.Select(v => mesh[v].Position).ToArray());
                if (initial_output.IsValid() == false)
                    throw new ArgumentException("Invalid polygon generated");

                Stack<int> concave_path = GenConcavePolygon(mesh, path, nLines);

                //TODO: Remove edges from Convex hull to generate a concave polygon of arbitrary size, doubles as a shrinker function?
                Polygon output = new Polygon(concave_path.Select(v => mesh[v].Position).ToArray());
                if (output.IsValid() == false)
                    throw new ArgumentException("Invalid polygon generated");

                return output;*/
            }
        }

        /// <summary>
        /// Generate a polygon from the provided mesh.  Returns a copy of the input mesh, minus the verticies outside of the polygon and that were used on the polygon. 
        /// The remnant mesh can provide a set of interior points to the caller or be used to generate interior polygons
        /// </summary>
        /// <param name="input_mesh"></param>
        /// <param name="nConcaveCreationAttempts"></param>
        /// <param name="output_mesh"></param>
        /// <returns></returns>
        internal static Polygon GenConcavePolygonFromMesh(TriangulationMesh<IVertex2D> mesh, int nConcaveCreationAttempts, bool GenerateInnerPolygon, out TriangulationMesh<IVertex2D> output_mesh_final)
        {
            if (mesh.Vertices.Count < 3 || mesh.Faces.Count == 0)
            {
                throw new ArgumentException("Mesh does not contain a polygon");
            }
            else if (mesh.Faces.Count == 1)
            {
                Polygon output = new(mesh.Faces.First().iVerts.Select(v => mesh[v].Position).ToArray().EnsureClosedRing());
                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                output_mesh_final = new TriangulationMesh<IVertex2D>();
                return output;
            }
            /*else if (mesh.Vertices.Count == 3) //With making meshes for interior polygons we can have verticies that are not connected, we should not make polygons if they don't form a face
            {
                //Any 3 distinct points will not intersect
                var output = new Polygon(mesh.Vertices.Select(v => v.Position).ToArray().EnsureClosedRing());
                Debug.Assert(output.IsValid(), "Invalid polygon generated");
                output_mesh_final = new TriangulationMesh<IVertex2D>();
                return output;
            }*/
            else
            {
                //var mesh = input_mesh.Clone(); //For anonymous method

                var startingEdge = mesh.Edges.Values.First(e => e.Faces.Count == 1);
                var targetVert = startingEdge.B;
                var startVert = startingEdge.A;
                Stack<int> path = new();
                path.Push(startVert);
                var convexHull = FindNonSelfIntersectingPath(mesh,
                                                             ref path,
                                                            (mesh_, path_, vert_) => path_ is null || (mesh[new EdgeKey(path_.Peek(), vert_)].Faces.Count == 1 && (path.Contains(vert_) == false)), //Edges can be included if they have one face
                                                            (mesh_, path_) => path_.Count > 2 && path_.Peek() == targetVert);

                path.Push(startVert); //Close the loop

                Polygon initial_output = new(path.Select(v => mesh[v].Position).ToArray());
                if (initial_output.IsValid() == false)
                    throw new ArgumentException("Invalid convex polygon generated");

                Stack<int> concave_path = GenConcavePolygon(mesh, path, nConcaveCreationAttempts);

                //TODO: Remove edges from Convex hull to generate a concave polygon of arbitrary size, doubles as a shrinker function?
                Polygon output = new(concave_path.Select(v => mesh[v].Position).ToArray());
                if (output.IsValid() == false)
                    throw new ArgumentException("Invalid concave polygon generated");

                output_mesh_final = GenerateInnerPolygon ?
                        CalculateRemainderMeshAfterInnerGeneration(mesh, concave_path, output) :
                        CalculateRemainderMeshAfterOuterGeneration(mesh, concave_path, output);

                return output;
            }
        }

        /// <summary>
        /// Returns a mesh containing all points inside an exterior polygon
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        private static TriangulationMesh<IVertex2D> CalculateRemainderMeshAfterOuterGeneration(TriangulationMesh<IVertex2D> mesh, Stack<int> path, Polygon poly)
        {
            int maxRemainingMeshVerts = mesh.Vertices.Count - (path.Count - 1); //-1 because the path is a closed loop
            if (maxRemainingMeshVerts < 3)
                return new TriangulationMesh<IVertex2D>(); //No reason to return a mesh that cannot generate a polygon

            //Remove edges from all verticies on or outside the polygon.
            List<IVertex2D> vertsToKeep = new(maxRemainingMeshVerts);
            for (int iVert = 0; iVert < mesh.Vertices.Count; iVert++)
            {
                bool removeVert = path.Contains(iVert);
                if (removeVert == false)
                {
                    //Remove exterior verticies
                    removeVert = poly.GetRelation(mesh[iVert].Position) != ShapeRelation.Contained;
                }

                if (removeVert == false)
                {
                    vertsToKeep.Add(mesh[iVert]);
                }
            }

            return CreateMeshSubset(mesh, vertsToKeep);
        }

        /// <summary>
        /// Returns a mesh containing all points outside the interior polygon
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        private static TriangulationMesh<IVertex2D> CalculateRemainderMeshAfterInnerGeneration(TriangulationMesh<IVertex2D> mesh, Stack<int> path, Polygon poly)
        {
            //Remove edges from all verticies on or outside the polygon.
            int maxRemainingMeshVerts = mesh.Vertices.Count - (path.Count - 1); //-1 because the path is a closed loop
            if (maxRemainingMeshVerts < 3)
                return new TriangulationMesh<IVertex2D>(); //No reason to return a mesh that cannot generate a polygon

            //Remove edges from all verticies on or outside the polygon.
            List<IVertex2D> vertsToKeep = new(maxRemainingMeshVerts);
            for (int iVert = 0; iVert < mesh.Vertices.Count; iVert++)
            {
                bool removeVert = path.Contains(iVert);
                if (removeVert == false)
                {
                    //Remove  verticies inside the polygon
                    removeVert = poly.GetRelation(mesh[iVert].Position) != ShapeRelation.None;
                }

                if (removeVert == false)
                {
                    vertsToKeep.Add(mesh[iVert]);
                }
            }

            return CreateMeshSubset(mesh, vertsToKeep);
        }

        /// <summary>
        /// Returns a copy of the mesh with all but the specified verticies removed.  Returned mesh will have different indicies for verts
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="vertsToKeep"></param>
        /// <returns></returns>
        private static TriangulationMesh<IVertex2D> CreateMeshSubset(TriangulationMesh<IVertex2D> mesh, List<IVertex2D> vertsToKeep)
        {
            int[] input_to_output = [.. mesh.Vertices.Select((v, i) => -1)];
            TriangulationMesh<IVertex2D> output_mesh = new();
            output_mesh.AddVerticies([.. vertsToKeep.Select(v => new TriangulationVertex(v.Position))]);
            for (int iOut = 0; iOut < vertsToKeep.Count; iOut++)
            {
                var input_vert = vertsToKeep[iOut];
                input_to_output[input_vert.Index] = iOut;
            }

            List<IEdge> edgesKept = [];
            for (int iOut = 0; iOut < vertsToKeep.Count; iOut++)
            {
                var input_vert = vertsToKeep[iOut];
                var edges = input_vert.Edges;
                foreach (var key in edges)
                {
                    int A = input_to_output[key.A];
                    int B = input_to_output[key.B];
                    if (A < 0 || B < 0)
                    {
                        continue;
                    }

                    Edge out_edge = new(A, B);
                    edgesKept.Add(mesh[key]);
                    output_mesh.AddEdge(out_edge);
                }
            }

            foreach (IEdge keptEdge in edgesKept)
            {
                foreach (IFace f in keptEdge.Faces)
                {
                    TriangleFace triFace = f as TriangleFace;

                    int[] new_face = [.. triFace.iVerts.Select(iVert => input_to_output[iVert])];

                    //Skip faces with verts that are not in the output mesh
                    if (new_face.Any(iVert => iVert < 0))
                        continue;

                    TriangleFace newFace = new(new_face);
                    output_mesh.AddFace(newFace);
                }
            }

            return output_mesh;
        }

        /// <summary>
        /// Builds a concave polygon by randomly selecting edges and removing them and add two edges to the opposite vertex from the edge:
        /// 
        ///  A
        ///  | \ 
        ///  |  C
        ///  | /
        ///  B
        ///
        /// So in the above example if A-B are on the exterior ring of the polygon we remove A-B and add A-C & C-B as long as C is not also on the exterior ring
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="ExteriorPolyRing"></param>
        /// <param name="nMaxRemovals"></param>
        /// <returns></returns>
        private static Stack<int> GenConcavePolygon(TriangulationMesh<IVertex2D> mesh, Stack<int> ExteriorPolyRing, int nMaxRemovals)
        {
            List<int> ring = [.. ExteriorPolyRing];

            TriangulatedMeshGenerators.OnProgress?.Invoke(mesh);

            int nRemoved = 0;
            do
            {
                //Find the first random edge that can be removed
                if (false == TryFindRemovableEdge(mesh, ring, out IEdgeKey edgeToRemove))
                {
                    //No removable edge found, stop looking
                    break;
                }

                IEdge edge = mesh[edgeToRemove];
                TriangleFace face = edge.Faces.First() as TriangleFace;
                int oppVert = face.OppositeVertex(edge);

                int iA = ring.IndexOf(edge.A);
                int iB = ring.IndexOf(edge.B);

                if (Math.Abs(iA - iB) > 1) //Check for wrapping around
                {
                    if (iA == 0)
                        iA = ring.Count - 1;

                    else if (iB == 0)
                        iB = ring.Count - 1;
                }

                if (iA < iB)
                {
                    ring.Insert(iB, oppVert);
                }
                else
                {
                    ring.Insert(iA, oppVert);
                }

#if TRACEMESH
                Trace.WriteLine(string.Format("Remove poly edge {0}", new EdgeKey(key.A, key.B)));
                Trace.WriteLine(string.Format("Add poly edge {0}", new EdgeKey(edge.A, oppVert)));
                Trace.WriteLine(string.Format("Add poly edge {0}", new EdgeKey(oppVert, edge.B)));
#endif

                //edges.Remove(new EdgeKey(key.A, key.B));
                //edges.Add(new EdgeKey(edge.A, oppVert));
                //edges.Add(new EdgeKey(oppVert, edge.B));
                mesh.RemoveEdge(edge);
                nRemoved += 1;

                TriangulatedMeshGenerators.OnProgress?.Invoke(mesh);

                Debug.Assert(ring.IsValidClosedRing(), "Ring should be valid after adjustment");

                Polygon output = new(ring.Select(v => mesh[v].Position).ToArray());
                if (output.IsValid() == false)
                    throw new ArgumentException("Invalid polygon generated from edge removal, this is a problem in the polygon ");
            }
            while (nRemoved < nMaxRemovals);

            return new Stack<int>(ring);
        }

        /// <summary>
        /// Returns the first removable edge in shuffledEdges
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="ring"></param>
        /// <param name="shuffledEdges"></param>
        /// <param name="toRemove"></param>
        /// <returns>True and the first removable edge in shuffledEdges, otherwise false and null</returns>
        private static bool TryFindRemovableEdge(TriangulationMesh<IVertex2D> mesh, IReadOnlyList<int> ring, out IEdgeKey toRemove)
        {
            var edges = ring.ToEdgeKeys();
            Gen<EdgeKey[]> ShuffledEdgesGen = Gen.Shuffle(edges);
            EdgeKey[] shuffledEdges = ShuffledEdgesGen.Eval(1, Global.StdGenSeed); //Spec a seed so the shuffle is predictable
            toRemove = shuffledEdges.FirstOrDefault(edge => CanEdgeBeRemovedFromPoly(mesh, ring, edge));
            return toRemove.A != default;
        }

        private static bool CanEdgeBeRemovedFromPoly(TriangulationMesh<IVertex2D> mesh, IReadOnlyList<int> ExteriorPolyRing, IEdgeKey key)
        {
            IEdge edge = mesh[key];
            //Debug.Assert(edge.Faces.Count == 1, "Edges selected should be on the outside ring of the polygon and only have a single face");
            if (edge.Faces.Count != 1)
            {
                throw new ArgumentException("Edges selected should be on the outside ring of the polygon and only have a single face");
                return false;
            }

            TriangleFace face = edge.Faces.First() as TriangleFace;
            int oppVert = face.OppositeVertex(edge);

            //We cannot remove an edge if the vertex that would be added is already on the exterior ring.
            if (ExteriorPolyRing.Contains(oppVert))
                return false;

            return true;
        }
    }



    public class PolygonGenerators
    {
        public static Arbitrary<Polygon> ArbPolygon() => Arb.From(GenPolygon(), PolygonShrinker);

        public static Gen<Polygon> GenPolygon() => Gen.Sized(size => GenClosedPolylineSmart(size));


        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect.
        /// This was a slow process using arbitrary random points, so this implementation uses a mesh to increase 
        /// the odds and speed of generating a polyline for high numbers of lines
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<Polygon> GenClosedPolylineSmart(int nLines)
        {
            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in Vector2Generators.GenDistinctPoints((nLines + 1) * 3)
                        //from polygon in Gen.Constant(LineSegmentGenerators.GenConcavePolygonFromPoints(points, nLines))
                    from polygon in Gen.Constant(LineSegmentGenerators.GenConcavePolygonWithInteriorHolesFromPoints(points, nLines))
                    select polygon);
        }

        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect.
        /// This was a slow process using arbitrary random points, so this implementation uses a mesh to increase 
        /// the odds and speed of generating a polyline for high numbers of lines
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<Polygon> GenPolygonWithInteriorHoles(int nLines)
        {

            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in Vector2Generators.GenDistinctPoints((nLines + 1) * 3)
                    from polygon in Gen.Constant(LineSegmentGenerators.GenConcavePolygonFromPoints(points, nLines))
                    select polygon);
        }
        /*
        /// <summary>
        /// Generate a single polyline with N verticies
        /// This was a slow process using arbitrary random points, so this implementation uses a mesh to increase 
        /// the odds and speed of generating a polyline for high numbers of lines
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<Polygon> GenPolygon(int nLines)
        {
            if(nLines < 3)
            {
                nLines = 3;
            }

            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in Vector2Generators.GenDistinctPoints((nLines + 1) * 3)
                    from polygon in LineSegmentGenerators.GenConvexPolygonSmarter(points, nLines)
                    select polygon);
        }*/

        public static IEnumerable<Polygon> PolygonShrinker(Polygon poly)
        {
            Polygon copy = null;

            for (int iInner = poly.InteriorRings.Count - 1; iInner >= 0; iInner--)
            {
                Polygon inner_copy = (Polygon)poly.InteriorPolygons[iInner].Clone();
                foreach (Polygon shrunk_inner in PolygonShrinker(inner_copy))
                {
                    if (shrunk_inner is null)
                        continue;

                    copy = (Polygon)poly.Clone();
                    try
                    {
                        copy.ReplaceInteriorRing(iInner, shrunk_inner);

                    }
                    catch (ArgumentException)
                    {
                        //Must have made an invalid polygon, continue
                        continue;
                    }

                    yield return copy;
                }

                //Try to remove the polygon as well
                copy = (Polygon)poly.Clone();
                copy.RemoveInteriorRing(iInner);
                yield return copy;
            }

            //We cannot shrink a polygon with 3 verticies
            if (poly.TotalUniqueVertices <= 3)
                yield break;

            List<int> iVertList = [.. poly.ExteriorRing.Select((v, i) => i)];
            iVertList.RemoveAt(iVertList.Count - 1);

            int[] iShuffledVerts = Gen.Shuffle<int>(iVertList).Eval(1, Global.StdGenSeed);

            Vector2[] exterior_ring = poly.ExteriorRing;
            //Shrink a polygon by randomly removing verticies
            for (int i = 0; i < poly.ExteriorRing.Length - 1; i++)
            {
                if (poly.ExteriorRing.Length <= 4)
                    break;

                copy = (Polygon)poly.Clone();
                //Trace.WriteLine(string.Format("Try remove {0} from {1}", i, copy));

                try
                {
                    copy.RemoveVertex(iShuffledVerts[i]);
                    if (copy.Area <= Geometry.Global.Epsilon)
                        continue;
                }
                catch (ArgumentException e)
                {
                    //Trace.WriteLine(string.Format("Could not remove {0} from {1}", i, copy));
                    continue;
                }

                yield return copy;
            }
        }
    }

    public class Vector2Generators
    {
        public static int maxDimValue = 100;
        public static IRange GeneratedArraySize = new Geometry.Range(0, 1000);

        static readonly Gen<Vector2> GridPoints = ChooseFrom(PointsOnGrid1D((maxDimValue * 2) + 1,
                                                                       (maxDimValue * 2) + 1,
                                                                       new Rectangle(-maxDimValue, maxDimValue, -maxDimValue, maxDimValue)));

        public static Arbitrary<Vector2> ArbRandomPoint() => Arb.From(Fresh());

        public static Arbitrary<Vector2[]> ArbRandomDistinctPoints() => Arb.From(GenDistinctPoints(), Arb.Default.Array<Vector2>().Shrinker);

        public static Gen<Vector2> Fresh()
        {
            //return Gen<Vector2> RandPoints = GenPoint();
            //return GenPoint();
            //return GridPoints;
            return Gen.Frequency(
                Tuple.Create(1, GenPoint()),
                Tuple.Create(3, GridPoints));

        }

        private static Vector2[] PointsOnGrid1D(int GridDimX, int GridDimY, Rectangle bounds)
        {
            Vector2[,] points = PointsOnGrid(GridDimX, GridDimY, bounds);
            List<Vector2> listPoints = new(GridDimX * GridDimY);

            for (int i = 0; i < points.GetLength(0); i += 5)
            {
                for (int j = 0; j < points.GetLength(1); j += 5)
                {
                    listPoints.Add(points[i, j]);
                }
            }

            return [.. listPoints];
        }

        private static Vector2[,] PointsOnGrid(int GridDimX, int GridDimY, Rectangle bounds)
        {
            Vector2[,] points = new Vector2[GridDimX, GridDimY];
            double XStep = bounds.Width / (GridDimX - 1);
            double YStep = bounds.Height / (GridDimY - 1);

            double X = bounds.Left;
            for (int iX = 0; iX < GridDimX; iX++)
            {
                double Y = bounds.Bottom;
                for (int iY = 0; iY < GridDimY; iY++)
                {
                    points[iX, iY] = new Vector2(X, Y);
                    Y += YStep;
                }

                X += XStep;
            }

            return points;
        }
        public static Gen<Vector2[]> GenDistinctPoints() => Gen.Sized(GenDistinctPoints);

        public static Gen<Vector2[]> GenDistinctPoints(int nPoints) =>
            //List<Vector2> points = new List<Vector2>(nPoints);
            Gen.Fresh<Vector2[]>(() => DistinctPointsGenerator(nPoints));//return Gen.GrowingElements< Vector2[]>(DistinctPointsGeneratorFunc(nPoints));//return Fresh().ArrayOf(nPoints).Where(points => points.Distinct().Count() == nPoints);

        private static IEnumerable<Vector2[]> DistinctPointsGeneratorFunc(int nPoints)
        {
            //List<Vector2> points = new List<Vector2>(nPoints);
            HashSet<Vector2> points = [];
            while (true)
            {
                int neededPoints = nPoints - points.Count;
                var generated_points = GenPoint().ArrayOf(neededPoints).Eval(maxDimValue, Global.RollingStdGenSeed);
                foreach (Vector2 genPoint in generated_points)
                {
                    if (points.Contains(genPoint))
                        continue;

                    points.Add(genPoint);
                }

                //If we have enough, then yield the array
                if (points.Count == nPoints)
                {
                    /*if (nPoints == 1)
                        Trace.WriteLine(string.Format("{0}: {1}", nPoints, points.First()));
                    else if (nPoints > 1)
                        Trace.WriteLine(string.Format("{0}: {1} ... {2}", nPoints, points.First(), points.Last()));
                        */
                    yield return points.ToArray();
                    points.Clear();
                }
            }

            //return Fresh().ArrayOf(nPoints).Where(points => points.Distinct().Count() == nPoints);
        }

        private static Vector2[] DistinctPointsGenerator(int nPoints)
        {
            //List<Vector2> points = new List<Vector2>(nPoints);
            HashSet<Vector2> points = [];
            while (true)
            {
                int neededPoints = nPoints - points.Count;
                var generated_points = Fresh().ArrayOf(neededPoints).Eval(maxDimValue, Global.RollingStdGenSeed);
                foreach (Vector2 genPoint in generated_points)
                {
                    if (points.Contains(genPoint))
                        continue;

                    points.Add(genPoint);
                }

                //If we have enough, then yield the array
                if (points.Count == nPoints)
                {
                    /*
                    if (nPoints == 1)
                        Trace.WriteLine(string.Format("{0}: {1}", nPoints, points.First()));
                    else if (nPoints > 1)
                        Trace.WriteLine(string.Format("{0}: {1} ... {2}", nPoints, points.First(), points.Last()));
                        */
                    return [.. points];
                }
            }

            //return Fresh().ArrayOf(nPoints).Where(points => points.Distinct().Count() == nPoints);
        }

        public static Gen<Vector2> ChooseFrom(Vector2[] items)
        {
            return from i in Gen.Choose(0, items.Length - 1)
                   select items[i];
        }

        private static readonly Arbitrary<NormalFloat> floatArb = Arb.Default.NormalFloat();
        private static readonly Arbitrary<int> intArb = Arb.Default.Int32();

        private static readonly Gen<NormalFloat> floatGen = floatArb.Generator;
        private static readonly Gen<int> intGen = intArb.Generator;


        //private static readonly Gen<Vector2> pointGen = null;
        private static Gen<Vector2> GenPoint() =>
            /*
var coords = floatArb.Generator.Two();
var point = coords.Select(t => new Vector2((double)t.Item1, (double)t.Item2));
Trace.WriteLine(string.Format("Point Generator: {0}", point));
return point;
*/
            /*
            Trace.WriteLine(string.Format("Test: {0}", intGen.Eval(1000, Global.StdGenSeed)));
            if(pointGen is null)
            {
                //pointGen = Gen.Fresh<Vector2>(() => new Vector2((double)floatGen.Eval(maxDimValue, Global.StdGenSeed),
                //(double)floatGen.Eval(maxDimValue, Global.StdGenSeed)));
                pointGen = Gen.Fresh<Vector2>(GenPointObj);
            }

            return pointGen;*/

            /*return Gen.Fresh(() => new Vector2((double)floatGen.Eval(maxDimValue, Global.StdGenSeed),
                                                                           (double)floatGen.Eval(maxDimValue,
                                                                           Global.StdGenSeed)));*/

            //Arb.Default.NormalFloat().Generator.ArrayOf(2).Select(t => new Vector2((double)t[0], (double)t[1]));
            /*
            if(pointGen is null)
                pointGen = Arb.Default.NormalFloat().Generator.ArrayOf(2).Select(t => new Vector2((double)t[0], (double)t[1]));

            return pointGen;
            */


            //var coords = Arb.Default.NormalFloat().Generator.Two();
            //return coords.Select(t => new Vector2((double)t.Item1, (double)t.Item2)); 
            //return floatGen.Two().Select((t) => new Vector2((double)t.Item1, (double)t.Item2));
            Arb.Default.NormalFloat().Generator.Two().Select((t) => new Vector2((double)t.Item1, (double)t.Item2));

        /*
        private static Vector2 GenPointObj()
        {
            return floatGen.Two().Select((t) => new Vector2((double)t.Item1, (double)t.Item2));
        }*/
    }

    /*
    public class PolygonGenerators
    {
        public static Arbitrary<LineSegment> ArbRandomPolygon()
        {
            return Arb.From(GenPoly());
        }

        public static Gen<Polygon> GenPoly(int nVerts)
        {
            
            Vector2Generators.GenDistinctPoints(nVerts).Where()
                
        }
    }
    */
}

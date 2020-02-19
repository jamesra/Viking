using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsCheck;
using Geometry;
using Geometry.Meshing;

namespace GeometryTests
{
    public static class GeometryArbitraries
    {
        public static void Register()
        {
            Arb.Register<GridVector2Generators>();
            Arb.Register<GridLineSegmentGenerators>();
            Arb.Register<GridPolygonGenerators>();
        }

        public static Arbitrary<GridVector2> PointGenerator()
        {
            return GridVector2Generators.ArbRandomPoint();
        }

        public static Arbitrary<GridVector2[]> DistinctPointsGenerator()
        {
            return GridVector2Generators.ArbRandomDistinctPoints();
        }

        public static Arbitrary<GridLineSegment> LineSegmentGenerator()
        {
            return GridLineSegmentGenerators.ArbRandomLine();
        }

        public static Arbitrary<GridPolyline> PolyLineGenerator()
        {
            return GridLineSegmentGenerators.ArbPolyLine();
        }
    }

    public class GridLineSegmentGenerators
    {
        public static Arbitrary<GridLineSegment> ArbRandomLine()
        {
            return Arb.From(GenLine());
        }

        public static Arbitrary<GridPolyline> ArbPolyLine()
        {
            return Arb.From(Fresh());
        }

        

        public static Gen<GridLineSegment> GenLine()
        {
            return GridVector2Generators.Fresh()
                .Two()
                .Where(t => t.Item1 != t.Item2)
                .Select(t => new GridLineSegment(t.Item1, t.Item2));


            var coords = Arb.Default.NormalFloat().Generator.Four();
            return coords.Select(t => new GridLineSegment(new GridVector2((double)t.Item1, (double)t.Item2),
                                                           new GridVector2((double)t.Item3, (double)t.Item4)));
        }

        /*
        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        public static Gen<GridVector2[]> GenOpenPolyline(int nLines)
        {
            int nPoints = nLines + 1;
            List<GridVector2> polyLinePts = new List<GridVector2>(nPoints);
            List<GridLineSegment> polyLine = new List<GridLineSegment>(nLines);

            while(polyLine.Count < nLines+1)
            {
                var pts = GridVector2Generators.GenDistinctPoints(nPoints-polyLinePts.Count);
                pts.Where(array => array.Where((p) => 
                    {
                        if (polyLine.Count == 0)
                            return true;

                        GridLineSegment ls = new GridLineSegment(polyLinePts.Last(), p);
                        GridPolyline 
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

        public static Gen<GridPolyline> Fresh()
        {
            return Gen.Sized(size => GenOpenPolylineSmart(size));
        }
        
        /*

        private static Gen<GridPolyline> GenOpenPolylineFast(int nLines)
        {
            //Oversample the points in the mesh to speed up the process of locating a non-intersecting path
            return (from points in GridVector2Generators.GenDistinctPoints(nLines + 1)
                    from polyline in GenOpenPolylineFast(points)
                    select polyline);
        }

        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<GridPolyline> GenOpenPolylineFast(GridVector2[] points)
        {
            if (points.Length <= 0)
            {
                return Gen.Constant<GridPolyline>(new GridPolyline());
            }
            else if(points.Length <= 3)
            {
                //Any 3 distinct points will not intersect
                return Gen.Constant<GridPolyline>(new GridPolyline(points));
            }
            else
            {
                var polyline = (from shuffled_points in Gen.Shuffle(points)
                              let lastPoint = shuffled_points.Last()
                              from polyline_subset in GenOpenPolylineFast(shuffled_points.ToList().GetRange(0, points.Length - 1).ToArray())
                              where polyline_subset.CanAdd(lastPoint)
                              select new GridPolyline(shuffled_points));
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
        private static Gen<GridPolyline> GenOpenPolylineSmart(int nLines)
        {
            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in GridVector2Generators.GenDistinctPoints((nLines + 1) * 3)
                    from polyline in GenOpenPolylineSmart(points, nLines > 50 ? (int)Math.Round(Math.Sqrt(nLines-50) + 50) : nLines)
                    select polyline);
        }



        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Gen<GridPolyline> GenOpenPolylineSmart(GridVector2[] points, int nLines)
        {
            if (points.Length <= 0)
            {
                return Gen.Constant<GridPolyline>(new GridPolyline());
            }
            else if (points.Length <= 3)
            {
                //Any 3 distinct points will not intersect
                return Gen.Constant<GridPolyline>(new GridPolyline(points));
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray());

                //Use verticies on the convex hull as starting points for our search
                //var startingPoints = mesh.Edges.Values.Where(e => e.Faces.Count == 1).SelectMany(e => new int[] { e.A, e.B }).Distinct();
                
                foreach(var startingVert in mesh.Verticies)
                {
                    Stack<int> VertStack = new Stack<int>();
                    VertStack.Push(startingVert.Index);
                    bool FoundPath = FindNonSelfIntersectingPath(mesh,
                                                                ref VertStack,
                                                                meets_path_inclusion_criteria: (mesh_, path_, vert_) => path_.Contains(vert_) == false,
                                                                meets_path_completion_criteria: (mesh_, path_) => PathLengthCriteria(mesh_, path_, nLines));

                    if (FoundPath)
                    {
                        return Gen.Constant(new GridPolyline(VertStack.Select(v => mesh[v].Position)));
                    }
                }

                throw new Exception("Unexpectedly unable to generate path from a set of points");
                return Gen.Constant<GridPolyline>(new GridPolyline());
            }
        }

        /// <summary>
        /// This function yields an arbitrary non-intersecting path using edges of a triangulated mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="PathLength"></param>
        /// <returns></returns>
        private static bool FindNonSelfIntersectingPath(TriangulationMesh<Vertex2D> mesh, Stack<int> path, int nLines)
        {
            if (path.Count == nLines + 1)
            {
                //We have found a path to all verticies that does not self intersect
                return true;
            }

            Debug.Assert(nLines < mesh.Verticies.Count);
            int currentVert = path.Peek();
            Vertex2D vertex = mesh[currentVert];

            foreach (var edgeKey in vertex.Edges)
            {
                int candidateVert = edgeKey.OppositeEnd(vertex.Index);

                if (path.Contains(candidateVert))
                    continue;
                else
                {
                    path.Push(candidateVert);
                    bool foundPath = FindNonSelfIntersectingPath(mesh, path, nLines);
                    if(foundPath)
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
        private static bool FindNonSelfIntersectingPath(TriangulationMesh<Vertex2D> mesh, 
                                                        ref Stack<int> path, 
                                                        Func<TriangulationMesh<Vertex2D>, Stack<int>, int, bool> meets_path_inclusion_criteria,
                                                        Func<TriangulationMesh<Vertex2D>, Stack<int>, bool> meets_path_completion_criteria)
        {
            if (meets_path_completion_criteria(mesh, path))
            {
                //We have found a path to all verticies that does not self intersect
                return true;
            }

            int currentVert = path.Peek();
            Vertex2D vertex = mesh[currentVert];

            foreach (var edgeKey in vertex.Edges)
            {
                int candidateVert = edgeKey.OppositeEnd(currentVert);

                if(false == meets_path_inclusion_criteria(mesh, path, candidateVert))
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
        private static bool PathLengthCriteria(TriangulationMesh<Vertex2D> mesh, Stack<int> path, int nLines)
        {
            return path.Count == nLines + 1;
        }

        /// <summary>
        /// Return true if the path can be closed using an edge in the mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="path"></param>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static bool PathClosedCriteria(TriangulationMesh<Vertex2D> mesh, Stack<int> path)
        {
            Vertex2D last = mesh[path.Last()];
            Vertex2D first = mesh[path.First()];

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
        private static Gen<GridPolyline> GenOpenPolyline(int nLines)
        {
            return (from points in GridVector2Generators.GenDistinctPoints(nLines + 1)
            from shuffled_points in Gen.Shuffle(points)
            where GridLineSegment.SegmentsFromPoints(shuffled_points).SelfIntersects(LineSetOrdering.POLYLINE) == false
            select new GridPolyline(shuffled_points));
        }

        /*
            int nPoints = nLines + 1;

            GridPolyline polyLine = new GridPolyline(nLines, AllowSelfIntersection: false);

            var pGen = GridVector2Generators.Fresh();

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
        var pts = GridVector2Generators.GenDistinctPoints((nLines - polyLine.LineCount));
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

    return Gen.Constant<GridPolyline>(polyLine);
}
*/


        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Gen<GridPolygon> GenClosedPolylineSmart(GridVector2[] points, int nLines)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Insufficient lines to form a closed shape.");
            }
            else if (points.Length == 3)
            {
                //Any 3 distinct points will not intersect
                return Gen.Constant<GridPolygon>(new GridPolygon(points.EnsureClosedRing()));
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray());

                foreach (var startingVert in mesh.Verticies)
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
                            return Gen.Constant(new GridPolygon(VertStack.Select(v => mesh[v].Position).ToArray().EnsureClosedRing() ));
                        }
                        else
                        {
                            VertStack.Pop();
                        }
                    }
                }

                throw new Exception("Unexpectedly unable to generate path from a set of points");
                //return Gen.Constant<GridPolygon>(new GridPolygon());
            }
        }

        /// <summary>
        /// Using the provided points, return a single polyline with nLines that is not closed and whose lines do not self-intersect
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        internal static Gen<GridPolygon> GenClosedPolylineSmarter(GridVector2[] points, int nLines)
        {
            if (points.Length < 3)
            {
                throw new ArgumentException("Insufficient lines to form a closed shape.");
            }
            else if (points.Length == 3)
            {
                //Any 3 distinct points will not intersect
                return Gen.Constant<GridPolygon>(new GridPolygon(points.EnsureClosedRing()));
            }
            else
            {
                var mesh = GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(points.Select(p => new Vertex2D(p)).ToArray());

                var startingEdge = mesh.Edges.Values.First(e => e.Faces.Count == 1);
                var targetVert = startingEdge.B;
                var startVert = startingEdge.A;
                Stack<int> path = new Stack<int>();
                path.Push(startVert);
                var convexHull = FindNonSelfIntersectingPath(mesh, 
                                                             ref path,
                                                            (mesh_, path_, vert_) => path_ == null ? true :  mesh[new EdgeKey(path_.Peek(), vert_)].Faces.Count == 1 && (path.Contains(vert_) == false), //Edges can be included if they have one face
                                                            (mesh_, path_) => path_.Count > 2 && path_.Peek() == targetVert);

                path.Push(startVert); //Close the loop

                //TODO: Remove edges from Convex hull to generate a concave polygon of arbitrary size, doubles as a shrinker function?
                return Gen.Constant(new GridPolygon(path.Select(v => mesh[v].Position).ToArray()));

            }
        }
         
    }

    public class GridPolygonGenerators
    {
        public static Arbitrary<GridPolygon> ArbPolygon()
        {
            return Arb.From(GenPolygon(), PolygonShrinker);
        }

        public static Gen<GridPolygon> GenPolygon()
        {
            return Gen.Sized(size => GenClosedPolylineSmart(size));
        }


        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect.
        /// This was a slow process using arbitrary random points, so this implementation uses a mesh to increase 
        /// the odds and speed of generating a polyline for high numbers of lines
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<GridPolygon> GenClosedPolylineSmart(int nLines)
        {
            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in GridVector2Generators.GenDistinctPoints((nLines + 1) * 3)
                    from polyline in GridLineSegmentGenerators.GenClosedPolylineSmarter(points, nLines)
                    select polyline);
        }

        /// <summary>
        /// Generate a single polyline with N verticies that is not closed and whose lines do not self-intersect.
        /// This was a slow process using arbitrary random points, so this implementation uses a mesh to increase 
        /// the odds and speed of generating a polyline for high numbers of lines
        /// </summary>
        /// <param name="nLines"></param>
        /// <returns></returns>
        private static Gen<GridPolygon> GenPolygon(int nLines)
        {
            if(nLines < 3)
            {
                nLines = 3;
            }

            ///Generate a large set of points, then find a path of the correct length using those points
            return (from points in GridVector2Generators.GenDistinctPoints((nLines + 1) * 3)
                    from polygon in GridLineSegmentGenerators.GenClosedPolylineSmarter(points, nLines)
                    select polygon);
        }

        public static IEnumerable<GridPolygon> PolygonShrinker(GridPolygon poly)
        {
            /*
            for(int iInner = 0; iInner < poly.InteriorRings.Count; iInner++)
            {
                GridPolygon copy = (GridPolygon)poly.Clone();
                
            }*/

            GridVector2[] exterior_ring = poly.ExteriorRing;
            //Shrink a polygon by randomly removing verticies
            for(int i = 0; i < poly.ExteriorRing.Length; i++)
            {
                GridPolygon copy = (GridPolygon)poly.Clone();
                try
                {   
                    copy.RemoveVertex(i);
                }
                catch(ArgumentException e)
                {
                    continue;
                }

                yield return copy;
            }
        }
    }


    public class GridVector2Generators
    {
        static Gen<GridVector2> GridPoints = ChooseFrom(PointsOnGrid1D(201, 201, new GridRectangle(-100, 100, -100, 100)));

        public static Arbitrary<GridVector2> ArbRandomPoint()
        {
            return Arb.From(Fresh());
        }

        public static Arbitrary<GridVector2[]> ArbRandomDistinctPoints()
        {
            return Arb.From(GenDistinctPoints(), Arb.Default.Array<GridVector2>().Shrinker);
        }

        public static Gen<GridVector2> Fresh()
        {
            Gen<GridVector2> RandPoints = GenPoint();
            return Gen.Frequency(
                Tuple.Create(1, RandPoints),
                Tuple.Create(1, GridPoints));
        }

        private static GridVector2[] PointsOnGrid1D(int GridDimX, int GridDimY, GridRectangle bounds)
        {
            GridVector2[,] points = PointsOnGrid(GridDimX, GridDimY, bounds);
            List<GridVector2> listPoints = new List<GridVector2>(GridDimX * GridDimY);

            for (int i = 0; i < points.GetLength(0); i++)
            {
                for (int j = 0; j < points.GetLength(1); j++)
                {
                    listPoints.Add(points[i, j]);
                }
            }

            return listPoints.ToArray();
        }

        private static GridVector2[,] PointsOnGrid(int GridDimX, int GridDimY, GridRectangle bounds)
        {
            GridVector2[,] points = new GridVector2[GridDimX, GridDimY];
            double XStep = bounds.Width / (GridDimX - 1);
            double YStep = bounds.Height / (GridDimY - 1);

            double X = bounds.Left;
            for (int iX = 0; iX < GridDimX; iX++)
            {
                double Y = bounds.Bottom;
                for (int iY = 0; iY < GridDimY; iY++)
                {
                    points[iX, iY] = new GridVector2(X, Y);
                    Y += YStep;
                }

                X += XStep;
            }

            return points;
        }
        public static Gen<GridVector2[]> GenDistinctPoints()
        {
            return Gen.Sized(size => GenDistinctPoints(size));
        }

        public static Gen<GridVector2[]> GenDistinctPoints(int nPoints)
        {
            return Fresh().ArrayOf(nPoints).Where(points => points.Distinct().Count() == nPoints);
        }

        public static Gen<GridVector2> ChooseFrom(GridVector2[] items)
        {
            return from i in Gen.Choose(0, items.Length - 1)
                   select items[i];
        }

        private static Gen<GridVector2> GenPoint()
        {
            var coords = Arb.Default.NormalFloat().Generator.Two();
            return coords.Select(t => new GridVector2((double)t.Item1, (double)t.Item2));
        }
    }

    /*
    public class GridPolygonGenerators
    {
        public static Arbitrary<GridLineSegment> ArbRandomPolygon()
        {
            return Arb.From(GenPoly());
        }

        public static Gen<GridPolygon> GenPoly(int nVerts)
        {
            
            GridVector2Generators.GenDistinctPoints(nVerts).Where()
                
        }
    }
    */
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Geometry.Meshing;

namespace Geometry
{
    internal struct Baseline
    {
        public readonly GridLineSegment Segment;
        public readonly IVertex2D OriginVert;
        public readonly IVertex2D TargetVert;
        public long Origin { get => (long)OriginVert.Index; }
        public long Target { get => (long)TargetVert.Index; }
        public readonly GridLine Line;

        public Baseline(IVertex2D Origin, IVertex2D Target)
        {
            OriginVert = Origin;
            TargetVert = Target;
            Segment = new GridLineSegment(Origin.Position, Target.Position);
            Line = new GridLine(Segment.A, GridVector2.Normalize(Segment.B - Segment.A));
        }
    }

    public static class DelaunayMeshGenerator2D
    {
        /// <summary>
        /// Generates the delaunay triangulation for a list of points. 
        /// Requires the points to be sorted on the X-axis coordinate!
        /// Every the integers in the returned array are the indicies in the passes array of triangles. 
        /// Implemented based upon: http://local.wasp.uwa.edu.au/~pbourke/papers/triangulate/
        /// "Triangulate: Efficient Triangulation Algorithm Suitable for Terrain Modelling"
        /// by Paul Bourke
        /// </summary>
        /// <returns>A Mesh2D whose vertex indicies match the input points</returns>
        public static TriangulationMesh<TriangulationVertex> TriangulateToMesh(GridVector2[] points)
        {
            TriangulationVertex[] verts = points.Select(p => new TriangulationVertex(p)).ToArray();
            return GenericDelaunayMeshGenerator2D<TriangulationVertex>.TriangulateToMesh(verts);
        }
    }

    internal enum CutDirection {NONE=0, HORIZONTAL, VERTICAL };
    /// <summary>
    /// Generates constrained Delaunay triangulations
    /// </summary>
    public static class GenericDelaunayMeshGenerator2D<VERTEX>
        where VERTEX : IVertex2D, IVertexSortEdgeByAngle
    {
        /// <summary>
        /// Generates the delaunay triangulation for a list of points. 
        /// Requires the points to be sorted on the X-axis coordinate!
        /// Every the integers in the returned array are the indicies in the passes array of triangles. 
        /// Implemented based upon: http://local.wasp.uwa.edu.au/~pbourke/papers/triangulate/
        /// "Triangulate: Efficient Triangulation Algorithm Suitable for Terrain Modelling"
        /// by Paul Bourke
        /// </summary>
        /// <returns>A Mesh2D whose vertex indicies match the input points</returns>
        public static TriangulationMesh<VERTEX> TriangulateToMesh(VERTEX[] verts)
        {
            if (verts == null)
            {
                throw new ArgumentNullException("Verticies must not be null.");
            }

            if (verts.Length < 3)
            {
                return null;
            }

            TriangulationMesh<VERTEX> mesh = new TriangulationMesh<VERTEX>();
            mesh.AddVerticies(verts);

            MeshSubset subset = new MeshSubset(mesh.XSorted, mesh.YSorted, CutDirection.HORIZONTAL, mesh.BoundingBox);
            RecursiveDivideAndConquerDelaunay(mesh, subset);

            //return TriangleIndicies;
            return mesh;
        }


        /// <summary>
        /// Divides the mesh verticies into two halves and triangulates the halves
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="VertSet">Indicies of verticies in the half.  Sorted on either X or Y axis</param>
        /// <returns></returns>
        private static TriangulationMesh<VERTEX> RecursiveDivideAndConquerDelaunay(TriangulationMesh<VERTEX> mesh, MeshSubset VertSet = null)
        {
            //The first recursion we populate variables to include all the verticies in the mesh
            if (VertSet == null)
            {
                VertSet = new MeshSubset(mesh.XSorted, mesh.YSorted, CutDirection.HORIZONTAL, mesh.BoundingBox);
                //VertSet = new ContinuousIndexSet(0, mesh.Verticies.Count);
                //XSortedVerts = mesh.XSorted;
                //YSortedVerts = mesh.YSorted;
            }

            //Check if we have 0-3 verticies.  Create edges appropriately.
            if (VertSet.Count == 0)
            {
                return null;
            }
            if (VertSet.Count == 1)
            {
                Trace.WriteLine(string.Format("Base case, single point: {0}", VertSet[0]));
                return mesh;
            }
            else if (VertSet.Count == 2)
            {
                Trace.WriteLine(string.Format("Base case: Add Edge: {0} - {1}", VertSet[0], VertSet[1]));
                mesh.AddEdge(new Edge((int)VertSet[0], (int)VertSet[1]));
                return mesh;
            }
            else if (VertSet.Count == 3)
            {
                Trace.WriteLine(string.Format("Base case: Add Triangle: {0} - {1}", VertSet[0], VertSet[1]));
                Trace.WriteLine(string.Format("Base case: Add Triangle: {0} - {1}", VertSet[1], VertSet[2]));
                Trace.WriteLine(string.Format("Base case: Add Triangle: {0} - {1}", VertSet[2], VertSet[0]));

                Edge ZeroOne = new Edge((int)VertSet[0], (int)VertSet[1]);
                Edge OneTwo = new Edge((int)VertSet[1], (int)VertSet[2]);
                Edge TwoZero = new Edge((int)VertSet[2], (int)VertSet[0]);
                mesh.AddEdge(ZeroOne);
                mesh.AddEdge(OneTwo);

                //There is a case where all three points are on a perfect line, in this case don't create the final edge and face.
                if (mesh.ToGridLineSegment(TwoZero).DistanceToPoint(mesh[VertSet[1]].Position) > 0)
                {
                    mesh.AddEdge(new Edge((int)VertSet[2], (int)VertSet[0]));
                    mesh.AddFace(new Face((int)VertSet[0], (int)VertSet[1], (int)VertSet[2]));
                }

                return mesh;
            }

            VertSet.SplitIntoHalves(mesh.Verticies.Select(v => (IVertex2D)v).ToArray(), out MeshSubset FirstHalfSet, out MeshSubset SecondHalfSet);

            TriangulationMesh<VERTEX> FirstHalfMesh = RecursiveDivideAndConquerDelaunay(mesh, FirstHalfSet);
            TriangulationMesh<VERTEX> SecondHalfMesh = RecursiveDivideAndConquerDelaunay(mesh, SecondHalfSet);

            //We've Triangulated each half, now stitch them together
            //Begin at the first vertex (the min value) from both sets, we'll call them L and R from here.

            VERTEX L, R;

            //TODO: The first vertex needs to be the index sorted on the opposite axis as the cut. 
            L = mesh[FirstHalfSet.SortedAlongCutAxisVertSet.First()];
            R = mesh[SecondHalfSet.SortedAlongCutAxisVertSet.First()];

            //Create the base LR edge
            while(true)
            {  
                //Edge case: Both sets of points are in opposite quadrants:
                //
                //Vertical cut:
                //        
                //         0
                //          \
                //           1
                //
                //    _--3
                // 2--
                //
                //In the case above 2-1 is the edge using the smallest Y value from each set (2,3) & (0,1).  However this leaves 3 below the origin line. 
                //To handle this we check that 2 and 1 do not have verticies clockwise or ccw from the origin line respectively.

                EdgeAngle[] L_CW_Candidates  = EdgesByAngle(mesh, L, R.Index, true);
                EdgeAngle[] R_CCW_Candidates = EdgesByAngle(mesh, R, L.Index,false);

                bool BaselineFound = true;
                ///If we can find a point below the baseline, use the point from the highest angle from the baseline
                if(L_CW_Candidates.Length > 0)
                {
                    Trace.WriteLine(string.Format("Reject Baseline: {0}-{1} for {2}", L.Index, R.Index, L_CW_Candidates.First().Target));
                    L = mesh[L_CW_Candidates.First().Target];
                    BaselineFound = false;
                }
                
                if(R_CCW_Candidates.Length > 0)
                {
                    Trace.WriteLine(string.Format("Reject Baseline: {0}-{1} for {2}", L.Index, R.Index, R_CCW_Candidates.First().Target));
                    R = mesh[R_CCW_Candidates.First().Target];
                    BaselineFound = false;
                }

                if(!BaselineFound)
                {
                    continue; 
                }

                Edge baseEdge = new Edge(L.Index, R.Index);
                mesh.AddEdge(baseEdge);

                Trace.WriteLine(string.Format("Add Baseline: {0}-{1}", L.Index, R.Index));

                break;
            }

            //Rotate counter clockwise from the L set, and clockwise from the R set to identify the next candidate points

            IVertex2D LOrigin = L;
            IVertex2D ROrigin = R;
            IVertex2D LeftCandidate = null;
            IVertex2D RightCandidate = null;
            List<long> LCandidates = L.EdgesByAngle(mesh.edgeAngleComparer, R.Index, false).ToList();
            List<long> RCandidates = R.EdgesByAngle(mesh.edgeAngleComparer, L.Index, true).ToList();

            if(LCandidates.Contains(R.Index))
            {
                LCandidates.Remove(R.Index);
            }

            if(RCandidates.Contains(L.Index))
            {
                RCandidates.Remove(L.Index);
            }

            double LAngle;
            double RAngle;

            Baseline LRBaseline = new Baseline(L, R);
            Baseline RLBaseline = new Baseline(R, L);

            GridCircle? LCircle = new GridCircle();
            GridCircle? RCircle = new GridCircle();

            while (true)
            {
                Debug.WriteLine(string.Format("L0: {0} R0: {1}", LOrigin.Index, ROrigin.Index));
                //TODO: Handle case where there are no left or right candidates
                if (LeftCandidate == null)
                {
                    LeftCandidate = TryGetNextCandidate(mesh, ref LCandidates, LRBaseline, Clockwise: false, angle: out LAngle, circle: out LCircle);
                    if(LeftCandidate != null)
                        Debug.Assert(LeftCandidate.Index != LOrigin.Index);
                }

                if (RightCandidate == null)
                {
                    RightCandidate = TryGetNextCandidate(mesh, ref RCandidates, RLBaseline, Clockwise: true, angle: out RAngle, circle: out RCircle);
                    if(RightCandidate != null)
                        Debug.Assert(RightCandidate.Index != ROrigin.Index);
                }

                //If we have no candidates we are done. 
                //If we have only one candidate that is the new edge. 
                //If we have both candidates figure out which one creates a circle that excludes the other candidate.
                if (LeftCandidate == null && RightCandidate == null)
                {
                    break;
                }
                else if (LeftCandidate == null && RightCandidate != null)
                {
                    goto UseRight;
                }
                else if (RightCandidate == null && LeftCandidate != null)
                {
                    goto UseLeft;
                }
                else
                {
                    if (LCircle.HasValue == false)
                        LCircle = GridCircle.CircleFromThreePoints(LOrigin.Position, ROrigin.Position, LeftCandidate.Position);

                    if (LCircle.Value.Contains(RightCandidate.Position))
                    {
                        //The right candidate needs to be used
                        goto UseRight;
                    }
                    else
                    {
                        //The left candidate needs to be used
                        goto UseLeft;
                    }
                }

            UseLeft:
                Trace.WriteLine(string.Format("Add Edge: {0}-{1}", LeftCandidate.Index, ROrigin.Index));
                mesh.AddEdge(new Edge(LeftCandidate.Index, ROrigin.Index));
                mesh.AddFace(new Face(LeftCandidate.Index, LOrigin.Index, ROrigin.Index));
                LOrigin = LeftCandidate;

                //Build the list of new candidates
                LCandidates = mesh[LOrigin.Index].EdgesByAngle(mesh.edgeAngleComparer, ROrigin.Index, false).Where(c => FirstHalfSet.Contains(c) && c != LOrigin.Index).ToList();

                LeftCandidate = null;

                goto FindNextEdge;
            UseRight:
                Trace.WriteLine(string.Format("Add Edge: {0}-{1}", RightCandidate.Index, LOrigin.Index));
                mesh.AddEdge(new Edge(RightCandidate.Index, LOrigin.Index));
                mesh.AddFace(new Face(RightCandidate.Index, LOrigin.Index, ROrigin.Index));
                ROrigin = RightCandidate;

                //Build the list of new candidates
                RCandidates = mesh[ROrigin.Index].EdgesByAngle(mesh.edgeAngleComparer, LOrigin.Index, true).Where(c => SecondHalfSet.Contains(c) && c != ROrigin.Index).ToList();
                RightCandidate = null;
                goto FindNextEdge;
            FindNextEdge:
                LRBaseline = new Baseline(LOrigin, ROrigin);
                RLBaseline = new Baseline(ROrigin, LOrigin);
            }
             
            return null;
        }
         
        /// <summary>
        /// Yields a set of connected verticies in order of rotation from the edge running from origin to target vertex. 
        /// Does not include target vertex in results and does not return edges with an angle 180 degrees or more.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Origin"></param>
        /// <param name="origin_edge_target"></param>
        /// <param name="clockwise"></param>
        /// <returns></returns>
        public static EdgeAngle[] EdgesByAngle(TriangulationMesh<VERTEX> mesh, VERTEX Origin, long origin_edge_target, bool clockwise)
        {
            //Setting the comparer should update the order of the edges attribute only if necessary.
            GridVector2 target = mesh[origin_edge_target].Position;
            MeshEdgeAngleComparerFixedIndex<VERTEX> angleComparer = new MeshEdgeAngleComparerFixedIndex<VERTEX>(mesh, Origin.Index, new GridLine(Origin.Position, target - Origin.Position), clockwise);

            long[] edges = Origin.Edges.Select(e => e.OppositeEnd((long)Origin.Index)).ToArray();
            double[] angles = edges.Select(edge => angleComparer.MeasureAngle(edge)).ToArray();

            int[] iSorted = angles.SortAndIndex();

            //Only process the edges with an angle below 180 degress.  Larger than that and a valid triangle is impossible. 
            int nBelow180 = angles.TakeWhile(angle => angle < Math.PI && angle > 0).Count();

            EdgeAngle[] edgeAngles = new EdgeAngle[nBelow180];
            for (int i = 0; i < nBelow180; i++)
            {
                EdgeAngle ea = new EdgeAngle(Origin.Index, edges[iSorted[i]], angles[i], clockwise);
                edgeAngles[i] = ea; 
            }
             
            return edgeAngles;
        }

        private static IVertex2D TryGetNextCandidate(TriangulationMesh<VERTEX> mesh, ref List<long> sortedCandidates, Baseline baseline, bool Clockwise, out double angle, out GridCircle? circle)
        {
            if (sortedCandidates == null || sortedCandidates.Count == 0)
            {
                circle = new GridCircle?();
                angle = double.MinValue;
                return null;
            }

            long candidate = sortedCandidates[0];
            IVertex2D candidateVert = mesh[candidate];

            while (candidateVert != null)
            {
                if (candidateVert.Index == baseline.Target)
                {
                    sortedCandidates.RemoveAt(0);
                    if (sortedCandidates.Count == 0)
                    {
                        circle = new GridCircle?();
                        angle = double.MinValue;
                        return null;
                    }

                    candidate = sortedCandidates[0];
                    candidateVert = mesh[candidate];
                    continue;
                }

                angle = GridVector2.AbsArcAngle(baseline.Line, candidateVert.Position, Clockwise: Clockwise);

                //When the candidates would create a triangle with an angle sum over 180 degrees there are no more viable candidates on that side to check.
                //However, a changing baseline may make the candidates viable again, so do not remove them.
                if(angle == 0 && baseline.Segment.IsEndpoint(candidateVert.Position))
                {
                    sortedCandidates.RemoveAt(0);
                    continue; 
                }
                if (angle >= Math.PI)
                { 
                    circle = new GridCircle?();
                    return null;
                }

                //If there are no other candidates, then we can return this vertex
                if (sortedCandidates.Count == 1)
                {
                    circle = new GridCircle?(); 
                    return candidateVert;
                }

                //OK, now we check if the next candidate is inside the circle described by the baseline and the candidate
                //TODO: We can check this faster with linear algebra using the determinant I believe
                circle = GridCircle.CircleFromThreePoints(baseline.Segment.A, baseline.Segment.B, candidateVert.Position);

                long nextCandidate = sortedCandidates[1];
                IVertex2D nextCandidateVert = mesh[nextCandidate];

                if (circle.Value.Contains(nextCandidateVert.Position))
                {
                    //Check edge case of a point exactly on the circle boundary
                    if(GridVector2.Distance(nextCandidateVert.Position, circle.Value.Center) == circle.Value.Radius)
                    {
                        return candidateVert;
                    }

                    //This candidate doesn't work, delete the edge from origin to the candidate and check the next potential candidate.
                    Debug.WriteLine(string.Format("Remove Edge: {0}-{1}", baseline.Origin, candidate));
                    mesh.RemoveEdge(new EdgeKey(baseline.Origin, candidate));
                    sortedCandidates.RemoveAt(0); //Remove the candidate from the list of options

                    candidate = nextCandidate;
                    candidateVert = nextCandidateVert;
                }
                else
                {
                    return candidateVert;
                }
            }

            angle = double.MinValue;
            circle = new GridCircle?();
            return null;
        }
    }

    class MeshSubset
    {
        public GridRectangle BoundingBox;

        public readonly CutDirection CutAxis;

        public long[] XSortedVerts;
        public long[] YSortedVerts;

        public long Count { get { return XSortedVerts.LongLength; } }

        public bool Contains(long value)
        {
            return XSortedVerts.Contains(value);
        }

        public IReadOnlyList<long> Verticies { get { return CutAxis == CutDirection.HORIZONTAL ? XSortedVerts : YSortedVerts; } }

        public long[] SortedAlongCutAxisVertSet
        {
            get { return CutAxis == CutDirection.VERTICAL ? YSortedVerts : XSortedVerts; }
            set
            {
                if (CutAxis == CutDirection.VERTICAL)
                {
                    YSortedVerts = value;
                }
                else
                {
                    XSortedVerts = value;
                }
            }
        }

        public long[] SortedOppositeCutAxisVertSet
        {
            get { return CutAxis == CutDirection.VERTICAL ? XSortedVerts : YSortedVerts; }
            set
            {
                if (CutAxis == CutDirection.VERTICAL)
                {
                    XSortedVerts = value;
                }
                else
                {
                    YSortedVerts = value;
                }
            }
        }

        public long this[long key]
        {
            get
            {
                return SortedOppositeCutAxisVertSet[key];
            }
            set
            {
                SortedOppositeCutAxisVertSet[key] = value;
            }
        }

        public int this[int key]
        {
            get
            {
                return (int)SortedOppositeCutAxisVertSet[key];
            }
            set
            {
                SortedOppositeCutAxisVertSet[key] = value;
            }
        }

        public MeshSubset(long[] SortedAlongAxis, long[] SortedOppositeAxis, CutDirection cutAxis, GridRectangle boundingRect)
        {
            CutAxis = cutAxis;
            XSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedAlongAxis : SortedOppositeAxis;
            YSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedOppositeAxis : SortedAlongAxis;
            BoundingBox = boundingRect;
        }


        public void SplitIntoHalves(IReadOnlyList<IVertex2D> mesh, out MeshSubset FirstSubset, out MeshSubset SecondSubset)
        {
            //Split the verticies into smaller groups and then merge the resulting triangulations
            CutDirection cutDirection = BoundingBox.Width > BoundingBox.Height ? CutDirection.VERTICAL : CutDirection.HORIZONTAL;

            long[] NewSortedAlongCutAxisVertSet;
            long[] NewSortedOppositeCutAxisVertSet;

            //Sort our verticies according to the new direction
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                //Use the mesh's ordering arrays to determine the new sorted vertex order
                NewSortedAlongCutAxisVertSet = XSortedVerts;
                NewSortedOppositeCutAxisVertSet = YSortedVerts;
            }
            else
            {
                NewSortedAlongCutAxisVertSet = YSortedVerts;
                NewSortedOppositeCutAxisVertSet = XSortedVerts;
            }

            //TODO: I'm 99% certain there is a way to get the verts sorted on the new axis just by indexing the arrays, but it is late and I'm not seeing it.  
            //These are the notes I wrote trying to figure it out:
            //A,B,C,D,E,F X Values
            //0,1,2,3,4,5 Y Values

            //B3,A2,C5,D1,F4,E0 Mesh Verts

            // 1, 0, 2, 3, 5, 4 Verts SortedOnX
            // 3, 2, 5, 1, 4, 0 Verts SortedOnY

            // 0, 3, 4, 5(B3, D1, F4, E0) Sample Index Set

            //XSorted Indices for Set
            //    1, 3, 5, 4       XSorted Indicies
            //   B3, D1, E0, F4   XSorted Set

            //YSorted Indicies for Set
            // 3, 1, 4, 0      YSorted Indicies            
            //E0, D1, B3, F4, YSorted Set


            //Divide verticies into two groups along the axis
            long nFirstHalf = NewSortedAlongCutAxisVertSet.LongLength / 2;
            
            long[] FirstHalfAlongAxis = new long[nFirstHalf];
            long[] SecondHalfAlongAxis = new long[NewSortedAlongCutAxisVertSet.LongLength - nFirstHalf];
            long[] FirstHalfOppAxis = new long[FirstHalfAlongAxis.LongLength];
            long[] SecondHalfOppAxis = new long[SecondHalfAlongAxis.LongLength];

            Array.Copy(NewSortedOppositeCutAxisVertSet, FirstHalfOppAxis, FirstHalfOppAxis.LongLength);
            Array.Copy(NewSortedOppositeCutAxisVertSet, FirstHalfOppAxis.LongLength, SecondHalfOppAxis, 0, SecondHalfOppAxis.LongLength);

            GridVector2 DivisionPoint = mesh[(int)FirstHalfOppAxis.Last()].Position;

            //Divide the opposite axis sorted set into two groups as well
            long iFirstHalfAdd = 0;
            long iSecondHalfAdd = 0;

            Debug.WriteLine(string.Format("{0}--------{1}-------",cutDirection, DivisionPoint));

            for (long i = 0; i < NewSortedAlongCutAxisVertSet.LongLength; i++)
            {
                long iVert = NewSortedAlongCutAxisVertSet[i];
                GridVector2 vertPos = mesh[(int)iVert].Position;
                bool AssignToFirst = false; 
                if (cutDirection == CutDirection.HORIZONTAL)
                {
                    if (vertPos.Y < DivisionPoint.Y)
                    {
                        AssignToFirst = true;
                    }
                    else if (vertPos.Y == DivisionPoint.Y)
                    {
                        AssignToFirst = iVert == FirstHalfOppAxis.Last() || FirstHalfOppAxis.Contains(iVert);
                    }
                    else
                    {
                        AssignToFirst = false;
                    }
                }
                else
                {
                    if (vertPos.X < DivisionPoint.X)
                    {
                        AssignToFirst = true;
                    }
                    else if(vertPos.X == DivisionPoint.X)
                    {
                        AssignToFirst = iVert == FirstHalfOppAxis.Last() || FirstHalfOppAxis.Contains(iVert);
                    }
                    else
                    {
                        AssignToFirst = false;
                    }
                }

                if(AssignToFirst)
                {
                    Debug.WriteLine(string.Format("1st <- {0}: {1}", iVert, vertPos));

                    FirstHalfAlongAxis[iFirstHalfAdd] = iVert;
                    iFirstHalfAdd += 1;
                }
                else
                {
                    Debug.WriteLine(string.Format("2nd <- {0}: {1}", iVert, vertPos));

                    SecondHalfAlongAxis[iSecondHalfAdd] = iVert;
                    iSecondHalfAdd += 1;
                }
            }
             
            GridRectangle FirstHalfBBox;
            GridRectangle SecondHalfBBox;
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                FirstHalfBBox = new GridRectangle(BoundingBox.Left, BoundingBox.Right, mesh[(int)FirstHalfOppAxis[0]].Position.Y, mesh[(int)FirstHalfOppAxis.Last()].Position.Y);
                SecondHalfBBox = new GridRectangle(BoundingBox.Left, BoundingBox.Right, mesh[(int)SecondHalfOppAxis[0]].Position.Y, mesh[(int)SecondHalfOppAxis.Last()].Position.Y);

                SecondSubset = new MeshSubset(FirstHalfAlongAxis, FirstHalfOppAxis, cutDirection, FirstHalfBBox);
                FirstSubset = new MeshSubset(SecondHalfAlongAxis, SecondHalfOppAxis, cutDirection, SecondHalfBBox);
            }
            else
            {
                FirstHalfBBox = new GridRectangle(mesh[(int)FirstHalfOppAxis[0]].Position.X, mesh[(int)FirstHalfOppAxis.Last()].Position.X, BoundingBox.Bottom, BoundingBox.Top);
                SecondHalfBBox = new GridRectangle(mesh[(int)SecondHalfOppAxis[0]].Position.X, mesh[(int)SecondHalfOppAxis.Last()].Position.X, BoundingBox.Bottom, BoundingBox.Top);

                FirstSubset = new MeshSubset(FirstHalfAlongAxis, FirstHalfOppAxis, cutDirection, FirstHalfBBox);
                SecondSubset = new MeshSubset(SecondHalfAlongAxis, SecondHalfOppAxis, cutDirection, SecondHalfBBox);
            }

            SecondSubset.SortSecondAxis(mesh, false);
        }

        /// <summary>
        /// //Assuming the points are sorted along the cut axis already, the secondary sorting axis is correct for whether the half is above or below the cut line:
        ///
        ///      1 -- 5
        ///      |    |
        ///      2 -- 6
        /// ---- |    | -- cut line --- 
        ///      3 -- 7
        ///      |    |
        ///      4 -- 8
        ///
        ///  After cutting, the Y Sorting along the X axis (for points with the same X value) for each set should be:
        ///
        ///      2 -- 6
        ///      |    |
        ///      1 -- 5
        /// ---- |    | -- cut line --- 
        ///      3 -- 7
        ///      |    |
        ///      4 -- 8
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="ascending"></param>
        private void SortSecondAxis(IReadOnlyList<IVertex2D> mesh, bool ascending=true)
        {
            int v1;
            int v2;
            int temp; 
            GridVector2 p1;
            GridVector2 p2;

            if (this.CutAxis == CutDirection.HORIZONTAL)
            {
                for (int i = 0; i < this.XSortedVerts.LongLength - 1; i++)
                {
                    v1 = (int)XSortedVerts[i];
                    p1 = mesh[v1].Position;

                    for (int j = i + 1; j < this.XSortedVerts.LongLength; j++)
                    {
                        v2 = (int)XSortedVerts[j];
                        p2 = mesh[v2].Position;

                        //Check if the first axis isn't equal, if it isn't then bail on this loop
                        if (p1.X != p2.X)
                        {
                            break;
                        }

                        bool swap = ascending ? p1.Y > p2.Y : p2.Y > p1.Y;

                        if (swap)
                        {
                            XSortedVerts[i] = v2;
                            XSortedVerts[j] = v1;
                            v1 = v2;
                            p1 = p2;
                            continue;
                        }
                        
                        //continue checking while the cut axis value remains equal
                    }
                }
            }
            else
            {
                for (int i = 0; i < this.YSortedVerts.LongLength - 1; i++)
                {
                    v1 = (int)YSortedVerts[i];
                    p1 = mesh[v1].Position;

                    for (int j = i + 1; j < this.YSortedVerts.LongLength; j++)
                    {
                        v2 = (int)YSortedVerts[j];
                        p2 = mesh[v2].Position;

                        //Check if the first axis isn't equal, if it isn't then bail on this loop
                        if (p1.Y != p2.Y)
                        {
                            break;
                        }

                        bool swap = ascending ? p1.X > p2.X : p2.X > p1.X;

                        if (swap)
                        {
                            YSortedVerts[i] = v2;
                            YSortedVerts[j] = v1;
                            v1 = v2;
                            p1 = p2;
                            continue;
                        }

                        //continue checking while the cut axis value remains equal
                    }
                }
            }
        }
        
    }
}
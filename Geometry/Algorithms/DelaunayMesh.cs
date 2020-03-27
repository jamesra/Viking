//#define TRACEDELAUNAY
//#define VERIFYDELAUNAY


using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;


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

    internal enum CutDirection { NONE = 0, HORIZONTAL, VERTICAL };
    /// <summary>
    /// Generates constrained Delaunay triangulations
    /// </summary>
    public static class GenericDelaunayMeshGenerator2D<VERTEX>
        where VERTEX : IVertex2D
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
        public static TriangulationMesh<VERTEX> TriangulateToMesh(VERTEX[] verts, TriangulationMesh<VERTEX>.ProgressUpdate ReportProgress = null)
        {
            if (verts == null)
            {
                throw new ArgumentNullException("Verticies must not be null.");
            }

            TriangulationMesh<VERTEX> mesh = new TriangulationMesh<VERTEX>();
            mesh.AddVerticies(verts);

            MeshCut subset = new MeshCut(mesh.XSorted, mesh.YSorted, CutDirection.HORIZONTAL, mesh.BoundingBox);

            //try
            //{
            RecursiveDivideAndConquerDelaunay(mesh, subset, ReportProgress: ReportProgress);
            //}
            //catch
            //{
            //    return mesh; 
            //}

            foreach (TriangleFace f in mesh.Faces.ToArray())
            {
                if (mesh.Faces.Contains(f) && mesh.IsTriangleDelaunay(f) == false)
                {
                    CheckEdgeFlip(mesh, f, ReportProgress);
                }
            }

#if VERIFYDELAUNAY
            
            Debug.Assert(mesh.AnyMeshEdgesIntersect() == false, "Mesh Edges Intersect");

            foreach(Face f in mesh.Faces)
            {
                Debug.Assert(mesh.IsTriangleDelaunay(f), string.Format("{0} is not a delaunay triangle", f));
                Debug.Assert(mesh.IsClockwise(f) == false);
            }
            
#endif

#if DEBUG
            foreach (Face f in mesh.Faces.ToArray())
            {
                if (mesh.ToTriangle(f).Area <= 0)
                    //mesh.RemoveFace(f);
                    throw new NonconformingTriangulationException(f, string.Format("{0} is a colinear triangle", f));

                if (mesh.IsTriangleDelaunay(f) == false)
                    throw new NonconformingTriangulationException(f, string.Format("{0} is not a delaunay triangle", f));

#if VERIFYDELAUNAY
                Debug.Assert(mesh.IsTriangleDelaunay(f), string.Format("{0} is not a delaunay triangle", f));
                Debug.Assert(mesh.IsClockwise(f) == false);
#endif

            }
#endif
            //return TriangleIndicies;
            return mesh;
        }


        /// <summary>
        /// Divides the mesh verticies into two halves and triangulates the halves
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="VertSet">Indicies of verticies in the half.  Sorted on either X or Y axis</param>
        /// <returns></returns>
        private static TriangulationMesh<VERTEX> RecursiveDivideAndConquerDelaunay(TriangulationMesh<VERTEX> mesh, MeshCut VertSet = null, IVertex2D[] verts = null, TriangulationMesh<VERTEX>.ProgressUpdate ReportProgress = null)
        {
            //The first recursion we populate variables to include all the verticies in the mesh
            if (VertSet == null)
            {
                VertSet = new MeshCut(mesh.XSorted, mesh.YSorted, CutDirection.HORIZONTAL, mesh.BoundingBox);
                //VertSet = new ContinuousIndexSet(0, mesh.Verticies.Count);
                //XSortedVerts = mesh.XSorted;
                //YSortedVerts = mesh.YSorted;
            }

            if (verts == null)
            {
                verts = mesh.Verticies.Cast<IVertex2D>().ToArray();
            }

            //Check if we have 0-3 verticies.  Create edges appropriately.
            if (VertSet.Count == 0)
            {
                return null;
            }
            if (VertSet.Count == 1)
            {
#if TRACEDELAUNAY
                Trace.WriteLine(string.Format("Base case, single point: {0}", VertSet[0]));
#endif
                return mesh;
            }
            else if (VertSet.Count == 2)
            {
#if TRACEDELAUNAY
                Trace.WriteLine(string.Format("Base case: Add Edge: {0} - {1}", VertSet[0], VertSet[1]));
#endif
                mesh.AddEdge(new Edge((int)VertSet[0], (int)VertSet[1]));

                if (ReportProgress != null)
                {
                    ReportProgress(mesh);
                }

                return mesh;
            }
            else if (VertSet.Count == 3)
            {
#if TRACEDELAUNAY
                Trace.WriteLine(string.Format("Base case: Add Triangle: {0} - {1}", VertSet[0], VertSet[1]));
                Trace.WriteLine(string.Format("Base case: Add Triangle: {0} - {1}", VertSet[1], VertSet[2]));
                Trace.WriteLine(string.Format("Base case: Add Triangle: {0} - {1}", VertSet[2], VertSet[0]));
#endif

                Edge ZeroOne = new Edge((int)VertSet[0], (int)VertSet[1]);
                Edge OneTwo = new Edge((int)VertSet[1], (int)VertSet[2]);
                Edge TwoZero = new Edge((int)VertSet[2], (int)VertSet[0]);
                mesh.AddEdge(ZeroOne);
                mesh.AddEdge(OneTwo);

                //There is a case where all three points are on a perfect line, in this case don't create the final edge and face.
                if (mesh.ToGridLineSegment(TwoZero).IsLeft(mesh[VertSet[1]].Position) != 0)
                {
                    mesh.AddEdge(new Edge((int)VertSet[2], (int)VertSet[0]));

                    //If the points are not co-linear, create a face
                    
                    TriangleFace newFace = new TriangleFace((int)VertSet[0], (int)VertSet[1], (int)VertSet[2]);

                    if (mesh.IsClockwise(newFace))
                    {
                        newFace = new TriangleFace((int)VertSet[0], (int)VertSet[2], (int)VertSet[1]);
                    }

                    if(mesh.ToTriangle(newFace).Area > 0) //Do not create a face for colinear points
                        mesh.AddFace(newFace);
                }

                if (ReportProgress != null)
                {
                    ReportProgress(mesh);
                }

                return mesh;
            }

            VertSet.SplitIntoHalves(verts, out MeshCut FirstHalfSet, out MeshCut SecondHalfSet);

            TriangulationMesh<VERTEX> FirstHalfMesh = RecursiveDivideAndConquerDelaunay(mesh, FirstHalfSet, verts, ReportProgress);
            TriangulationMesh<VERTEX> SecondHalfMesh = RecursiveDivideAndConquerDelaunay(mesh, SecondHalfSet, verts, ReportProgress);

            //We've Triangulated each half, now stitch them together
            //Begin at the first vertex (the min value) from both sets, we'll call them L and R from here.

            SortedSet<IEdgeKey> AddedEdges = new SortedSet<IEdgeKey>();

            // VERTEX L, R;

            //FindBaselineByAngle(mesh, FirstHalfSet, SecondHalfSet, out VERTEX L, out VERTEX R);
            FindBaselineByLeftOfLineTest(mesh, FirstHalfSet, SecondHalfSet, out VERTEX L, out VERTEX R);

            Edge baseEdge = new Edge(L.Index, R.Index);
            mesh.AddEdge(baseEdge);

            if (ReportProgress != null)
            {
                ReportProgress(mesh);
            }

            AddedEdges.Add(baseEdge);

#if TRACEDELAUNAY
            Trace.WriteLine(string.Format("Add Baseline: {0}-{1}", L.Index, R.Index));
#endif 
            //Rotate counter clockwise from the L set, and clockwise from the R set to identify the next candidate points

            IVertex2D LOrigin = L;
            IVertex2D ROrigin = R;
            IVertex2D LeftCandidate = null;
            IVertex2D RightCandidate = null;
            List<EdgeAngle> LCandidates = EdgesByAngle(mesh, L, R.Index, false).ToList();
            List<EdgeAngle> RCandidates = EdgesByAngle(mesh, R, L.Index, true).ToList();

            double LAngle;
            double RAngle;

            Baseline LRBaseline = new Baseline(L, R);
            Baseline RLBaseline = new Baseline(R, L);

            GridCircle? LCircle = new GridCircle();
            GridCircle? RCircle = new GridCircle();

            List<Face> AddedFaces = new List<Face>();

            SortedSet<long> PastLeftOriginVerts = new SortedSet<long>();
            SortedSet<long> PastRightOriginVerts = new SortedSet<long>();
            PastLeftOriginVerts.Add(L.Index);
            PastRightOriginVerts.Add(R.Index);


            while (true)
            {
#if TRACEDELAUNAY
                Debug.WriteLine(string.Format("L0: {0} R0: {1}", LOrigin.Index, ROrigin.Index));
#endif
                //TODO: Handle case where there are no left or right candidates
                if (LeftCandidate == null)
                {
                    LeftCandidate = TryGetNextCandidate(mesh, ref LCandidates, LRBaseline, Clockwise: false, angle: out LAngle, circle: out LCircle);
                    if (LeftCandidate != null)
                        Debug.Assert(LeftCandidate.Index != LOrigin.Index);
                }

                if (RightCandidate == null)
                {
                    RightCandidate = TryGetNextCandidate(mesh, ref RCandidates, RLBaseline, Clockwise: true, angle: out RAngle, circle: out RCircle);
                    if (RightCandidate != null)
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

                TriangleFace newFace;

            UseLeft:
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Add Edge: {0}-{1}", LeftCandidate.Index, ROrigin.Index));
#endif
                    Edge NewEdge = new Edge(LeftCandidate.Index, ROrigin.Index);
                    mesh.AddEdge(NewEdge);
                    AddedEdges.Add(NewEdge);
                    newFace = new TriangleFace(LeftCandidate.Index, LOrigin.Index, ROrigin.Index);

                    if (mesh.IsClockwise(newFace))
                        newFace = new TriangleFace(LeftCandidate.Index, ROrigin.Index, LOrigin.Index);

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Add Face: {0}", newFace));
#endif
                    //A quick sanity check to ensure we do not add a colinear triangle
                    if (mesh.ToTriangle(newFace).Area > 0)
                    {
                        mesh.AddFace(newFace);

                        //CheckEdgeFlip(mesh, newFace);
                        AddedFaces.Add(newFace);
                    }

                    PastLeftOriginVerts.Add(LOrigin.Index);
                    LOrigin = LeftCandidate;

                    //Check if the new face meets delaunay criteria
                    Debug.Assert(mesh.IsClockwise(newFace) == false, "Face verts aren't counter-clockwise");


                    //Build the list of new candidates
                    //LCandidates = mesh[LOrigin.Index].EdgesByAngle(mesh.edgeAngleComparer, ROrigin.Index, false).Where(c => FirstHalfSet.Contains(c) && c != LOrigin.Index).ToList();
                    LCandidates = EdgesByAngle(mesh, LOrigin, ROrigin.Index, false).ToList();
                    RCandidates = EdgesByAngle(mesh, ROrigin, LOrigin.Index, true).ToList();

                    //Debug.Assert(false == LCandidates.Any(c => PastLeftOriginVerts.Contains(c.Origin)));

                    LeftCandidate = null;
                }
                goto FindNextEdge;
            UseRight:
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Add Edge: {0}-{1}", RightCandidate.Index, LOrigin.Index));
#endif
                    Edge NewEdge = new Edge(RightCandidate.Index, LOrigin.Index);
                    mesh.AddEdge(NewEdge);
                    AddedEdges.Add(NewEdge);
                    newFace = new TriangleFace(RightCandidate.Index, LOrigin.Index, ROrigin.Index);
                    if (mesh.IsClockwise(newFace))
                        newFace = new TriangleFace(RightCandidate.Index, ROrigin.Index, LOrigin.Index);

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Add Face: {0}", newFace));
#endif
                    //A quick sanity check to ensure we do not add a colinear triangle
                    if (mesh.ToTriangle(newFace).Area > 0)
                    {
                        
                        mesh.AddFace(newFace);
                        //CheckEdgeFlip(mesh, newFace);
                        AddedFaces.Add(newFace);
                    }

                    Debug.Assert(mesh.IsClockwise(newFace) == false, "Face verts aren't counter-clockwise");

                    PastRightOriginVerts.Add(ROrigin.Index);
                    ROrigin = RightCandidate;

                    //Check if the new face meets delaunay criteria

                    //Build the list of new candidates
                    //RCandidates = mesh[ROrigin.Index].EdgesByAngle(mesh.edgeAngleComparer, LOrigin.Index, true).Where(c => SecondHalfSet.Contains(c) && c != ROrigin.Index).ToList();
                    LCandidates = EdgesByAngle(mesh, LOrigin, ROrigin.Index, false).ToList();
                    RCandidates = EdgesByAngle(mesh, ROrigin, LOrigin.Index, true).ToList();

                    //Debug.Assert(false == RCandidates.Any(c => PastRightOriginVerts.Contains(c.Origin)));

                    RightCandidate = null;
                }
                goto FindNextEdge;
            FindNextEdge:

                //LCandidates = LCandidates.Where(c => PastLeftOriginVerts.Contains(c.Target) == false).ToList();
                //RCandidates = RCandidates.Where(c => PastRightOriginVerts.Contains(c.Target) == false).ToList();

                //Ensure we only take candidates from the left or right sets, not new edges that cross the sets
                LCandidates = LCandidates.Where(c => FirstHalfSet.Contains(c.Target)).ToList();
                RCandidates = RCandidates.Where(c => SecondHalfSet.Contains(c.Target)).ToList();

                //An edge case  where points on a straight line will try to add a face twice because of floating point rounding errors
                LCandidates = LCandidates.Where(c => AddedEdges.Contains(new EdgeKey(ROrigin.Index, c.Target)) == false).ToList();
                RCandidates = RCandidates.Where(c => AddedEdges.Contains(new EdgeKey(LOrigin.Index, c.Target)) == false).ToList();
                //LCandidates = LCandidates.Where(c => !PastLeftOriginVerts.Contains(c.Target)).ToList();
                //RCandidates = RCandidates.Where(c => !PastRightOriginVerts.Contains(c.Target)).ToList();

                //Debug.Assert(false == LCandidates.Any(c => PastLeftOriginVerts.Contains(c.Origin)));
                //Debug.Assert(false == RCandidates.Any(c => PastRightOriginVerts.Contains(c.Origin)));


                LRBaseline = new Baseline(LOrigin, ROrigin);
                RLBaseline = new Baseline(ROrigin, LOrigin);

                if (ReportProgress != null)
                {
                    ReportProgress(mesh);
                }
            }

            List<IEdgeKey> EdgesToCheck = AddedFaces.SelectMany(f => f.Edges).Distinct().ToList();
            foreach (IEdgeKey edge in EdgesToCheck)
            {
                if (mesh.Contains(edge))
                    CheckEdgeFlip(mesh, mesh[edge] as Edge, ReportProgress);
            }



            return null;
        }

        private static void FindBaselineByAngle(TriangulationMesh<VERTEX> mesh, MeshCut LowerHalfSet, MeshCut UpperHalfSet, out VERTEX LowerOrigin, out VERTEX UpperOrigin)
        {

            //TODO: The first vertex needs to be the index sorted on the opposite axis as the cut. 
            VERTEX L = mesh[LowerHalfSet.SortedAlongCutAxisVertSet.First()];
            VERTEX R = mesh[UpperHalfSet.SortedAlongCutAxisVertSet.First()];

            //L = mesh[FirstHalfSet.SortedOppositeCutAxisVertSet.First()];
            //R = mesh[SecondHalfSet.SortedOppositeCutAxisVertSet.First()];

            //TODO: I changed the above two lines before quitting for the night.  Check if the
            //loop below is still needed

            //Create the base LR edge
            while (true)
            {
                //Edge case: Both sets of points are in opposite quadrants:
                //
                //Vertical cut:             Horizontal cut:
                //        
                //         0                            0
                //          \                            \
                //           1                            1
                //                               3
                //    _--3                       /
                // 2--                          2
                //
                //In the case above 2-1 is the edge using the smallest Y value from each set (2,3) & (0,1).  However this leaves 3 below the origin line. 
                //To handle this we check that 2 and 1 do not have verticies clockwise or ccw from the origin line respectively.

                EdgeAngle[] L_CW_Candidates = EdgesByAngle(mesh, L, R.Index, true);
                EdgeAngle[] R_CCW_Candidates = EdgesByAngle(mesh, R, L.Index, false);

                //I can't find a case where the correct base LR edge is going to have an angle beyond 90 degrees.  Ideally I should only check for an angle
                //less than the angle from the origin line to the axis, but that angle is always less than 90.

                //Clockwise is from the testAngleAxisLine to the point.
#if TRACEDELAUNAY
                if (LowerHalfSet.CutAxis == CutDirection.HORIZONTAL)
                {
                    string s = string.Format("Horizontal: Left | Right reversed {0} | {1}", LowerHalfSet, UpperHalfSet);
                    Trace.WriteLineIf(L.Position.Y > R.Position.Y, s);
                    Debug.Assert(L.Position.Y < R.Position.Y, s);
                }
                else
                {
                    string s = string.Format("Vertical: Left | Right reversed {0} | {1}", LowerHalfSet, UpperHalfSet);
                    Trace.WriteLineIf(L.Position.X > R.Position.X, s);
                    Debug.Assert(L.Position.X < R.Position.X, s);
                }
#endif
                /*
               GridLine testAngleAxisLine = new GridLine(L.Position, FirstHalfSet.CutAxis == CutDirection.VERTICAL ? GridVector2.UnitX : GridVector2.UnitY);

               if(L_CW_Candidates.Length > 0 || R_CCW_Candidates.Length > 0)
               {
                   double L_Max_Angle = GridVector2.AbsArcAngle(testAngleAxisLine, R.Position, false);//Check angles from the base line to parallel with the cut axis
                   Debug.Assert(L_Max_Angle <= Math.PI);
                   double R_Max_Angle = Math.PI - L_Max_Angle;

                   L_CW_Candidates = L_CW_Candidates.Where(c => L_Max_Angle - c.Angle > Global.Epsilon).ToArray();
                   R_CCW_Candidates = R_CCW_Candidates.Where(c => R_Max_Angle - c.Angle > Global.Epsilon).ToArray();
               }
               */

                L_CW_Candidates = L_CW_Candidates.Where(c => Math.PI - c.Angle > Global.Epsilon).ToArray();
                R_CCW_Candidates = R_CCW_Candidates.Where(c => Math.PI - c.Angle > Global.Epsilon).ToArray();

                bool BaselineFound = true;
                ///If we can find a point below the baseline, use the point from the highest angle from the baseline
                if (L_CW_Candidates.Length > 0)
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Reject Baseline: {0}-{1} for {2}", L.Index, R.Index, L_CW_Candidates.First().Target));
#endif
                    L = mesh[L_CW_Candidates.First().Target];
                    BaselineFound = false;
                }

                if (R_CCW_Candidates.Length > 0)
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Reject Baseline: {0}-{1} for {2}", L.Index, R.Index, R_CCW_Candidates.First().Target));
#endif
                    R = mesh[R_CCW_Candidates.First().Target];
                    BaselineFound = false;
                }

                if (!BaselineFound)
                {
                    continue;
                }

                break;
            }

            LowerOrigin = L;
            UpperOrigin = R;
            return;
        }

        /// <summary>
        /// Checks for a baseline vertex by ensuring no vertex is to the left of the Lower and Upper set's first vertex.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="LowerHalfSet"></param>
        /// <param name="UpperHalfSet"></param>
        /// <param name="LowerOrigin"></param>
        /// <param name="UpperOrigin"></param>
        private static void FindBaselineByLeftOfLineTest(TriangulationMesh<VERTEX> mesh, MeshCut LowerHalfSet, MeshCut UpperHalfSet, out VERTEX LowerOrigin, out VERTEX UpperOrigin)
        {

            //TODO: The first vertex needs to be the index sorted on the opposite axis as the cut. 
            VERTEX L;
            VERTEX R;

            if (LowerHalfSet.CutAxis == CutDirection.HORIZONTAL)
            {
                L = mesh[LowerHalfSet.SortedAlongCutAxisVertSet.Last()];
                R = mesh[UpperHalfSet.SortedAlongCutAxisVertSet.Last()];
            }
            else
            {
                L = mesh[LowerHalfSet.SortedAlongCutAxisVertSet.First()];
                R = mesh[UpperHalfSet.SortedAlongCutAxisVertSet.First()];
            }

            GridLineSegment LR_baseline_candidate;
            GridLineSegment RL_baseline_candidate;

            //L = mesh[FirstHalfSet.SortedOppositeCutAxisVertSet.First()];
            //R = mesh[SecondHalfSet.SortedOppositeCutAxisVertSet.First()];

            //TODO: I changed the above two lines before quitting for the night.  Check if the
            //loop below is still needed

            //Create the base LR edge

            int iLoopCount = 0;

            //This dictionary prevents rare endless loops in conditions where we have colinear points in one or both sets.
            Dictionary<int, SortedSet<int>> RejectedBaselinePairs = new Dictionary<int, SortedSet<int>>();

            while (true)
            {
                iLoopCount += 1;

                Debug.Assert(iLoopCount <= (LowerHalfSet.Count + UpperHalfSet.Count) * 2, "FindBaselineByLeftOfLineTest: Taking an unreasonably long time to identify baseline.");
                //if (iLoopCount > (LowerHalfSet.Count + UpperHalfSet.Count) * 2)
                    //throw new ArgumentException("FindBaselineByLeftOfLineTest: Taking an unreasonably long time to identify baseline.");


                //Edge case: Both sets of points are in opposite quadrants:
                //
                //Vertical cut:             Horizontal cut:
                //        
                //         0                            0
                //          \                            \
                //           1                            1
                //                               3
                //    _--3                       /
                // 2--                          2
                //
                //In the case above 2-1 is the edge using the smallest Y value from each set (2,3) & (0,1).  However this leaves 3 below the origin line. 
                //To handle this we check that 2 and 1 do not have verticies clockwise or ccw from the origin line respectively.

                LR_baseline_candidate = mesh.ToGridLineSegment(L.Index, R.Index);
                RL_baseline_candidate = mesh.ToGridLineSegment(R.Index, L.Index);

                SortedSet<int> L_Rejected_Candidates = RejectedBaselinePairs.ContainsKey(R.Index) ? RejectedBaselinePairs[R.Index] : new SortedSet<int>();

                int[] L_Origin_Candidates = mesh[L.Index].Edges.Select(e => e.OppositeEnd(L.Index)).Where(id => L_Rejected_Candidates.Contains(id) == false).ToArray();
                //int[] L_Origin_Candidates = mesh[L.Index].Edges.Select(e => e.OppositeEnd(L.Index)).ToArray();
                int[] L_Origin_Candidates_IsLeft = L_Origin_Candidates.Select(iVert => LR_baseline_candidate.IsLeft(mesh[iVert].Position)).ToArray();
                

                /*
                if (LowerHalfSet.CutAxis == CutDirection.HORIZONTAL)
                {
                    for (int i = 0; i < L_Origin_Candidates_IsLeft.Length; i++)
                        L_Origin_Candidates_IsLeft[i] = -L_Origin_Candidates_IsLeft[i];

                    for (int i = 0; i < R_Origin_Candidates_IsLeft.Length; i++)
                        R_Origin_Candidates_IsLeft[i] = -R_Origin_Candidates_IsLeft[i];
                }
                */

                bool NewCandidateFound = false;

                for (int i = 0; i < L_Origin_Candidates.Length; i++)
                {
                    //For the case of a point on the line we use the closer point to the R origin
                    if (L_Origin_Candidates_IsLeft[i] == 0)
                    {
                        int L_Candidate = L_Origin_Candidates[i];
                        double L_R_Distance = GridVector2.DistanceSquared(L.Position, R.Position);
                        double L_Candidate_Distance = GridVector2.DistanceSquared(mesh[L_Candidate].Position, R.Position);
                        if (L_R_Distance > L_Candidate_Distance)
                        {
                            //OK, the point on the L_R baseline is closer, switch to the new baseline.  Repeat the search.
#if TRACEDELAUNAY
                            Trace.WriteLine(string.Format("Reject Left Baseline: {0}-{1} for {2}", L.Index, R.Index, L_Candidate));
#endif
                            RejectedBaselinePairs.AddToSet(R.Index, L.Index); //Record that this baseline pairing does not work so we don't test it again
                            L = mesh[L_Candidate];
                            NewCandidateFound = true;

                            break;
                        }
                    }
                    else if (L_Origin_Candidates_IsLeft[i] < 0)
                    {
                        int L_Candidate = L_Origin_Candidates[i];
#if TRACEDELAUNAY
                        Trace.WriteLine(string.Format("Reject Left Baseline: {0}-{1} for {2}-{1}", L.Index, R.Index, L_Candidate));
#endif
                        RejectedBaselinePairs.AddToSet(R.Index, L.Index); //Record that this baseline pairing does not work so we don't test it again

                        L = mesh[L_Candidate];
                        NewCandidateFound = true;
                        break;
                    }
                }

                //If we changed the L Origin, then repeat the search with the new origin
                if (NewCandidateFound)
                    continue;

                
                SortedSet<int> R_Rejected_Candidates = RejectedBaselinePairs.ContainsKey(L.Index) ? RejectedBaselinePairs[L.Index] : new SortedSet<int>();

                //Reverse the IsLeft result for the Upper->Lower line
                int[] R_Origin_Candidates = mesh[R.Index].Edges.Select(e => e.OppositeEnd(R.Index)).Where(id => R_Rejected_Candidates.Contains(id) == false).ToArray();
                //int[] R_Origin_Candidates = mesh[R.Index].Edges.Select(e => e.OppositeEnd(R.Index)).ToArray();
                int[] R_Origin_Candidates_IsLeft = R_Origin_Candidates.Select(iVert => LR_baseline_candidate.IsLeft(mesh[iVert].Position)).ToArray();

                for (int i = 0; i < R_Origin_Candidates.Length; i++)
                {
                    //For the case of a point on the line we use the closer point to the R origin
                    if (R_Origin_Candidates_IsLeft[i] == 0)
                    {
                        int R_Candidate = R_Origin_Candidates[i];
                        double L_R_Distance = GridVector2.DistanceSquared(L.Position, R.Position);
                        double R_Candidate_Distance = GridVector2.DistanceSquared(mesh[R_Candidate].Position, L.Position);
                        if (L_R_Distance > R_Candidate_Distance)
                        {
                            //OK, the point on the L_R baseline is closer, switch to the new baseline.  Repeat the search.
#if TRACEDELAUNAY
                            Trace.WriteLine(string.Format("Reject Right Baseline: {0}-{1} for {2}", L.Index, R.Index, R_Candidate));
#endif
                            RejectedBaselinePairs.AddToSet(L.Index, R.Index); //Record that this baseline pairing does not work so we don't test it again
                            R = mesh[R_Candidate];
                            NewCandidateFound = true;
                            break;
                        }
                    }
                    else if (R_Origin_Candidates_IsLeft[i] < 0)
                    {
                        int R_Candidate = R_Origin_Candidates[i];
#if TRACEDELAUNAY
                        Trace.WriteLine(string.Format("Reject Right Baseline: {0}-{1} for {0}-{2}", L.Index, R.Index, R_Candidate));
#endif
                        RejectedBaselinePairs.AddToSet(L.Index, R.Index); //Record that this baseline pairing does not work so we don't test it again

                        R = mesh[R_Candidate];
                        NewCandidateFound = true;
                        break;
                    }
                }

                if (NewCandidateFound)
                {
                    
                    continue;
                }

                break;
            }

            /*
            if (LowerHalfSet.CutAxis == CutDirection.HORIZONTAL)
            {
                LowerOrigin = R;
                UpperOrigin = L;
            }
            else
            {
                LowerOrigin = L;
                UpperOrigin = R;
            }
            */

            LowerOrigin = L;
            UpperOrigin = R;

            return;
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
        public static EdgeAngle[] EdgesByAngle(TriangulationMesh<VERTEX> mesh, IVertex2D Origin, long origin_edge_target, bool clockwise)
        {
            //Setting the comparer should update the order of the edges attribute only if necessary.
            GridVector2 target = mesh[origin_edge_target].Position;
            MeshEdgeAngleComparerFixedIndex<VERTEX> angleComparer = new MeshEdgeAngleComparerFixedIndex<VERTEX>(mesh, Origin.Index, new GridLine(Origin.Position, target - Origin.Position), clockwise);

            List<long> edge_list = Origin.Edges.Select(e => e.OppositeEnd((long)Origin.Index)).Where(e => e != origin_edge_target).ToList();

            //We have to include angle == 0 for the case where points are on a uniform grid.  This allows the baseline finding code to correctly locate the point nearest the cut line.
            //EdgeAngle[] edgeAngles = edge_list.Select(edge => new EdgeAngle(Origin.Index, edge, angleComparer.MeasureAngle(edge), clockwise)).Where(edge => edge.Angle >= 0 && edge.Angle < Math.PI).ToArray();
            EdgeAngle[] edgeAngles = edge_list.Select(edge => new EdgeAngle(Origin.Index, edge, angleComparer.MeasureAngle(edge), clockwise)).ToArray();
            EdgeAngle[] edgeAnglesFiltered = edgeAngles.Where(edge => edge.Angle >= 0 && edge.Angle < Math.PI).ToArray();

            Array.Sort(edgeAnglesFiltered.Select(e => e.Angle).ToArray(), edgeAnglesFiltered);


            /*
            List<double> angle_list = edge_list.Select(edge => angleComparer.MeasureAngle(edge)).ToList();

            //Angles below zero are possible in edge cases where points are almost exactly on the origin line.
            //Requiring angles to be > 0 prevents adding a duplicate face in these cases.  Since the list is
            //sorted, remove them from both the edges and angles array
            for(int i = angle_list.Count-1; i >= 0; i--)
            {
                if(angle_list[i] <= 0)
                {
                    edge_list.RemoveAt(i);
                    angle_list.RemoveAt(i);
                }
            }

            //Convert to an array so we can sort and index
            double[] angles = angle_list.ToArray();
             
            int[] iSorted = angles.SortAndIndex();

            //Todo: Handle the case where angle is very close to 2 * PI

            //Only process the edges with an angle below 180 degress.  Larger than that and a valid triangle is impossible. 
            int nBelow180 = angles.TakeWhile(angle => angle < Math.PI).Count();

            EdgeAngle[] edgeAngles = new EdgeAngle[nBelow180];
            for (int i = 0; i < nBelow180; i++)
            {
                EdgeAngle ea = new EdgeAngle(Origin.Index, edge_list[iSorted[i]], angles[i], clockwise);
                edgeAngles[i] = ea; 
            }
             */
            return edgeAnglesFiltered;
        }

        private static IVertex2D TryGetNextCandidate(TriangulationMesh<VERTEX> mesh, ref List<EdgeAngle> sortedCandidates, Baseline baseline, bool Clockwise, out double angle, out GridCircle? circle)
        {
            if (sortedCandidates == null || sortedCandidates.Count == 0)
            {
                circle = new GridCircle?();
                angle = double.MinValue;
                return null;
            }

            EdgeAngle candidate = sortedCandidates[0];
            IVertex2D candidateVert = mesh[candidate.Target];

            while (candidateVert != null)
            {
                angle = candidate.Angle;

                if (sortedCandidates.Count == 0)
                {
                    circle = new GridCircle?();
                    return candidateVert;
                }

                if (baseline.Segment.IsEndpoint(candidateVert.Position))
                {
                    sortedCandidates.RemoveAt(0);
                    continue;
                }

                //When the candidates would create a triangle with an angle sum over 180 degrees there are no more viable candidates on that side to check.
                //However, a changing baseline may make the candidates viable again, so do not remove them.
                if (angle >= Math.PI)
                {
                    circle = new GridCircle?();
                    return null;
                }

                //Check edge cases where we are very near PI to ensure the point is not on the line
                if (angle >= (Math.PI - Global.Epsilon))
                {
                    if (baseline.Segment.IsLeft(candidateVert.Position) == 0)
                    {
                        circle = new GridCircle?();
                        return null;
                    }
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

                EdgeAngle nextCandidate = sortedCandidates[1];
                IVertex2D nextCandidateVert = mesh[nextCandidate.Target];

                if (circle.Value.Contains(nextCandidateVert.Position))
                {
                    //Check edge case of a point exactly on the circle boundary
                    if (GridVector2.Distance(nextCandidateVert.Position, circle.Value.Center) == circle.Value.Radius)
                    {
                        return candidateVert;
                    }

                    //This candidate doesn't work, delete the edge from origin to the candidate and check the next potential candidate.
#if TRACEDELAUNAY
                    Debug.WriteLine(string.Format("Remove Edge: {0}-{1}", baseline.Origin, candidate));
#endif
                    mesh.RemoveEdge(new EdgeKey(baseline.Origin, candidate.Target));
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

        static void CheckEdgeFlip(TriangulationMesh<VERTEX> mesh, TriangleFace f, TriangulationMesh<VERTEX>.ProgressUpdate ReportProgress = null)
        {
            //Check if the face has already been removed.
            if (mesh.Contains(f) == false)
                return;
#if TRACEDELAUNAY

            bool FaceStartedAsDelaunay = mesh.IsTriangleDelaunay(f);
//            Trace.WriteLineIf(FaceStartedAsDelaunay, string.Format("Edge flip test face is Delaunay {0}", f));
#endif

            VERTEX[] verts = f.iVerts.Select(v => mesh[v]).ToArray();
            GridVector2[] circlePoints = verts.Select(v => v.Position).ToArray();

            Debug.Assert(circlePoints.AreClockwise() == false, "Face verts aren't counter-clockwise");

            foreach (var edge in f.Edges)
            {
                TriangleFace oppositeFace = mesh[edge].Faces.Where(face => f != face as Face).FirstOrDefault() as TriangleFace;
                if (oppositeFace == null)
                    continue;

                int other_opposite_vert = oppositeFace.OppositeVertex(edge);

                var flippedEdgeCandidate = mesh.ToGridLineSegment(f.OppositeVertex(edge), other_opposite_vert);
                var existingEdge = mesh.ToGridLineSegment(edge);

                //If the two triangles are not a convex polygon then we need to skip flipping this edge.  Otherwise we will cover an area already
                //covered by another face
                if (flippedEdgeCandidate.Intersects(existingEdge) == false)
                    continue;

                //I should check angles, but have the code written to look at circles and want to test other things
                if (GridCircle.Contains(circlePoints, mesh[other_opposite_vert].Position) == OverlapType.CONTAINED)
                {
                    //OK, need to flip the edge

                    int face_opposite_vert = f.OppositeVertex(edge);
#if TRACEDELAUNAY 
                    Trace.WriteLine(string.Format("Flip {0} with {1}", f, oppositeFace));
                    Debug.WriteLine(string.Format("Remove Edge: {0}", edge));
                    Debug.WriteLine(string.Format("Add Edge: {0}-{1}", face_opposite_vert, other_opposite_vert));

                    Debug.Assert(FaceStartedAsDelaunay == false, string.Format("Edge flip found for a face that was already Delaunay {0}", f));
#endif
                    mesh.RemoveEdge(edge);

                    mesh.AddEdge(new Edge(face_opposite_vert, other_opposite_vert));

                    InfiniteSequentialIndexSet TriangleIndexer = new InfiniteSequentialIndexSet(0, 3, 0);

                    int iA = f.iVerts.IndexOf(face_opposite_vert);
                    int iB = oppositeFace.iVerts.IndexOf(other_opposite_vert);

                    /*
                    int[] AVerts = f.iVerts[(int)TriangleIndexer[iA + 1]] == edge.A ? new int[] { face_opposite_vert, edge.A, other_opposite_vert } : new int[] { face_opposite_vert, other_opposite_vert, edge.A };
                    int[] BVerts = oppositeFace.iVerts[(int)TriangleIndexer[iB + 1]] == edge.B ? new int[] { face_opposite_vert, other_opposite_vert, edge.B } : new int[] { face_opposite_vert, edge.B, other_opposite_vert };

                    TriangleFace A = new TriangleFace(AVerts);
                    TriangleFace B = new TriangleFace(BVerts);
                    */

                    int[] AVerts = new int[] { face_opposite_vert, other_opposite_vert, edge.A };
                    int[] BVerts = new int[] { face_opposite_vert, other_opposite_vert, edge.B };

                    TriangleFace A = mesh.IsClockwise(AVerts) ? new TriangleFace(AVerts.Reverse()) : new TriangleFace(AVerts);
                    TriangleFace B = mesh.IsClockwise(BVerts) ? new TriangleFace(BVerts.Reverse()) : new TriangleFace(BVerts);

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Edge Flip Face {0} Clockwise = {1}", A, mesh.IsClockwise(AVerts)));
                    Trace.WriteLine(string.Format("Edge Flip Face {0} Clockwise = {1}", B, mesh.IsClockwise(BVerts)));
#endif

                    Debug.Assert(mesh.IsClockwise(A) == false, string.Format("New face {0} should be counter-clockwise", A));
                    Debug.Assert(mesh.IsClockwise(B) == false, string.Format("New face {0} should be counter-clockwise", B));

                    //Debug.Assert(mesh.IsTriangleDelaunay(A), string.Format("New triangle should be Delaunay", A));
                    //Debug.Assert(mesh.IsTriangleDelaunay(B), string.Format("New triangle should be Delaunay", B));

                    try
                    {
                        mesh.AddFace(A);
                        mesh.AddFace(B);
                    }
                    catch
                    {
                        Debug.Assert(false);
                        return;
                    }

                    if (ReportProgress != null)
                    {
                        ReportProgress(mesh);
                    }

                    CheckEdgeFlip(mesh, A, ReportProgress);

                    if (mesh.Contains(B)) //Check that the face wasn't removed when checking A for flips
                        CheckEdgeFlip(mesh, B, ReportProgress);

                    return;
                }
            }
        }

        static void CheckEdgeFlip(TriangulationMesh<VERTEX> mesh, Edge edge, TriangulationMesh<VERTEX>.ProgressUpdate ReportProgress = null)
        {
            if (edge.Faces.Count < 2)
                return;

            TriangleFace f = edge.Faces[0] as TriangleFace;
            TriangleFace oppositeFace = edge.Faces[1] as TriangleFace;

            VERTEX[] verts = f.iVerts.Select(v => mesh[v]).ToArray();
            GridVector2[] circlePoints = verts.Select(v => v.Position).ToArray();

            int other_opposite_vert = oppositeFace.OppositeVertex(edge);

            Debug.Assert(f.Edges.All(e => mesh.Contains(e)), "Mesh does not contain face edges");
            Debug.Assert(oppositeFace.Edges.All(e => mesh.Contains(e)), "Mesh does not contain face edges");

            var flippedEdgeCandidate = mesh.ToGridLineSegment(f.OppositeVertex(edge), other_opposite_vert);
            var existingEdge = mesh.ToGridLineSegment(edge);

            //If the two triangles are not a convex polygon then we need to skip flipping this edge.  Otherwise we will cover an area already
            //covered by another face
            if (flippedEdgeCandidate.Intersects(existingEdge) == false)
                return;

            //I should check angles, but have the code written to look at circles and want to test other things
            if (GridCircle.Contains(circlePoints, mesh[other_opposite_vert].Position) == OverlapType.CONTAINED)
            {
                //OK, need to flip the edge
                int face_opposite_vert = f.OppositeVertex(edge);
#if TRACEDELAUNAY
                Trace.WriteLine(string.Format("Flip {0} with {1}", f, oppositeFace));
                Debug.WriteLine(string.Format("Remove Edge: {0}", edge));
                Debug.WriteLine(string.Format("Add Edge: {0}-{1}", face_opposite_vert, other_opposite_vert));
#endif
                IEdge new_edge = new Edge(face_opposite_vert, other_opposite_vert);
                var new_faces = TriangleFace.Flip(edge, new_edge);
                mesh.RemoveEdge(edge);
                mesh.AddEdge(new_edge);

                System.Diagnostics.Debug.Assert(false == mesh.IsClockwise(new_faces.Item1));
                System.Diagnostics.Debug.Assert(false == mesh.IsClockwise(new_faces.Item2));

                mesh.AddFace(new_faces.Item1);
                mesh.AddFace(new_faces.Item2);
                /*
                mesh.RemoveEdge(edge);

                mesh.AddEdge(new Edge(face_opposite_vert, other_opposite_vert));

                InfiniteWrappedIndexSet TriangleIndexer = new InfiniteWrappedIndexSet(0, 3, 0);

                int iA = f.iVerts.IndexOf(face_opposite_vert);
                int iB = oppositeFace.iVerts.IndexOf(other_opposite_vert);

                int[] AVerts = f.iVerts[(int)TriangleIndexer[iA + 1]] == edge.A ? new int[] { face_opposite_vert, edge.A, other_opposite_vert } : new int[] { face_opposite_vert, other_opposite_vert, edge.A };
                int[] BVerts = oppositeFace.iVerts[(int)TriangleIndexer[iB + 1]] == edge.B ? new int[] { face_opposite_vert, other_opposite_vert, edge.B } : new int[] { face_opposite_vert, edge.B, other_opposite_vert };

                if(mesh.IsClockwise(AVerts))
                {
                    AVerts = AVerts.Reverse().ToArray();
                }

                if(mesh.IsClockwise(BVerts))
                {
                    BVerts = BVerts.Reverse().ToArray();
                }

                TriangleFace A = new TriangleFace(AVerts);
                TriangleFace B = new TriangleFace(BVerts);

                Debug.Assert(mesh.IsClockwise(A) == false, string.Format("New face must be counter-clockwise: {0}", A));
                Debug.Assert(mesh.IsClockwise(B) == false, string.Format("New face must be counter-clockwise: {0}", B));

                mesh.AddFace(A);
                mesh.AddFace(B);
                */

                if (ReportProgress != null)
                {
                    ReportProgress(mesh);
                }

                //Debug.Assert(mesh.IsTriangleDelaunay(A), string.Format("New triangle should be Delaunay", A));
                //Debug.Assert(mesh.IsTriangleDelaunay(B), string.Format("New triangle should be Delaunay", B));
                /*
                List<IEdgeKey> EdgesToCheck = A.Edges.Union(B.Edges).ToList();
                foreach (IEdgeKey e in EdgesToCheck)
                {
                    if (mesh.Contains(e))
                        CheckEdgeFlip(mesh, mesh[e] as Edge, ReportProgress);
                }
                */

                CheckEdgeFlip(mesh, new_faces.Item1, ReportProgress);
                if (mesh.Contains(new_faces.Item2)) //Check that the face wasn't removed when checking A for flips
                    CheckEdgeFlip(mesh, new_faces.Item2, ReportProgress);

            }
        }

    }

}
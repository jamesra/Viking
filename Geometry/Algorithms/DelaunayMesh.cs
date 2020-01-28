#define TRACEDELAUNAY

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
        where VERTEX : IVertex2D
    {
        public delegate void ProgressUpdate(TriangulationMesh<VERTEX> mesh);

        /// <summary>
        /// Generates the delaunay triangulation for a list of points. 
        /// Requires the points to be sorted on the X-axis coordinate!
        /// Every the integers in the returned array are the indicies in the passes array of triangles. 
        /// Implemented based upon: http://local.wasp.uwa.edu.au/~pbourke/papers/triangulate/
        /// "Triangulate: Efficient Triangulation Algorithm Suitable for Terrain Modelling"
        /// by Paul Bourke
        /// </summary>
        /// <returns>A Mesh2D whose vertex indicies match the input points</returns>
        public static TriangulationMesh<VERTEX> TriangulateToMesh(VERTEX[] verts, ProgressUpdate ReportProgress = null)
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

            MeshCut subset = new MeshCut(mesh.XSorted, mesh.YSorted, CutDirection.HORIZONTAL, mesh.BoundingBox);

            //try
            //{
                RecursiveDivideAndConquerDelaunay(mesh, subset, ReportProgress: ReportProgress);
            //}
            //catch
            //{
            //    return mesh; 
            //}
            
            foreach(TriangleFace f in mesh.Faces.ToArray())
            {
                if(mesh.Faces.Contains(f) && mesh.IsTriangleDelaunay(f) == false)
                {
                    CheckEdgeFlip(mesh, f, ReportProgress);
                }
            }
           
#if DEBUG
            foreach(Face f in mesh.Faces)
            {
                Debug.Assert(mesh.IsTriangleDelaunay(f), string.Format("{0} is not a delaunay triangle", f));
                Debug.Assert(mesh.IsClockwise(f) == false);
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
        private static TriangulationMesh<VERTEX> RecursiveDivideAndConquerDelaunay(TriangulationMesh<VERTEX> mesh, MeshCut VertSet = null, IVertex2D[] verts = null, ProgressUpdate ReportProgress = null)
        {
            //The first recursion we populate variables to include all the verticies in the mesh
            if (VertSet == null)
            {
                VertSet = new MeshCut(mesh.XSorted, mesh.YSorted, CutDirection.HORIZONTAL, mesh.BoundingBox);
                //VertSet = new ContinuousIndexSet(0, mesh.Verticies.Count);
                //XSortedVerts = mesh.XSorted;
                //YSortedVerts = mesh.YSorted;
            }

            if(verts == null)
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
                if (mesh.ToGridLineSegment(TwoZero).DistanceToPoint(mesh[VertSet[1]].Position) > 0)
                {
                    mesh.AddEdge(new Edge((int)VertSet[2], (int)VertSet[0]));

                    TriangleFace newFace = new TriangleFace((int)VertSet[0], (int)VertSet[1], (int)VertSet[2]);

                    if(mesh.IsClockwise(newFace))
                    {
                        newFace = new TriangleFace((int)VertSet[0], (int)VertSet[2], (int)VertSet[1]);
                    }

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

            VERTEX L, R;

            //TODO: The first vertex needs to be the index sorted on the opposite axis as the cut. 
            L = mesh[FirstHalfSet.SortedAlongCutAxisVertSet.First()];
            R = mesh[SecondHalfSet.SortedAlongCutAxisVertSet.First()];

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

                EdgeAngle[] L_CW_Candidates  = EdgesByAngle(mesh, L, R.Index, true);
                EdgeAngle[] R_CCW_Candidates = EdgesByAngle(mesh, R, L.Index, false);

                //I can't find a case where the correct base LR edge is going to have an angle beyond 90 degrees.  Ideally I should only check for an angle
                //less than the angle from the origin line to the axis, but that angle is always less than 90.

                //Clockwise is from the testAngleAxisLine to the point.
#if TRACEDELAUNAY
                if (FirstHalfSet.CutAxis == CutDirection.HORIZONTAL)
                {
                    string s = string.Format("Horizontal: Left | Right reversed {0} | {1}", FirstHalfSet, SecondHalfSet);
                    Trace.WriteLineIf(L.Position.Y > R.Position.Y, s);
                    Debug.Assert(L.Position.Y < R.Position.Y, s);
                }
                else
                {
                    string s = string.Format("Vertical: Left | Right reversed {0} | {1}", FirstHalfSet, SecondHalfSet);
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
                if(L_CW_Candidates.Length > 0)
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Reject Baseline: {0}-{1} for {2}", L.Index, R.Index, L_CW_Candidates.First().Target));
#endif
                    L = mesh[L_CW_Candidates.First().Target];
                    BaselineFound = false;
                }
                
                if(R_CCW_Candidates.Length > 0)
                {
#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Reject Baseline: {0}-{1} for {2}", L.Index, R.Index, R_CCW_Candidates.First().Target));
#endif
                    R = mesh[R_CCW_Candidates.First().Target];
                    BaselineFound = false;
                }

                if(!BaselineFound)
                {
                    continue; 
                }

                Edge baseEdge = new Edge(L.Index, R.Index);
                mesh.AddEdge(baseEdge);
                AddedEdges.Add(baseEdge);

#if TRACEDELAUNAY
                Trace.WriteLine(string.Format("Add Baseline: {0}-{1}", L.Index, R.Index));
#endif 
                break;
            }

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

                    mesh.AddFace(newFace);

                    //CheckEdgeFlip(mesh, newFace);
                    AddedFaces.Add(newFace);
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

                    mesh.AddFace(newFace);
                    //CheckEdgeFlip(mesh, newFace);
                    AddedFaces.Add(newFace);

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
            foreach(IEdgeKey edge in EdgesToCheck)
            {
                if (mesh.Contains(edge))
                    CheckEdgeFlip(mesh, mesh[edge] as Edge, ReportProgress);
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
        public static EdgeAngle[] EdgesByAngle(TriangulationMesh<VERTEX> mesh, IVertex2D Origin, long origin_edge_target, bool clockwise)
        {
            //Setting the comparer should update the order of the edges attribute only if necessary.
            GridVector2 target = mesh[origin_edge_target].Position;
            MeshEdgeAngleComparerFixedIndex<VERTEX> angleComparer = new MeshEdgeAngleComparerFixedIndex<VERTEX>(mesh, Origin.Index, new GridLine(Origin.Position, target - Origin.Position), clockwise);

            List<long> edge_list = Origin.Edges.Select(e => e.OppositeEnd((long)Origin.Index)).Where(e => e != origin_edge_target).ToList();
            EdgeAngle[] edgeAngles = edge_list.Select(edge => new EdgeAngle(Origin.Index, edge, angleComparer.MeasureAngle(edge), clockwise)).Where(edge => edge.Angle > 0 && edge.Angle < Math.PI).ToArray();

            Array.Sort(edgeAngles.Select(e => e.Angle).ToArray(), edgeAngles);


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
            return edgeAngles;
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

                if(sortedCandidates.Count == 0)
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
                    if(GridVector2.Distance(nextCandidateVert.Position, circle.Value.Center) == circle.Value.Radius)
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

        static void CheckEdgeFlip(TriangulationMesh<VERTEX> mesh, TriangleFace f, ProgressUpdate ReportProgress = null)
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

                    InfiniteWrappedIndexSet TriangleIndexer = new InfiniteWrappedIndexSet(0, 3, 0);

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

                    if(mesh.Contains(B)) //Check that the face wasn't removed when checking A for flips
                        CheckEdgeFlip(mesh, B, ReportProgress);

                    return;
                } 
            }
        }

        static void CheckEdgeFlip(TriangulationMesh<VERTEX> mesh, Edge edge, ProgressUpdate ReportProgress = null)
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

                CheckEdgeFlip(mesh, A);
                if (mesh.Contains(B)) //Check that the face wasn't removed when checking A for flips
                    CheckEdgeFlip(mesh, B, ReportProgress);
                    
            }
        } 
        
    }

    class MeshCut
    {
        public GridRectangle BoundingBox;

        public readonly CutDirection CutAxis;

        public long[] XSortedVerts;
        public long[] YSortedVerts;

        /// <summary>
        /// Used for quick Contains tests
        /// </summary>
        private HashSet<long> _AllVerts;

        /// <summary>
        /// When set to true, the XSortedVerts with equal X values are sorted by ascending Y value, otherwise by descending Y value
        /// </summary>
        public bool XSecondAxisAscending = true;

        /// <summary>
        /// When set to true, the YSortedVerts with equal Y values are sorted by ascending X value, otherwise by descending X value
        /// </summary>
        public bool YSecondAxisAscending = true;

        public long Count { get { return XSortedVerts.LongLength; } }

        public bool Contains(long value)
        {
            return _AllVerts.Contains(value);
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

        public MeshCut(long[] SortedAlongAxis, long[] SortedOppositeAxis, CutDirection cutAxis, GridRectangle boundingRect)
        {
            CutAxis = cutAxis;
            XSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedAlongAxis : SortedOppositeAxis;
            YSortedVerts = cutAxis == CutDirection.HORIZONTAL ? SortedOppositeAxis : SortedAlongAxis;
            BoundingBox = boundingRect;

            _AllVerts = new HashSet<long>(SortedAlongAxis);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach(long index in SortedAlongCutAxisVertSet)
            {
                sb.AppendFormat("{0} ", index);
            }

            return sb.ToString();
        }


        public void SplitIntoHalves(IReadOnlyList<IVertex2D> mesh, out MeshCut LowerSubset, out MeshCut UpperSubset)
        {
            //Split the verticies into smaller groups and then merge the resulting triangulations
            CutDirection cutDirection = BoundingBox.Width > BoundingBox.Height ? CutDirection.VERTICAL : CutDirection.HORIZONTAL;

            long[] NewSortedAlongCutAxisVertSet;
            long[] NewSortedOppositeCutAxisVertSet;

            bool AxisAscending; 

            //Sort our verticies according to the new direction
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                //Use the mesh's ordering arrays to determine the new sorted vertex order
                NewSortedAlongCutAxisVertSet = XSortedVerts;
                NewSortedOppositeCutAxisVertSet = YSortedVerts;
                AxisAscending = XSecondAxisAscending;
            }
            else
            {
                NewSortedAlongCutAxisVertSet = YSortedVerts;
                NewSortedOppositeCutAxisVertSet = XSortedVerts;
                AxisAscending = YSecondAxisAscending;
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
            long nLowerHalf = NewSortedAlongCutAxisVertSet.LongLength / 2;
            long nUpperHalf = NewSortedAlongCutAxisVertSet.LongLength - nLowerHalf;
                        
            long[] LowerHalfAlongAxis = new long[nLowerHalf];
            long[] UpperHalfAlongAxis = new long[nUpperHalf];
            long[] LowerHalfOppAxis = new long[LowerHalfAlongAxis.LongLength];
            long[] UpperHalfOppAxis = new long[UpperHalfAlongAxis.LongLength];

            Array.Copy(NewSortedOppositeCutAxisVertSet, LowerHalfOppAxis, LowerHalfOppAxis.LongLength);
            Array.Copy(NewSortedOppositeCutAxisVertSet, LowerHalfOppAxis.LongLength, UpperHalfOppAxis, 0, UpperHalfOppAxis.LongLength);

            //GridVector2 DivisionPoint = (mesh[(int)FirstHalfOppAxis.Last()].Position + mesh[(int)SecondHalfAlongAxis.First()].Position) / 2.0;
            GridVector2 DivisionPoint = mesh[(int)LowerHalfOppAxis.Last()].Position;

            //Divide the opposite axis sorted set into two groups as well
            long iLowerHalfAdd = 0;
            long iUpperHalfAdd = 0;

#if TRACEDELAUNAY
            Debug.WriteLine(string.Format("{0}--------{1}-------",cutDirection, DivisionPoint));
#endif
            GridVector2[] vertPosArray = NewSortedAlongCutAxisVertSet.Select(i => mesh[(int)i].Position).ToArray();
            for (long i = 0; i < NewSortedAlongCutAxisVertSet.LongLength; i++)
            {
                long iVert = NewSortedAlongCutAxisVertSet[i];
                GridVector2 vertPos = vertPosArray[i];//mesh[(int)iVert].Position;
                bool AssignToLower = false; 
                if (cutDirection == CutDirection.HORIZONTAL)
                {
                    if (vertPos.Y < DivisionPoint.Y)
                    {
                        AssignToLower = true;
                    }
                    else if (vertPos.Y == DivisionPoint.Y)
                    {
                        AssignToLower = iVert == LowerHalfOppAxis.Last() || LowerHalfOppAxis.Contains(iVert);
                    }
                    else
                    {
                        AssignToLower = false;
                    }
                }
                else
                {
                    if (vertPos.X < DivisionPoint.X)
                    {
                        AssignToLower = true;
                    }
                    else if(vertPos.X == DivisionPoint.X)
                    {
                        AssignToLower = iVert == LowerHalfOppAxis.Last() || LowerHalfOppAxis.Contains(iVert);
                    }
                    else
                    {
                        AssignToLower = false;
                    }
                }

                if(AssignToLower)
                {
#if TRACEDELAUNAY
                    Debug.WriteLine(string.Format("1st <- {0}: {1}", iVert, vertPos));
#endif

                    LowerHalfAlongAxis[iLowerHalfAdd] = iVert;
                    iLowerHalfAdd += 1;
                }
                else
                {

#if TRACEDELAUNAY
                    Debug.WriteLine(string.Format("2nd <- {0}: {1}", iVert, vertPos));
#endif

                    UpperHalfAlongAxis[iUpperHalfAdd] = iVert;
                    iUpperHalfAdd += 1;
                }
            }
             
            GridRectangle LowerHalfBBox;
            GridRectangle UpperHalfBBox;
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                LowerHalfBBox = new GridRectangle(BoundingBox.Left, BoundingBox.Right, mesh[(int)LowerHalfOppAxis[0]].Position.Y, mesh[(int)LowerHalfOppAxis.Last()].Position.Y);
                UpperHalfBBox = new GridRectangle(BoundingBox.Left, BoundingBox.Right, mesh[(int)UpperHalfOppAxis[0]].Position.Y, mesh[(int)UpperHalfOppAxis.Last()].Position.Y);

                LowerSubset = new MeshCut(LowerHalfAlongAxis, LowerHalfOppAxis, cutDirection, LowerHalfBBox);
                UpperSubset = new MeshCut(UpperHalfAlongAxis, UpperHalfOppAxis, cutDirection, UpperHalfBBox);
            }
            else
            {
                LowerHalfBBox = new GridRectangle(mesh[(int)LowerHalfOppAxis[0]].Position.X, mesh[(int)LowerHalfOppAxis.Last()].Position.X, BoundingBox.Bottom, BoundingBox.Top);
                UpperHalfBBox = new GridRectangle(mesh[(int)UpperHalfOppAxis[0]].Position.X, mesh[(int)UpperHalfOppAxis.Last()].Position.X, BoundingBox.Bottom, BoundingBox.Top);

                LowerSubset = new MeshCut(LowerHalfAlongAxis, LowerHalfOppAxis, cutDirection, LowerHalfBBox);
                UpperSubset = new MeshCut(UpperHalfAlongAxis, UpperHalfOppAxis, cutDirection, UpperHalfBBox);
            }
#if DEBUG
            if (cutDirection == CutDirection.HORIZONTAL)
            {
                string s = string.Format("Horizontal: Left | Right reversed {0} | {1}", LowerSubset, UpperSubset);
                Trace.WriteLineIf(mesh[(int)LowerSubset.Verticies[0]].Position.Y > mesh[(int)UpperSubset.Verticies[0]].Position.Y, s);
                Debug.Assert(mesh[(int)LowerSubset.Verticies[0]].Position.Y < mesh[(int)UpperSubset.Verticies[0]].Position.Y, s);
            }
            else
            {
                string s = string.Format("Vertical: Left | Right reversed {0} | {1}", LowerSubset, UpperSubset);
                Trace.WriteLineIf(mesh[(int)LowerSubset.Verticies[0]].Position.X > mesh[(int)UpperSubset.Verticies[0]].Position.X, s);
                Debug.Assert(mesh[(int)LowerSubset.Verticies[0]].Position.X < mesh[(int)UpperSubset.Verticies[0]].Position.X, s);
            }
#endif
            LowerSubset.SortSecondAxis(mesh, true);
            UpperSubset.SortSecondAxis(mesh, false);
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
                XSecondAxisAscending = ascending;
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
                YSecondAxisAscending = ascending;
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
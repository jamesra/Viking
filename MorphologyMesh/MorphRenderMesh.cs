using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry.Meshing;
using Geometry;

namespace MorphologyMesh
{
    

    public class MorphMeshVertex : Vertex
    {
        /// <summary>
        /// Verticies we add to close holes will not have a poly index
        /// </summary>
        public PointIndex? PolyIndex;

        /// <summary>
        /// Set to true if this vertex has a continuous wall of faces to the adjacent verticies in the shape
        /// </summary>
        public bool FacesAreComplete = false;

        public MorphMeshVertex(PointIndex? polyIndex, GridVector3 p) : base(p)
        {
            PolyIndex = polyIndex;
        }

        public MorphMeshVertex(PointIndex? polyIndex, GridVector3 p, GridVector3 n) : base(p, n)
        {
            PolyIndex = polyIndex;
        }

        public static IVertex Duplicate(IVertex old)
        {
            MorphMeshVertex vert = old as MorphMeshVertex;
            if (vert != null)
            {
                return new MorphMeshVertex(vert.PolyIndex, vert.Position, vert.Normal);
            }

            return new Vertex(old.Position, old.Normal);
        }

        /// <summary>
        /// Return true if there are continuos faces between the two adjacent verticies along the contour this vertex is part of
        /// </summary>
        /// <param name="mesh"></param>
        public bool IsFaceSurfaceComplete(MorphRenderMesh mesh)
        {
            if (!PolyIndex.HasValue)
                return true;

            if (FacesAreComplete)
                return true;

            PointIndex prev = PolyIndex.Value.Previous;
            PointIndex next = PolyIndex.Value.Next;

            MorphMeshVertex prevVertex = mesh[prev];
            MorphMeshVertex nextVertex = mesh[next];

            IEnumerable<IEdgeKey> startEdges = this.Edges.Where(e => mesh[e].Contains(prevVertex.Index));
            if(!startEdges.Any())
                return false;

            IEnumerable<IEdgeKey> endingEdges = this.Edges.Where(e => mesh[e].Contains(nextVertex.Index));
            if (!endingEdges.Any())
                return false;

            MorphMeshEdge start = mesh[startEdges.First()];
            MorphMeshEdge end = mesh[endingEdges.First()];

            //OK, walk the faces and determine if there is a path from the starting edge to the ending edge

            if (start.Faces.Count == 0)
                return false;

            //We expect the starting vertex to be a contour vertex
            Debug.Assert(start.Type == EdgeType.CONTOUR);

            //TODO: We probably need to ensure the path doesn't wrap all the away around the contours the long way at this step instead of later
            List<IFace> path = mesh.FindFacesInPath(start.Faces.First(), (face) => face.iVerts.Contains(this.Index), (face) => face.Edges.Contains(end));
            if (path == null)
                return false;

            //Check that every face in the shortest path always includes the vertex we are testing.
            FacesAreComplete = path.All(f => f.iVerts.Contains(this.Index));
            return FacesAreComplete;
        }
    }


    public class MorphMeshEdge : Edge, IEquatable<MorphMeshEdge>
    {
        public EdgeType Type;

        public bool MatchingOrientation = false; //True if this edge outside of one shape and inside another

        public MorphMeshEdge(EdgeType type, int A, int B) : base(A, B)
        {
            Type = type;
        }

        public ImmutableSortedSet<MorphMeshFace> Faces
        {
            get
            {
                return new SortedSet<MorphMeshFace>(this._Faces.Select(f => (MorphMeshFace)f)).ToImmutableSortedSet();
            }
        }

        public static new IEdge Duplicate(IEdge old, int A, int B)
        {
            MorphMeshEdge edge = old as MorphMeshEdge;
            if (edge != null)
                return new MorphMeshEdge(edge.Type, A, B);

            return new MorphMeshEdge(EdgeType.UNKNOWN, A, B);
        }

        public bool Equals(MorphMeshEdge other)
        {
            return base.Equals(other);
        }
    }

    public class MorphMeshFace : Face
    {
        /// <summary>
        /// Records if the face is part of a specific region type
        /// </summary>
        public RegionType Type { get; private set; }

        public MorphMeshFace(int A, int B, int C) : base(A, B, C)
        {
        }

        public MorphMeshFace(IEnumerable<int> verts) : base(verts)
        {
        }


        public IEnumerable<MorphMeshFace> AdjacentFaces(MorphRenderMesh mesh)
        { /*
            IEdge[] edges = this.Edges.Select(e => mesh.Edges[e]).ToArray();
            IFace[] Faces = edges.SelectMany(e => mesh.Edges[e].Faces).ToArray();
            IFace[] Adjacent = Faces.Where(f => f != (IFace)this).ToArray();
            return Adjacent.Select(f => (MorphMeshFace)f).ToArray();
            */
            return this.Edges.SelectMany(e => mesh.Edges[e].Faces.Where(f => f != (IFace)this)).Select(f => (MorphMeshFace)f);
        }

        /// <summary>
        /// Returns true if all verticies in the face share a Z value.  If they do not the out parameter has no value.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        public bool AllVertsAtSameZ(MorphRenderMesh mesh, out double? Z)
        {

            MorphMeshVertex[] verts = this.iVerts.Select(i => (MorphMeshVertex)mesh[i]).ToArray();
            double ExpectedZ = verts.First().Position.Z;
            if (!verts.All(v => v.Position.Z == ExpectedZ))
            {
                Z = new double?();
                return false;
            }
            Z = ExpectedZ;
            return true;
        }

        public bool IsInExposedRegion(MorphRenderMesh mesh)
        {
            return IsInExposedRegion(mesh, this);
        }

        public static bool IsInExposedRegion(MorphRenderMesh mesh, IFace face)
        {
            var edges = face.Edges.Select(e => (MorphMeshEdge)mesh.Edges[e]);
            var EdgeTypes = edges.Select(e => e.Type).ToArray();
            int countInternal = EdgeTypes.Count(e => e == EdgeType.FLAT);
            int countDirection = EdgeTypes.Count(e => e == EdgeType.FLIPPED_DIRECTION);
            if (countInternal + countDirection == 0)
                return false;

            

            int countValid = EdgeTypes.Count(e => (e & EdgeType.VALID) > 0);
            if (countValid > 1)
                return false;

            return countInternal + countValid + countDirection == 3;
        }

        public bool IsInInvaginatedRegion(MorphRenderMesh mesh)
        {
            return IsInInvaginatedRegion(mesh, this);
        }

        public static bool IsInInvaginatedRegion(MorphRenderMesh mesh, IFace face)
        {
            var edges = face.Edges.Select(e => (MorphMeshEdge)mesh.Edges[e]);
            var EdgeTypes = edges.Select(e => e.Type).ToArray();
            int countInternal = EdgeTypes.Count(e => e == EdgeType.INVAGINATION);
            if (countInternal == 0)
                return false;

            int countValid = EdgeTypes.Count(e => e == EdgeType.CONTOUR);
            if (countValid != 2)
            {
                countValid = EdgeTypes.Count(e => (e & EdgeType.VALID) > 0);
                if (countValid > 1)
                    return false;
            }

            //return EdgeTypes.Any(e => e == EdgeType.INTERNAL) && (EdgeTypes.Count(e => (e & EdgeType.VALID) > 0) == 2);
            return countInternal + countValid == 3;
        }

        public bool IsInHoleRegion(MorphRenderMesh mesh)
        {
            return IsInHoleRegion(mesh, this);
        }

        public static bool IsInHoleRegion(MorphRenderMesh mesh, IFace face)
        {
            var edges = face.Edges.Select(e => (MorphMeshEdge)mesh.Edges[e]);
            var EdgeTypes = edges.Select(e => e.Type).ToArray();
            int countInternal = EdgeTypes.Count(e => e == EdgeType.HOLE);
            if (countInternal == 0)
                return false;

            int countValid = EdgeTypes.Count(e => (e & EdgeType.VALID) > 0);
            if (countValid > 1)
                return false;

            //return EdgeTypes.Any(e => e == EdgeType.INTERNAL) && (EdgeTypes.Count(e => (e & EdgeType.VALID) > 0) == 2);
            return countInternal + countValid == 3;
        }

        public static bool IsSurfaceEdge(EdgeType t)
        {
            switch (t)
            {
                case EdgeType.CONTOUR:
                case EdgeType.CORRESPONDING:
                case EdgeType.VALID:
                case EdgeType.SURFACE:
                    return true;
                default:
                    return false;
            }

        }

        public bool IsSurface(MorphRenderMesh mesh)
        {
            var edges = this.Edges.Select(e => (MorphMeshEdge)mesh.Edges[e]);
            return edges.All(e => IsSurfaceEdge(e.Type));
        }

        public static IFace Duplicate(IFace old, int[] iVerts)
        {
            MorphMeshFace newFace = new MorphMeshFace(iVerts);
            return newFace;
        }
    }

    /// <summary>
    /// A set of faces that represent a region which needs to be mapped to the adjacent section or triangulated and assigned a flat mesh
    /// </summary>
    public class MorphMeshRegion : IComparable<MorphMeshRegion>, IEquatable<MorphMeshRegion>

    {
        private MorphRenderMesh ParentMesh;

        private SortedSet<MorphMeshFace> _Faces;
        public SortedSet<MorphMeshFace> Faces
        {
            get { return _Faces; }
        }

        public RegionType Type { get; private set; }

        public MorphMeshRegion(MorphRenderMesh mesh, IEnumerable<MorphMeshFace> faces, RegionType type)
        {
            ParentMesh = mesh;
            _Faces = new SortedSet<MorphMeshFace>(faces);
            Type = type;
        }


        /// <summary>
        /// Invaginations must have only one open end.  There are times when edges are reported as invaginations when in fact they are bridges which are not true regions. 
        /// HOwever bridges have two edges that are 
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        static internal bool IsValidInvagination(MorphMeshRegion region)
        {
            Debug.Assert(region.Type == RegionType.INVAGINATION);

            IEnumerable<MorphMeshEdge> RegionEdges = region.Faces.SelectMany(f => f.Edges).Distinct().Select(key => (MorphMeshEdge)region.ParentMesh.Edges[key]);

            //We are looking for two region faces that have an edge with a non-region face or only one face (On the convex hull).  If there are two this is a bridge and not an invagination
            IEnumerable<MorphMeshEdge> CandidateEdges = RegionEdges.Where(e => e.Type != EdgeType.CONTOUR);

            List<MorphMeshEdge> ExposedEdges = CandidateEdges.Where(e => e.Faces.Count == 1 || e.Faces.Any(face => !region.Contains(face))).ToList();

            if (ExposedEdges.Count() > 1)
                return false;

            return true;
        }

        public double Z
        {
            get
            {
                if (Faces.Count == 0)
                    throw new ArgumentException("No faces in region");

                return ParentMesh[Faces.First().iVerts.First()].Position.Z;
            }
        }

        private GridPolygon _Polygon = null;

        public GridPolygon Polygon
        {
            get
            {
                if (_Polygon != null)
                    return _Polygon;
                 

                PointIndex[] polyIndicies = Verticies.Select(v => ((MorphMeshVertex)ParentMesh.Verticies[v]).PolyIndex.Value).ToArray();


                //If the polygon verticies contact both segments of inner and outer verticies we must
                //determine how to connect the segments without creating a self-intersecting polygon
                bool IsFirstInner = polyIndicies[0].IsInner;
                if (polyIndicies.Any(pi => pi.IsInner != IsFirstInner))
                {
                    Dictionary<PointIndex, int> PolyIndexToMeshIndex = new Dictionary<PointIndex, int>();
                    for (int i = 0; i < polyIndicies.Length; i++)
                    {
                        PolyIndexToMeshIndex.Add(polyIndicies[i], Verticies[i]);
                    }

                    //Identify the poly-lines and determine how they connect
                    List<PointIndex[]> contours = IdentifyContours(polyIndicies);
                    PointIndex[] finalIndicies = ConnectContours(contours, PolyIndexToMeshIndex);
                    int[] debugMeshIndicies = finalIndicies.Select(i => PolyIndexToMeshIndex[i]).ToArray();
                    GridVector2[] points = finalIndicies.Select(i => ParentMesh.Verticies[PolyIndexToMeshIndex[i]].Position.XY()).ToArray();
                    _Polygon = new GridPolygon(points.EnsureClosedRing());
                }
                else
                {
                    //Sort the polyIndices
                    int[] sorted_polyIndicies = polyIndicies.SortAndIndex();
                    int[] mesh_indicies = sorted_polyIndicies.Select(i => Verticies[i]).ToArray();


                    //Simple case, all verticies are on the same ring
                    GridVector2[] points = mesh_indicies.Select(i => ParentMesh.Verticies[i].Position.XY()).ToArray();
                    _Polygon = new GridPolygon(points.EnsureClosedRing());
                }

                return _Polygon;
            }
        }

        private MorphMeshVertex[] _RegionPerimeter = null; //The region indicies organized so they progress in order around the perimeter of the region
        public MorphMeshVertex[] RegionPerimeter
        {
            get
            {
                if (_RegionPerimeter != null)
                    return _RegionPerimeter;

                PointIndex[] polyIndicies = Verticies.Select(v => ((MorphMeshVertex)ParentMesh.Verticies[v]).PolyIndex.Value).ToArray();
                 
                //If the polygon verticies contact both segments of inner and outer verticies we must
                //determine how to connect the segments without creating a self-intersecting polygon
                bool IsFirstInner = polyIndicies[0].IsInner;
                if (polyIndicies.Any(pi => pi.IsInner != IsFirstInner))
                {
                    Dictionary<PointIndex, int> PolyIndexToMeshIndex = new Dictionary<PointIndex, int>();
                    for (int i = 0; i < polyIndicies.Length; i++)
                    {
                        PolyIndexToMeshIndex.Add(polyIndicies[i], Verticies[i]);
                    }

                    //Identify the poly-lines and determine how they connect
                    List<PointIndex[]> contours = IdentifyContours(polyIndicies);
                    _RegionPerimeter = ConnectContours(contours, PolyIndexToMeshIndex).Select(pi => ParentMesh[pi]).ToArray();
                }
                else
                {
                    //Simple case, all verticies are on the same ring

                    //Sort the polyIndices
                    int[] sorted_polyIndicies = polyIndicies.SortAndIndex();
                    int[] mesh_indicies = sorted_polyIndicies.Select(i => Verticies[i]).ToArray();

                    _RegionPerimeter = mesh_indicies.Select(i => (MorphMeshVertex)ParentMesh.Verticies[i]).ToArray();
                }

                return _RegionPerimeter;
            }
        }

        private static List<PointIndex[]> IdentifyContours(PointIndex[] polyIndicies)
        {
            //Make sure we don't have artificial jumps in the array at 0 indicies. i.e. A line that wraps around the end to the beginning of the ring
            polyIndicies = PointIndex.SortByRing(polyIndicies); 

            List<PointIndex[]> listContours = new List<PointIndex[]>();

            List<PointIndex> contour = new List<PointIndex>();
            contour.Add(polyIndicies[0]);
            for(int i = 1; i < polyIndicies.Length; i++)
            {
                PointIndex lastCountourPoint = contour.Last();
                PointIndex pi = polyIndicies[i];
                //if (pi.iInnerPoly != lastCountourPoint.iInnerPoly || pi.iPoly != lastCountourPoint.iPoly)
                if(!lastCountourPoint.AreAdjacent(pi))
                {
                    listContours.Add(contour.ToArray());
                    contour = new List<PointIndex>();
                    contour.Add(pi);
                }
                else
                {
                    contour.Add(pi);
                }
            }

            //If we started in the middle of a contour due to the indicies wrapping around we prepend the last contour
            //to the first contour in the list
            if (contour.Last().AreAdjacent(listContours.First()[0]))
                listContours.First().Union(contour);
            else
                listContours.Add(contour.ToArray());

            return listContours;
        }

        private PointIndex[] ConnectContours(List<PointIndex[]> contours, Dictionary<PointIndex, int> PolyIndexToMeshIndex)
        {
            List<PointIndex> AssembledContour = new List<PointIndex>(); 
            
            PointIndex[] lastContour = contours[0];
            AssembledContour.AddRange(lastContour);

            GridVector2[] lastContourEndpoints = ContourEndpoints(lastContour, PolyIndexToMeshIndex);
            
            for (int i = 1; i < contours.Count; i++)
            {
                PointIndex[] Contour = contours[i];
                if (Contour.Length == 1)
                {
                    AssembledContour.AddRange(Contour);
                }
                else
                {
                    GridVector2[] Endpoints = ContourEndpoints(Contour, PolyIndexToMeshIndex);

                    GridLineSegment B = new GridLineSegment(lastContourEndpoints[1], Endpoints[0]);
                    GridLineSegment A = new GridLineSegment(lastContourEndpoints[0], Endpoints[1]);

                    //If the line crosses then we need to reverse the contour before adding it to the output
                    if (A.Intersects(B))
                    {
                        lastContour = Contour.Reverse().ToArray();
                    }
                    else
                    {
                        lastContour = Contour;
                    }

                    AssembledContour.AddRange(lastContour); 
                }

                lastContourEndpoints = ContourEndpoints(AssembledContour, PolyIndexToMeshIndex);
            }

            return AssembledContour.ToArray();
        }

        GridVector2[] ContourEndpoints(IReadOnlyList<PointIndex> contour, Dictionary<PointIndex, int> PolyIndexToMeshIndex)
        {
            int iStart = PolyIndexToMeshIndex[contour[0]];
            int iEnd = PolyIndexToMeshIndex[contour.Last()];

            return new GridVector2[]
                { this.ParentMesh.Verticies[iStart].Position.XY(),
                  this.ParentMesh.Verticies[iEnd].Position.XY() };
        }

        private int[] _Verticies = null;
        /// <summary>
        /// Return region verticies in no particular order
        /// </summary>
        public int[] Verticies
        {
            get
            {
                if(_Verticies == null)
                    _Verticies = Faces.SelectMany(f => f.iVerts).Distinct().ToArray();

                return _Verticies;
            }
        }

        public GridVector3[] VertPositions
        {
            get
            {
                return Verticies.Select(v => ParentMesh.Verticies[v].Position).ToArray();
            }
        }

        /// <summary>
        /// Return true if this regions polygons is entirely outside any polygons on the adjacent section
        /// </summary>
        /// <returns></returns>
        public bool IsExposed(MorphRenderMesh mesh)
        {
            GridPolygon[] AdjacentPolys = mesh.Polygons.Where((p, i) => mesh.PolyZ[i] != this.Z).ToArray();

            if(AdjacentPolys.Any(p => p.Contains(this.Polygon)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Return true if this regions polygons is entirely outside any polygons on the adjacent section
        /// </summary>
        /// <returns></returns>
        public bool IsPartlyExposed(MorphRenderMesh mesh)
        {
            GridPolygon[] AdjacentPolys = mesh.Polygons.Where((p, i) => mesh.PolyZ[i] != this.Z).ToArray();

            if (AdjacentPolys.Any(p => p.Intersects(this.Polygon) && !p.Contains(this.Polygon)))
            {
                return true;
            }

            return false;
        }



        public double NearestDistance(MorphMeshRegion other)
        {
            return this.Polygon.Distance(other.Polygon);
        }

        public bool Contains(IFace face)
        {
            return this.Faces.Contains(face); 
        }

        public int CompareTo(MorphMeshRegion other)
        {
            if(this.Faces.Count != other.Faces.Count)
            {
                return other.Faces.Count - this.Faces.Count;
            }

            MorphMeshFace[] Mine = Faces.ToArray();
            MorphMeshFace[] Theirs = other.Faces.ToArray();

            for (int i= 0; i < this.Faces.Count; i++)
            {
                int comparison = Mine[i].CompareTo(Theirs[i]);
                if (comparison != 0)
                    return comparison;
            }

            return 0; 
        }

        public bool Equals(MorphMeshRegion other)
        {
            return this.Faces.SetEquals(other.Faces);
        }

        public override string ToString()
        {
            return string.Format("Reg: {0} {1}", this.Faces.First(), this.Faces.Count);
        }
    }

    public class MorphMeshRegionGraph : GraphLib.Graph<MorphMeshRegion, GraphLib.Node<MorphMeshRegion, MorphMeshRegionGraphEdge>, MorphMeshRegionGraphEdge>
    { }

    public class MorphMeshRegionGraphEdge : GraphLib.Edge<MorphMeshRegion>
    {
        public MorphMeshRegionGraphEdge(MorphMeshRegion SourceNode, MorphMeshRegion TargetNode) : base(SourceNode, TargetNode, false)
        {
        } 
    }



    /// <summary>
    /// A 3D mesh that records the polygons used to construct the mesh and tracks the original polygonal index of every vertex and the type of edge connecting verticies.
    /// </summary>
    public class MorphRenderMesh : DynamicRenderMesh
    {
        public GridPolygon[] Polygons { get; private set; }

        public double[] PolyZ { get; private set; }

        private List<MorphMeshRegion> _Regions = new List<MorphMeshRegion>();

        public List<MorphMeshRegion> Regions { get; private set; }

        private Dictionary<PointIndex, long> PolyIndexToVertex = new Dictionary<PointIndex, long>();

        private Dictionary<double, List<GridPolygon>> PolygonsByZ = new Dictionary<double, List<GridPolygon>>();
         
        public MorphRenderMesh(GridPolygon[] polygons, double[] ZLevels)
        {
            Debug.Assert(polygons.Length == ZLevels.Length);
            Polygons = polygons;
            PolyZ = ZLevels;
            this.DuplicateVertex = MorphMeshVertex.Duplicate;
            this.DuplicateEdge = MorphMeshEdge.Duplicate;
            this.DuplicateFace = MorphMeshFace.Duplicate;

            foreach(double Z in PolyZ.Distinct())
            {
                PolygonsByZ.Add(Z, new List<GridPolygon>());
            }

            for(int i = 0; i < PolyZ.Length; i++)
            {
                PolygonsByZ[PolyZ[i]].Add(Polygons[i]);
            }

            PopulateMesh(this);
        }

        private static void PopulateMesh(MorphRenderMesh mesh)
        {
            foreach (PointIndex i1 in new PolySetVertexEnum(mesh.Polygons))
            {
                MorphMeshVertex v = new MorphMeshVertex(i1, i1.Point(mesh.Polygons).ToGridVector3(mesh.PolyZ[i1.iPoly]));
                mesh.AddVertex(v);
            }

            foreach (PointIndex i1 in new PolySetVertexEnum(mesh.Polygons))
            {
                PointIndex next = i1.Next;
                MorphMeshEdge edge = new MorphMeshEdge(EdgeType.CONTOUR, mesh[i1].Index, mesh[next].Index);
                mesh.AddEdge(edge);
            }
        }

        public List<GridPolygon> GetSameLevelPolygons(PointIndex key)
        {
            double PointZ = PolyZ[key.iPoly];
            return PolygonsByZ[PointZ];
        }

        public List<GridPolygon> GetAdjacentLevelPolygons(PointIndex key)
        {
            double PointZ = PolyZ[key.iPoly];
            double OtherZ = PolyZ.Where(z => z != PointZ).First();
            return PolygonsByZ[OtherZ];
        }

        public new MorphMeshVertex this[int key]
        {
            get { return (MorphMeshVertex)this.Verticies[key]; }
        }

        public new MorphMeshEdge this[IEdgeKey key]
        {
            get { return (MorphMeshEdge)this.Edges[key]; }
        }

        public virtual MorphMeshVertex this[PointIndex key]
        {
            get
            {
                return (MorphMeshVertex)Verticies[(int)PolyIndexToVertex[key]];
            }
            set
            {
                Verticies[(int)PolyIndexToVertex[key]] = value;
            }
        }

        public virtual bool Contains(PointIndex key)
        {
            return PolyIndexToVertex.ContainsKey(key);
        }

        public virtual bool Contains(IEdgeKey key)
        {
            return Edges.ContainsKey(key);
        }

        /// <summary>
        /// Returns true if an edge exists between the two points
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public virtual bool ContainsEdge(PointIndex A, PointIndex B)
        {
            if (!this.Contains(A) || !this.Contains(B))
                return false;

            EdgeKey key = new EdgeKey(this[A].Index, this[B].Index);
            return this.Contains(key);
        }

        public MorphMeshVertex GetVertex(int key)
        {
            return (MorphMeshVertex)Verticies[key];
        }

        public int AddVertex(MorphMeshVertex v)
        {
            int iVert = base.AddVertex(v);
            if(v.PolyIndex.HasValue)
                PolyIndexToVertex.Add(v.PolyIndex.Value, iVert);
            return iVert; 
        }

        public int AddVerticies(ICollection<MorphMeshVertex> verts)
        {
            int iStartVert = base.AddVerticies(verts.Select(v => (IVertex)v).ToArray());

            foreach (MorphMeshVertex v in verts)
            {
                if(v.PolyIndex.HasValue)
                    PolyIndexToVertex.Add(v.PolyIndex.Value, v.Index);
            }

            return iStartVert;
        }

        public MorphMeshEdge GetEdge(IEdgeKey key)
        {
            return (MorphMeshEdge)Edges[key];
        }
        
        public IEnumerable<MorphMeshEdge> MorphEdges
        {
            get
            {
                foreach (IEdge edge in this.Edges.Values)
                {
                    yield return (MorphMeshEdge)edge;
                }
            }
        }

        public override int Append(DynamicRenderMesh other)
        {
            int iStartVertex = base.Append(other);
            for(int i = iStartVertex; i < this.Verticies.Count; i++)
            {
                MorphMeshVertex v = this[i];
                if(v.PolyIndex.HasValue)
                    PolyIndexToVertex.Add(v.PolyIndex.Value, i); 
            }

            return iStartVertex;
        }
         
        /// <summary>
        /// Assign a type to each edge based on the rules specified in EdgeTypeExtensions
        /// </summary>
        public void ClassifyMeshEdges()
        { 
            GridPolygon[] Polygons = this.Polygons;

            foreach (MorphMeshEdge edge in this.MorphEdges)
            {
                if (edge.Type != EdgeType.UNKNOWN)
                    continue;

                MorphMeshVertex A = this.GetVertex(edge.A);
                MorphMeshVertex B = this.GetVertex(edge.B);

                if (A.Position.XY() == B.Position.XY())
                {
                    edge.Type = EdgeType.CORRESPONDING;
                    continue;
                }

                GridLineSegment L = this.ToSegment(edge.Key);
                edge.Type = EdgeTypeExtensions.GetEdgeTypeWithOrientation(A.PolyIndex.Value, B.PolyIndex.Value, Polygons, L.PointAlongLine(0.5));
            }

            return;
        }

        public void IdentifyRegions()
        {
            this.Regions = IdentifyRegions(this); 
        }

        /// <summary>
        /// A helper function that ensures all faces have the same Z level
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="face"></param>
        /// <param name="criteria"></param>
        /// <param name="ExpectedZ"></param>
        /// <returns></returns>
        private static bool IsInRegion(MorphRenderMesh mesh, MorphMeshFace face, Func<MorphRenderMesh, MorphMeshFace, bool> criteria, double ExpectedZ)
        {
            double? FaceZ;
            if (face.AllVertsAtSameZ(mesh, out FaceZ))
            {
                if (FaceZ != ExpectedZ)
                    return false;
            }
            else
            {
                return false; 
            }

            return criteria(mesh, face);
        }

        /// <summary>
        /// Identify all adjacent faces which have an invalid edge
        /// </summary>
        public static List<MorphMeshRegion> IdentifyRegions(MorphRenderMesh mesh)
        {
            List<MorphMeshRegion> listRegions = new List<MorphMeshRegion>();
            SortedSet<IFace> CheckedFaces = new SortedSet<IFace>();

            foreach(IFace f in mesh.Faces)
            {
                if(CheckedFaces.Contains(f))
                {
                    continue; 
                }

                MorphMeshFace face = (MorphMeshFace)f;

                MorphMeshVertex[] faceVerts = face.iVerts.Select(i => (MorphMeshVertex)mesh.Verticies[i]).ToArray();
                double? FaceZ;
                if(!face.AllVertsAtSameZ(mesh, out FaceZ))
                {
                    continue; 
                }
                
                if(face.IsInExposedRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh, mesh.FloodFillRegion(face, (m, foundFace) => IsInRegion(m, foundFace, MorphMeshFace.IsInExposedRegion, FaceZ.Value), ref CheckedFaces), RegionType.EXPOSED);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInHoleRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh, mesh.FloodFillRegion(face, (m, foundFace) => IsInRegion(m, foundFace, MorphMeshFace.IsInHoleRegion, FaceZ.Value), ref CheckedFaces), RegionType.HOLE);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    
                    continue;
                }

                if (face.IsInInvaginatedRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh, mesh.FloodFillRegion(face, (m, foundFace) => IsInRegion(m, foundFace, MorphMeshFace.IsInInvaginatedRegion, FaceZ.Value), ref CheckedFaces), RegionType.INVAGINATION);

                    //Whether or not the region is valid we mark it as checked so we don't repeat the floodfill for every face in the region.
                    CheckedFaces.UnionWith(region.Faces);
                    

                    //Invaginated regions can sometimes be bridges between two seperate ares of the same cell.  Test if the region is valid by examing the entire region for two open exits.
                    if (MorphMeshRegion.IsValidInvagination(region))
                    {
                        listRegions.Add(region); 
                        continue;
                    }
                }


                CheckedFaces.Add(face);
            }

            return listRegions; 
        }

        /// <summary>
        /// Build an RTree using SliceChords in the mesh.  
        /// Note that slice-chords cross Z levels so CONTOUR and ARTIFICIAL edges are not included
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public RTree.RTree<SliceChord> CreateChordTree()
        {
            RTree.RTree<SliceChord> rTree = new RTree.RTree<SliceChord>();
            ///Create a list of all slice chords.  Contours are valid but are not slice chords since they don't cross sections
            foreach (MorphMeshEdge e in this.Edges.Values.Where(e => (((MorphMeshEdge)e).Type != EdgeType.CONTOUR) && (((MorphMeshEdge)e).Type != EdgeType.ARTIFICIAL)))
            {
                SliceChord chord = new SliceChord(this[e.A].PolyIndex.Value, this[e.B].PolyIndex.Value, this.Polygons);
                rTree.Add(chord.Line.BoundingBox.ToRTreeRect(0), chord);
            }

            return rTree;
        }

        /// <summary>
        /// Returns the region, a set of faces, which are connected to the passed face and meet the criteria function
        /// </summary>
        /// <param name="f"></param>
        /// <param name="MeetsCriteriaFunc"></param>
        /// <param name="CheckedFaces"></param>
        /// <returns></returns>
        public SortedSet<MorphMeshFace> FloodFillRegion(MorphMeshFace f, Func<MorphRenderMesh, MorphMeshFace, bool> MeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces)
        {
            SortedSet<IFace> checkedRegionFaces = new SortedSet<IFace>(CheckedFaces); 
            
            return FloodFillRegionRecurse(f, MeetsCriteriaFunc, ref checkedRegionFaces);
        }

        private SortedSet<MorphMeshFace> FloodFillRegionRecurse(MorphMeshFace f, Func<MorphRenderMesh, MorphMeshFace, bool> MeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces)
        {
            SortedSet<MorphMeshFace> region = new SortedSet<MorphMeshFace>();
            region.Add(f);
            CheckedFaces.Add(f); 

            foreach (MorphMeshFace adjacent in f.AdjacentFaces(this))
            {
                if (CheckedFaces.Contains(adjacent))
                    continue;

                if (!MeetsCriteriaFunc(this, adjacent))
                {
                    CheckedFaces.Add(adjacent);
                    continue;
                }

                region.UnionWith(FloodFillRegion(adjacent, MeetsCriteriaFunc, ref CheckedFaces));
            }

            return region; 
        }

    }
}

using System;
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
        public PointIndex PolyIndex;

        public MorphMeshVertex(PointIndex polyIndex, GridVector3 p) : base(p)
        {
            PolyIndex = polyIndex;
        }

        public MorphMeshVertex(PointIndex polyIndex, GridVector3 p, GridVector3 n) : base(p, n)
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

    }


    public class MorphMeshEdge : Edge
    {
        public EdgeType Type;

        public bool MatchingOrientation = false; //True if this edge outside of one shape and inside another

        public MorphMeshEdge(EdgeType type, int A, int B) : base(A, B)
        {
            Type = type;
        }

        public static new IEdge Duplicate(IEdge old, int A, int B)
        {
            MorphMeshEdge edge = old as MorphMeshEdge;
            if (edge != null)
                return new MorphMeshEdge(edge.Type, A, B);

            return new MorphMeshEdge(EdgeType.UNKNOWN, A, B);
        }
    }

    public class MorphMeshFace : Face
    {

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

        public bool IsInExposedRegion(MorphRenderMesh mesh)
        {
            return IsInExposedRegion(mesh, this);
        }

        public static bool IsInExposedRegion(MorphRenderMesh mesh, IFace face)
        {
            var edges = face.Edges.Select(e => (MorphMeshEdge)mesh.Edges[e]);
            var EdgeTypes = edges.Select(e => e.Type).ToArray();
            int countInternal = EdgeTypes.Count(e => e == EdgeType.FLAT);
            if (countInternal == 0)
                return false;

            int countValid = EdgeTypes.Count(e => (e & EdgeType.VALID) > 0);
            if (countValid > 1)
                return false;

            //if (EdgeTypes.Count(e => e == EdgeType.CONTOUR) == 1 && EdgeTypes.Count(e => e == EdgeType.FLYING) == 2)
            //    return false; 

            //return EdgeTypes.Any(e => e == EdgeType.INTERNAL) && (EdgeTypes.Count(e => (e & EdgeType.VALID) > 0) == 2);
            return countInternal + countValid == 3;
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

            int countValid = EdgeTypes.Count(e => (e & EdgeType.VALID) > 0);
            if (countValid > 1)
                return false;

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
    public class MorphMeshRegion
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
                 

                PointIndex[] polyIndicies = Verticies.Select(v => ((MorphMeshVertex)ParentMesh.Verticies[v]).PolyIndex).ToArray();


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
        
        /// <summary>
        /// Return region verticies in no particular order
        /// </summary>
        public int[] Verticies
        {
            get
            {
                return Faces.SelectMany(f => f.iVerts).Distinct().ToArray();
            }
        }

        public GridVector3[] VertPositions
        {
            get
            {
                return Verticies.Select(v => ParentMesh.Verticies[v].Position).ToArray();
            }
        }



        public double NearestDistance(MorphMeshRegion other)
        {
            return this.Polygon.Distance(other.Polygon);
        }
    }


    public class MorphRenderMesh : DynamicRenderMesh
    {
        public GridPolygon[] Polygons { get; private set; }

        private List<MorphMeshRegion> _Regions = new List<MorphMeshRegion>();

        public List<MorphMeshRegion> Regions { get; private set; }


        public MorphRenderMesh(GridPolygon[] polygons)
        {
            Polygons = polygons;
            this.DuplicateVertex = MorphMeshVertex.Duplicate;
            this.DuplicateEdge = MorphMeshEdge.Duplicate;
            this.DuplicateFace = MorphMeshFace.Duplicate;
        }

        public new MorphMeshVertex this[int key]
        {
            get { return (MorphMeshVertex)this.Verticies[key]; }
        }

        public MorphMeshVertex GetVertex(int key)
        {
            return (MorphMeshVertex)Verticies[key];
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

        public void IdentifyRegions()
        {
            this.Regions = IdentifyRegions(this); 
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

                if(face.IsInExposedRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh, mesh.FloodFillRegion(face, MorphMeshFace.IsInExposedRegion, ref CheckedFaces), RegionType.EXPOSED);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInHoleRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh, mesh.FloodFillRegion(face, MorphMeshFace.IsInHoleRegion, ref CheckedFaces), RegionType.HOLE);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInInvaginatedRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh, mesh.FloodFillRegion(face, MorphMeshFace.IsInInvaginatedRegion, ref CheckedFaces), RegionType.INVAGINATION);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    continue;
                }


                CheckedFaces.Add(face);
            }

            return listRegions; 
        }

        /// <summary>
        /// Returns the region, a set of faces, which are connected to the passed face and meet the criteria function
        /// </summary>
        /// <param name="f"></param>
        /// <param name="MeetsCriteriaFunc"></param>
        /// <param name="CheckedFaces"></param>
        /// <returns></returns>
        private SortedSet<MorphMeshFace> FloodFillRegion(MorphMeshFace f, Func<MorphRenderMesh, MorphMeshFace, bool> MeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces)
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

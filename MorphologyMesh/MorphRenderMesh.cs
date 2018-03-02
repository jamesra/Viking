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
    public enum RegionType
    {
        EXPOSED,
        HOLE,
        INVAGINATION
    }

    public enum EdgeType
    {
        UNKNOWN = 0x00,
        INVALID = 0x10, //An edge that cannot be part of the final surface
        VALID = 0x01,
        
        CONTOUR = 0x11, //An edge along the contour, part of either the exterior or inner ring
        SURFACE = 0x21, //An edge that crosses from one Z-LEVEL to another and is part of the surface
        FLAT = 0x20, //An edge that connects two verticies on the same shape
        FLYING = 0x40, //An edge that crosses empty space, not a valid surface edge                
        INTERNAL = 0x80, //An edge that runs between two sections but is known to be inside the mesh
        INVAGINATION = 0x100, //An edge that spans between the same shape outside of that shape, but passes over a shape on an adjacent section
        HOLE = 0x200, //An edge that spans a hole in a shape
        CORRESPONDING = 0x41,  //An edge that shares XY coordinates with a vertex on a shape on an adjacent section
        FLIPPED_DIRECTION = 0x400 //An edge that would be valid, but the orientation is wrong.  For example, the line has solid material to the left on one vertex and the right on another
    }

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
        private SortedSet<MorphMeshFace> _Faces;
        public SortedSet<MorphMeshFace> Faces
        {
            get { return _Faces; }
        }

        public RegionType Type { get; private set; }

        public MorphMeshRegion(IEnumerable<MorphMeshFace> faces, RegionType type)
        {
            _Faces = new SortedSet<MorphMeshFace>(faces);
            Type = type;
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
                    MorphMeshRegion region = new MorphMeshRegion(mesh.FloodFillRegion(face, MorphMeshFace.IsInExposedRegion, ref CheckedFaces), RegionType.EXPOSED);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInHoleRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh.FloodFillRegion(face, MorphMeshFace.IsInHoleRegion, ref CheckedFaces), RegionType.HOLE);
                    listRegions.Add(region);
                    CheckedFaces.UnionWith(region.Faces);
                    continue;
                }

                if (face.IsInInvaginatedRegion(mesh))
                {
                    MorphMeshRegion region = new MorphMeshRegion(mesh.FloodFillRegion(face, MorphMeshFace.IsInInvaginatedRegion, ref CheckedFaces), RegionType.INVAGINATION);
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

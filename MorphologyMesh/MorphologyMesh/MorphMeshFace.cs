using Geometry.Meshing;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMesh
{

    public class MorphMeshFace : Face
    {
        /// <summary>
        /// Records if the face is part of a specific region type
        /// </summary>
        public RegionType Type { get; private set; }

        public bool NormalIsKnownCorrect = false;

        public MorphMeshFace(int A, int B, int C) : base(A, B, C)
        {
        }

        public MorphMeshFace(IEnumerable<int> verts) : base(verts)
        {
        }

        public static new IFace Create(IEnumerable<int> vertex_indicies)
        {
            return new MorphMeshFace(vertex_indicies);
        }


        /// <summary>
        /// Returns all faces adjacent to this face
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
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
        /// Return all faces sharing an edge with this face who meet the criteria function
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="EdgeCriteriaFunction"></param>
        /// <returns></returns>
        public IEnumerable<MorphMeshFace> AdjacentFaces(MorphRenderMesh mesh, EdgeMeetsCriteriaFunc MeetsEdgeCriteriaFunction)
        { /*
            IEdge[] edges = this.Edges.Select(e => mesh.Edges[e]).ToArray();
            IFace[] Faces = edges.SelectMany(e => mesh.Edges[e].Faces).ToArray();
            IFace[] Adjacent = Faces.Where(f => f != (IFace)this).ToArray();
            return Adjacent.Select(f => (MorphMeshFace)f).ToArray();
            */

            if (MeetsEdgeCriteriaFunction == null)
                return this.AdjacentFaces(mesh);
            else
                return this.Edges.SelectMany(e => mesh.Edges[e].Faces.Where(f => f != (IFace)this && MeetsEdgeCriteriaFunction(mesh, this, (MorphMeshFace)f, (MorphMeshEdge)mesh.Edges[e]))).Select(f => (MorphMeshFace)f);
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



            int countValid = EdgeTypes.Count(e => e.IsValid());
            if (countValid > 1)
                return false;

            return countInternal + countValid + countDirection == 3;
        }

        public bool IsInUntiledRegion(MorphRenderMesh mesh)
        {
            return IsInUntiledRegion(mesh, this);
        }

        /// <summary>
        /// Return true if the origin and adjacent face are not sharing a Contour edge. 
        /// This check is essential when determining the correct boundaries of regions.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="origin"></param>
        /// <param name="adjacent"></param>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static bool AdjacentFaceDoesNotCrossContour(MorphRenderMesh mesh, MorphMeshFace origin, IFace adjacent, MorphMeshEdge edge)
        {
            if (edge.Type == EdgeType.CONTOUR)
                return false;

            return true;
        }

        public static bool IsInUntiledRegion(MorphRenderMesh mesh, IFace face)
        {
            var edges = face.Edges.Select(e => (MorphMeshEdge)mesh.Edges[e]);
            var EdgeTypes = edges.Select(e => e.Type).ToArray();
            int countUntiled = EdgeTypes.Count(e => e == EdgeType.UNTILED);
            if (countUntiled == 0)
                return false;

            int countValid = EdgeTypes.Count(e => e.IsValid());
            if (countValid == 0)
                return false;

            return true;//countUntiled == 3;
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
                countValid = EdgeTypes.Count(e => e.IsValid());
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

            int countValid = EdgeTypes.Count(e => e.IsValid());
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
                //case EdgeType.VALID:
                case EdgeType.SURFACE:
                case EdgeType.MEDIALAXIS:
                case EdgeType.CONTOUR_TO_MEDIALAXIS:
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


}

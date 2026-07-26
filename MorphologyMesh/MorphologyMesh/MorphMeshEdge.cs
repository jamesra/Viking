using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MorphologyMesh
{


    public class MorphMeshEdge(EdgeType type, int A, int B) : Edge(A, B), IEquatable<MorphMeshEdge>
    {
        public EdgeType Type = type;

        public bool MatchingOrientation = false; //True if this edge is outside of one shape and inside another

        public override void AddFace(IFace f) => base.AddFace(f);

        public new static IEdge Create(int A, int B) => new MorphMeshEdge(EdgeType.UNKNOWN, A, B);

        public new ImmutableSortedSet<MorphMeshFace> Faces => new SortedSet<MorphMeshFace>(this._Faces.Select(f => (MorphMeshFace)f)).ToImmutableSortedSet();

        /// <summary>
        /// The number of faces this edge carries in a correct Bajaj mesh.  CONTOUR edges are the seam shared with
        /// the neighboring slice, so within a single slice mesh they carry one face.  Every other edge is interior
        /// to the slice and carries two.
        /// </summary>
        public int RequiredFaceCount => Type == EdgeType.CONTOUR ? 1 : 2;

        /// <summary>
        /// Returns false if the edge requires additional faces to complete the meshing of the morphology.
        /// </summary>
        public bool FacesComplete => Faces.Count >= RequiredFaceCount;

        /// <summary>
        /// True when the edge carries more faces than a manifold surface allows.  This used to be reported as
        /// "complete", which hid non-manifold edges from region detection instead of surfacing them.  The count of
        /// such edges is reported per mesh by <see cref="MeshManifoldValidator"/>.
        /// </summary>
        public bool HasTooManyFaces => Faces.Count > RequiredFaceCount;

        public static IEdge Duplicate(IEdge old, int A, int B)
        {
            MorphMeshEdge edge = old as MorphMeshEdge;
            if (edge != null)
                return new MorphMeshEdge(edge.Type, A, B);

            return new MorphMeshEdge(EdgeType.UNKNOWN, A, B);
        }

        public bool Equals(MorphMeshEdge other) => base.Equals(other);

        public override string ToString() => base.ToString() + " " + this.Type.ToString();
    }

}

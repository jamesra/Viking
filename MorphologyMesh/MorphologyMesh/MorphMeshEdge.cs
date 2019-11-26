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


    public class MorphMeshEdge : Edge, IEquatable<MorphMeshEdge>
    {
        public EdgeType Type;

        public bool MatchingOrientation = false; //True if this edge is outside of one shape and inside another

        public MorphMeshEdge(EdgeType type, int A, int B) : base(A, B)
        {
            Type = type;
        }

        public static new IEdge Create(int A, int B)
        {
            return new MorphMeshEdge(EdgeType.UNKNOWN, A, B);
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

        public override string ToString()
        {
            return base.ToString() + " " + this.Type.ToString();
        }
    }

}

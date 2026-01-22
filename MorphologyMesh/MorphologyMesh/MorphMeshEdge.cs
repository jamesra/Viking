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

        public override void AddFace(IFace f) => base.AddFace(f);//Debug.Assert(this.Faces.Count < 3, string.Format("{0} was extra face on {1}", f, this));

        public new static IEdge Create(int A, int B) => new MorphMeshEdge(EdgeType.UNKNOWN, A, B);

        public new ImmutableSortedSet<MorphMeshFace> Faces => new SortedSet<MorphMeshFace>(this._Faces.Select(f => (MorphMeshFace)f)).ToImmutableSortedSet();

        /// <summary>
        /// Returns false if the edge requires additional faces to complete the meshing of the morphology.
        /// Currently used for Bajaj meshing, where CONTOUR edges require one face, and all others require two.
        /// </summary>
        /// <returns></returns>
        public bool FacesComplete
        {
            get
            {
                //System.Diagnostics.Debug.Assert(this.Faces.Count < 3); // We cannot have more than two faces on an edge when meshing morphology
                if (this.Faces.Count > 2)
                    return true;    //I don't know how we could have three, but that's enough faces for this edge

                return Type == EdgeType.CONTOUR ? Faces.Count == 1 : Faces.Count == 2;
            }
        }

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

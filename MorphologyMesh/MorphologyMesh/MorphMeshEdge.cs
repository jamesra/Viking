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

        public override void AddFace(IFace f)
        {
            base.AddFace(f);
            //Debug.Assert(this.Faces.Count < 3, string.Format("{0} was extra face on {1}", f, this));
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

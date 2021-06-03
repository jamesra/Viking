using Geometry;
using System;

namespace MorphologyMesh
{
    public interface ISliceChord : IEquatable<ISliceChord>
    {
        GridLineSegment Line { get; }

    }

    /// <summary>
    /// Describes a slice chord via verticies on the mesh.  Can include medial axis verticies unlike the original SliceChord class
    /// </summary>
    public class MeshChord : IEquatable<MeshChord>, ISliceChord
    { 
        public readonly GridLineSegment Line;

        public readonly int iOrigin; //The index of the vertex in the mesh at the origin of the chord
        public readonly int iTarget; //The index of the vertex in the mesh at the target of the chord

        private readonly MorphRenderMesh mesh;

        GridLineSegment ISliceChord.Line { get { return this.Line; } }

        public MeshChord(MorphRenderMesh mesh, int iO, int iT)
        {
            this.mesh = mesh;
            this.iOrigin = iO;
            this.iTarget = iT;
            this.Line = new GridLineSegment(mesh[iO].Position.XY(), mesh[iT].Position.XY());
        }
         
        public bool Equals(MeshChord other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.iOrigin == other.iOrigin && this.iTarget == other.iTarget;
        }

        public bool Equals(ISliceChord other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.Line.Equals(other.Line);
        }
    }

    /// <summary>
    /// A slice chord is a line that runs directly from a vertex on one contour to a vertex on the same or another contour.
    /// </summary>
    public class SliceChord : IEquatable<SliceChord>, ISliceChord
    {
        public readonly GridLineSegment Line;

        public readonly PointIndex Origin; //The vertex originating the slice chord
        public readonly PointIndex Target; //The target vertex

        //public SliceChordTestType PassedTests; //Tests we know this chord has passed.
        //public SliceChordTestType FailedTests; //Tests we know this chord has failed.
         
        public SliceChord(PointIndex O, PointIndex T, GridPolygon[] polygons)
        {
            this.Line = new GridLineSegment(O.Point(polygons), T.Point(polygons));
            this.Origin = O;
            this.Target = T;
            this.Orientation = EdgeTypeExtensions.Orientation(Origin, Target, polygons);
        }

        public bool Equals(SliceChord other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.Origin == this.Origin &&
               other.Target == this.Target)
                return true;

            return false;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null))
                return false;

            SliceChord other = obj as SliceChord;

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return Origin.GetHashCode() + Target.GetHashCode();
        }

        public double Orientation
        {
            get; private set;
        }

        GridLineSegment ISliceChord.Line { get { return this.Line; } }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Origin, Target);
        }

        public bool Equals(ISliceChord other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.Line.Equals(other.Line);
        }

        bool IEquatable<ISliceChord>.Equals(ISliceChord other)
        {
            SliceChord cast_other = other as SliceChord;
            if(cast_other != null)
            {
                return this.Equals(cast_other);
            }

            return this.Line.Equals(other.Line);
        }
    }
}
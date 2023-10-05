using Geometry;
using System;

namespace MorphologyMesh
{
    public interface ISliceChord : IEquatable<ISliceChord>
    {
        /// <summary>
        /// Geometric line segment of the chord
        /// </summary>
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
            if (other is null)
                return false;

            return this.iOrigin == other.iOrigin && this.iTarget == other.iTarget;
        }

        public bool Equals(ISliceChord other)
        {
            if (other is null)
                return false;

            return this.Line.Equals(other.Line);
        }
    }

    /// <summary>
    /// A slice chord is a line that runs directly from a vertex on one contour to a vertex on the same or another contour.
    /// </summary>
    public class SliceChord : IEquatable<SliceChord>, ISliceChord
    {
        /// <summary>
        /// Geometric line segment of the chord
        /// </summary>
        public readonly GridLineSegment Line;

        public readonly PolygonIndex Origin; //The vertex originating the slice chord
        public readonly PolygonIndex Target; //The target vertex

        public double Orientation
        {
            get; private set;
        }

        GridLineSegment ISliceChord.Line => this.Line;


        //public SliceChordTestType PassedTests; //Tests we know this chord has passed.
        //public SliceChordTestType FailedTests; //Tests we know this chord has failed.

        public SliceChord(PolygonIndex O, PolygonIndex T, GridPolygon[] polygons)
        {
            this.Line = new GridLineSegment(O.Point(polygons), T.Point(polygons));
            this.Origin = O;
            this.Target = T;
            this.Orientation = Origin.Orientation(Target, polygons);
        }

        public bool Equals(SliceChord other)
        {
            if (other is null)
                return false;

            if (other.Origin == this.Origin &&
               other.Target == this.Target)
                return true;

            return false;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;

            SliceChord other = obj as SliceChord;

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return Origin.GetHashCode() + Target.GetHashCode();
        }

        
        public override string ToString() => $"{Origin} - {Target}";

        public bool Equals(ISliceChord other)
        {
            if (other is null)
                return false;

            return this.Line.Equals(other.Line);
        }

        bool IEquatable<ISliceChord>.Equals(ISliceChord other)
        {
            if (other is SliceChord cast_other)
            {
                return this.Equals(cast_other);
            }

            return this.Line.Equals(other.Line);
        }
    }
}
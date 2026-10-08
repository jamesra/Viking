using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Geometry
{
    /// <summary>
    /// Maps points from one triangle to another using barycentric coordinates.
    /// Holds indices into a shared <see cref="MappingVector2"/> array so grid lookups can build one on the stack.
    /// </summary>
    public readonly struct MappingTriangle(MappingVector2[] nodes, int n1, int n2, int n3) : ICloneable, IEquatable<MappingTriangle>, ITransform
    {
        internal readonly MappingVector2[] Nodes = nodes;

        internal readonly int N1 = n1;
        internal readonly int N2 = n2;
        internal readonly int N3 = n3;

        public override bool Equals(object obj) => obj is MappingTriangle other && Equals(other);

        public override int GetHashCode() => GeometryHashCode.Combine(
            GeometryHashCode.Combine(Nodes is null ? 0 : RuntimeHelpers.GetHashCode(Nodes), N1),
            GeometryHashCode.Combine(N2, N3));

        public double MinMapX => Math.Min(Math.Min(Nodes[N1].MappedPoint.X, Nodes[N2].MappedPoint.X), Nodes[N3].MappedPoint.X);

        public double MaxMapX => Math.Max(Math.Max(Nodes[N1].MappedPoint.X, Nodes[N2].MappedPoint.X), Nodes[N3].MappedPoint.X);

        public double MinMapY => Math.Min(Math.Min(Nodes[N1].MappedPoint.Y, Nodes[N2].MappedPoint.Y), Nodes[N3].MappedPoint.Y);

        public double MaxMapY => Math.Max(Math.Max(Nodes[N1].MappedPoint.Y, Nodes[N2].MappedPoint.Y), Nodes[N3].MappedPoint.Y);

        public Rectangle MappedBoundingBox => new(MinMapX, MaxMapX, MinMapY, MaxMapY);

        public double MinCtrlX => Math.Min(Math.Min(Nodes[N1].ControlPoint.X, Nodes[N2].ControlPoint.X), Nodes[N3].ControlPoint.X);

        public double MaxCtrlX => Math.Max(Math.Max(Nodes[N1].ControlPoint.X, Nodes[N2].ControlPoint.X), Nodes[N3].ControlPoint.X);

        public double MinCtrlY => Math.Min(Math.Min(Nodes[N1].ControlPoint.Y, Nodes[N2].ControlPoint.Y), Nodes[N3].ControlPoint.Y);

        public double MaxCtrlY => Math.Max(Math.Max(Nodes[N1].ControlPoint.Y, Nodes[N2].ControlPoint.Y), Nodes[N3].ControlPoint.Y);

        public Rectangle ControlBoundingBox => new(MinCtrlX, MaxCtrlX, MinCtrlY, MaxCtrlY);

        /// <summary>Allocates a <see cref="Triangle"/>. The mapping methods below use the vertices directly instead.</summary>
        public Triangle Control => new(Nodes[N1].ControlPoint, Nodes[N2].ControlPoint, Nodes[N3].ControlPoint);

        /// <summary>Allocates a <see cref="Triangle"/>. The mapping methods below use the vertices directly instead.</summary>
        public Triangle Mapped => new(Nodes[N1].MappedPoint, Nodes[N2].MappedPoint, Nodes[N3].MappedPoint);

        private Vector2 M1 => Nodes[N1].MappedPoint;
        private Vector2 M2 => Nodes[N2].MappedPoint;
        private Vector2 M3 => Nodes[N3].MappedPoint;
        private Vector2 C1 => Nodes[N1].ControlPoint;
        private Vector2 C2 => Nodes[N2].ControlPoint;
        private Vector2 C3 => Nodes[N3].ControlPoint;

        public MappingTriangle Copy() => this;

        object ICloneable.Clone() => this;

        //The mapping methods below give the same results, and throw the same ArgumentException for a degenerate triangle, as
        //building the Mapped and Control triangles did, without allocating. They run once or more per mapped point.

        public bool CanTransform(in Vector2 Point) => Triangle.Covers(M1, M2, M3, Point);

        public bool CanInverseTransform(in Vector2 Point) => Triangle.Covers(C1, C2, C3, Point);

        private static bool BarycentricCoordIsMappable(in Vector2 uv) =>
            uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0);

        public Vector2 Transform(in Vector2 Point)
        {
            Triangle.ThrowIfDegenerate(M1, M2, M3);
            Vector2 uv = Triangle.Barycentric(M1, M2, M3, Point);
            Debug.Assert(BarycentricCoordIsMappable(uv));

            Triangle.ThrowIfDegenerate(C1, C2, C3);
            Vector2 translated = Vector2.FromBarycentric(C1, C2, C3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public Vector2 InverseTransform(in Vector2 Point)
        {
            Triangle.ThrowIfDegenerate(C1, C2, C3);
            Vector2 uv = Triangle.Barycentric(C1, C2, C3, Point);

            Triangle.ThrowIfDegenerate(M1, M2, M3);
            Vector2 translated = Vector2.FromBarycentric(M1, M2, M3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public Vector2[] Transform(in Vector2[] Points)
        {
            Vector2 m1 = M1, m2 = M2, m3 = M3, c1 = C1, c2 = C2, c3 = C3;
            Triangle.ThrowIfDegenerate(m1, m2, m3);
            Triangle.ThrowIfDegenerate(c1, c2, c3);

            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 uv = Triangle.Barycentric(m1, m2, m3, Points[i]);
                Debug.Assert(BarycentricCoordIsMappable(uv));
                output[i] = Vector2.FromBarycentric(c1, c2, c3, uv.Y, uv.X).Round(Global.TransformSignificantDigits);
            }

            return output;
        }

        public Vector2[] InverseTransform(in Vector2[] Points)
        {
            Vector2 m1 = M1, m2 = M2, m3 = M3, c1 = C1, c2 = C2, c3 = C3;
            Triangle.ThrowIfDegenerate(m1, m2, m3);
            Triangle.ThrowIfDegenerate(c1, c2, c3);

            Vector2[] output = new Vector2[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 uv = Triangle.Barycentric(c1, c2, c3, Points[i]);
                output[i] = Vector2.FromBarycentric(m1, m2, m3, uv.Y, uv.X).Round(Global.TransformSignificantDigits);
            }

            return output;
        }

        public bool Equals(MappingTriangle other) =>
            ReferenceEquals(Nodes, other.Nodes) && N1 == other.N1 && N2 == other.N2 && N3 == other.N3;

        public static bool operator ==(MappingTriangle left, MappingTriangle right) => left.Equals(right);

        public static bool operator !=(MappingTriangle left, MappingTriangle right) => !left.Equals(right);

        public bool TryTransform(in Vector2 Point, out Vector2 translated)
        {
            Triangle.ThrowIfDegenerate(M1, M2, M3);
            Vector2 uv = Triangle.Barycentric(M1, M2, M3, Point);
            if (false == BarycentricCoordIsMappable(uv))
            {
                translated = default;
                return false;
            }

            Triangle.ThrowIfDegenerate(C1, C2, C3);
            translated = Vector2.FromBarycentric(C1, C2, C3, uv.Y, uv.X);
            translated = translated.Round(Global.TransformSignificantDigits);
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] output)
        {
            Vector2 m1 = M1, m2 = M2, m3 = M3, c1 = C1, c2 = C2, c3 = C3;
            Triangle.ThrowIfDegenerate(m1, m2, m3);

            output = new Vector2[Points.Length];
            bool[] wasMapped = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 uv = Triangle.Barycentric(m1, m2, m3, Points[i]);
                if (!BarycentricCoordIsMappable(uv))
                    continue;

                Triangle.ThrowIfDegenerate(c1, c2, c3);
                wasMapped[i] = true;
                output[i] = Vector2.FromBarycentric(c1, c2, c3, uv.Y, uv.X).Round(Global.TransformSignificantDigits);
            }

            return wasMapped;
        }

        public bool TryInverseTransform(in Vector2 Point, out Vector2 translated)
        {
            Triangle.ThrowIfDegenerate(C1, C2, C3);
            Vector2 uv = Triangle.Barycentric(C1, C2, C3, Point);
            if (false == BarycentricCoordIsMappable(uv))
            {
                translated = default;
                return false;
            }

            Triangle.ThrowIfDegenerate(M1, M2, M3);
            translated = Vector2.FromBarycentric(M1, M2, M3, uv.Y, uv.X);
            translated = translated.Round(Global.TransformSignificantDigits);
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] output)
        {
            Vector2 m1 = M1, m2 = M2, m3 = M3, c1 = C1, c2 = C2, c3 = C3;
            Triangle.ThrowIfDegenerate(c1, c2, c3);

            output = new Vector2[Points.Length];
            bool[] wasMapped = new bool[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                Vector2 uv = Triangle.Barycentric(c1, c2, c3, Points[i]);
                if (!BarycentricCoordIsMappable(uv))
                    continue;

                Triangle.ThrowIfDegenerate(m1, m2, m3);
                wasMapped[i] = true;
                output[i] = Vector2.FromBarycentric(m1, m2, m3, uv.Y, uv.X).Round(Global.TransformSignificantDigits);
            }

            return wasMapped;
        }
    }
}

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

        public Triangle Control => new(Nodes[N1].ControlPoint, Nodes[N2].ControlPoint, Nodes[N3].ControlPoint);

        public Triangle Mapped => new(Nodes[N1].MappedPoint, Nodes[N2].MappedPoint, Nodes[N3].MappedPoint);

        public MappingTriangle Copy() => this;

        object ICloneable.Clone() => this;

        public bool CanTransform(in Vector2 Point) => Mapped.Covers(Point);

        public bool CanInverseTransform(in Vector2 Point) => Control.Covers(Point);

        private static bool BarycentricCoordIsMappable(in Vector2 uv) =>
            uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0);

        public Vector2 Transform(in Vector2 Point)
        {
            Vector2 uv = Mapped.Barycentric(Point);
            Debug.Assert(BarycentricCoordIsMappable(uv));

            Vector2 translated = Vector2.FromBarycentric(Control.P1, Control.P2, Control.P3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public Vector2 InverseTransform(in Vector2 Point)
        {
            Vector2 uv = Control.Barycentric(Point);

            Vector2 translated = Vector2.FromBarycentric(Mapped.P1, Mapped.P2, Mapped.P3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public Vector2[] Transform(in Vector2[] Points)
        {
            Triangle mapped = Mapped;
            Triangle control = Control;
            var uv_points = Points.Select(point => mapped.Barycentric(point));
            Debug.Assert(uv_points.All(uv => uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0)));

            return [.. uv_points.Select(uv => Vector2.FromBarycentric(control.P1, control.P2, control.P3, uv.Y, uv.X).Round(Global.TransformSignificantDigits))];
        }

        public Vector2[] InverseTransform(in Vector2[] Points)
        {
            Triangle mapped = Mapped;
            Triangle control = Control;
            var uv_points = Points.Select(point => control.Barycentric(point));

            return [.. uv_points.Select(uv => Vector2.FromBarycentric(mapped.P1, mapped.P2, mapped.P3, uv.Y, uv.X).Round(Global.TransformSignificantDigits))];
        }

        public bool Equals(MappingTriangle other) =>
            ReferenceEquals(Nodes, other.Nodes) && N1 == other.N1 && N2 == other.N2 && N3 == other.N3;

        public static bool operator ==(MappingTriangle left, MappingTriangle right) => left.Equals(right);

        public static bool operator !=(MappingTriangle left, MappingTriangle right) => !left.Equals(right);

        public bool TryTransform(in Vector2 Point, out Vector2 translated)
        {
            Vector2 uv = Mapped.Barycentric(Point);
            if (false == BarycentricCoordIsMappable(uv))
            {
                translated = default;
                return false;
            }

            translated = Vector2.FromBarycentric(Control.P1, Control.P2, Control.P3, uv.Y, uv.X);
            translated = translated.Round(Global.TransformSignificantDigits);
            return true;
        }

        public bool[] TryTransform(in Vector2[] Points, out Vector2[] output)
        {
            output = Mapped.Barycentric(Points);
            var wasMapped = output.Select(uv => BarycentricCoordIsMappable(uv)).ToArray();
            for (int i = 0; i < Points.Length; i++)
            {
                if (wasMapped[i] == false)
                {
                    output[i] = default;
                    continue;
                }

                output[i] = Vector2.FromBarycentric(Control.P1, Control.P2, Control.P3, output[i].Y, output[i].X);
                output[i] = output[i].Round(Global.TransformSignificantDigits);
            }

            return wasMapped;
        }

        public bool TryInverseTransform(in Vector2 Point, out Vector2 translated)
        {
            Vector2 uv = Control.Barycentric(Point);
            if (false == BarycentricCoordIsMappable(uv))
            {
                translated = default;
                return false;
            }

            translated = Vector2.FromBarycentric(Mapped.P1, Mapped.P2, Mapped.P3, uv.Y, uv.X);
            translated = translated.Round(Global.TransformSignificantDigits);
            return true;
        }

        public bool[] TryInverseTransform(in Vector2[] Points, out Vector2[] output)
        {
            output = Control.Barycentric(Points);
            var wasMapped = output.Select(uv => BarycentricCoordIsMappable(uv)).ToArray();
            for (int i = 0; i < Points.Length; i++)
            {
                if (wasMapped[i] == false)
                {
                    output[i] = default;
                    continue;
                }

                output[i] = Vector2.FromBarycentric(Mapped.P1, Mapped.P2, Mapped.P3, output[i].Y, output[i].X);
                output[i] = output[i].Round(Global.TransformSignificantDigits);
            }

            return wasMapped;
        }
    }
}

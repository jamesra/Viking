using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;


namespace Geometry
{
    /// <summary>
    /// Maps points from one triangle to another using barycentric coordinates.
    /// Holds indices into a shared <see cref="MappingGridVector2"/> array so grid lookups can build one on the stack.
    /// </summary>
    public readonly struct MappingGridTriangle(MappingGridVector2[] nodes, int n1, int n2, int n3) : ICloneable, IEquatable<MappingGridTriangle>, ITransform
    {
        internal readonly MappingGridVector2[] Nodes = nodes;

        internal readonly int N1 = n1;
        internal readonly int N2 = n2;
        internal readonly int N3 = n3;

        public override bool Equals(object obj) => obj is MappingGridTriangle other && Equals(other);

        public override int GetHashCode()
        {
            int nodesHash = Nodes is null ? 0 : RuntimeHelpers.GetHashCode(Nodes);
            unchecked
            {
                int hash = nodesHash;
                hash = (hash * 397) ^ N1;
                hash = (hash * 397) ^ N2;
                hash = (hash * 397) ^ N3;
                return hash;
            }
        }

        public double MinMapX => Math.Min(Math.Min(Nodes[N1].MappedPoint.X, Nodes[N2].MappedPoint.X), Nodes[N3].MappedPoint.X);

        public double MaxMapX => Math.Max(Math.Max(Nodes[N1].MappedPoint.X, Nodes[N2].MappedPoint.X), Nodes[N3].MappedPoint.X);

        public double MinMapY => Math.Min(Math.Min(Nodes[N1].MappedPoint.Y, Nodes[N2].MappedPoint.Y), Nodes[N3].MappedPoint.Y);

        public double MaxMapY => Math.Max(Math.Max(Nodes[N1].MappedPoint.Y, Nodes[N2].MappedPoint.Y), Nodes[N3].MappedPoint.Y);

        public GridRectangle MappedBoundingBox => new(MinMapX, MaxMapX, MinMapY, MaxMapY);

        public double MinCtrlX => Math.Min(Math.Min(Nodes[N1].ControlPoint.X, Nodes[N2].ControlPoint.X), Nodes[N3].ControlPoint.X);

        public double MaxCtrlX => Math.Max(Math.Max(Nodes[N1].ControlPoint.X, Nodes[N2].ControlPoint.X), Nodes[N3].ControlPoint.X);

        public double MinCtrlY => Math.Min(Math.Min(Nodes[N1].ControlPoint.Y, Nodes[N2].ControlPoint.Y), Nodes[N3].ControlPoint.Y);

        public double MaxCtrlY => Math.Max(Math.Max(Nodes[N1].ControlPoint.Y, Nodes[N2].ControlPoint.Y), Nodes[N3].ControlPoint.Y);

        public GridRectangle ControlBoundingBox => new(MinCtrlX, MaxCtrlX, MinCtrlY, MaxCtrlY);

        public GridTriangle Control => new(Nodes[N1].ControlPoint, Nodes[N2].ControlPoint, Nodes[N3].ControlPoint);

        public GridTriangle Mapped => new(Nodes[N1].MappedPoint, Nodes[N2].MappedPoint, Nodes[N3].MappedPoint);

        public MappingGridTriangle Copy() => this;

        object ICloneable.Clone() => this;

        public bool CanTransform(in GridVector2 Point) => Mapped.Contains(Point);

        public bool CanInverseTransform(in GridVector2 Point) => Control.Contains(Point);

        private static bool BarycentricCoordIsMappable(in GridVector2 uv) =>
            uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0);

        public GridVector2 Transform(in GridVector2 Point)
        {
            GridVector2 uv = Mapped.Barycentric(Point);
            Debug.Assert(BarycentricCoordIsMappable(uv));

            GridVector2 translated = GridVector2.FromBarycentric(Control.p1, Control.p2, Control.p3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public GridVector2 InverseTransform(in GridVector2 Point)
        {
            GridVector2 uv = Control.Barycentric(Point);

            GridVector2 translated = GridVector2.FromBarycentric(Mapped.p1, Mapped.p2, Mapped.p3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public GridVector2[] Transform(in GridVector2[] Points)
        {
            GridTriangle mapped = Mapped;
            GridTriangle control = Control;
            var uv_points = Points.Select(point => mapped.Barycentric(point));
            Debug.Assert(uv_points.All(uv => uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0)));

            return [.. uv_points.Select(uv => GridVector2.FromBarycentric(control.p1, control.p2, control.p3, uv.Y, uv.X).Round(Global.TransformSignificantDigits))];
        }

        public GridVector2[] InverseTransform(in GridVector2[] Points)
        {
            GridTriangle mapped = Mapped;
            GridTriangle control = Control;
            var uv_points = Points.Select(point => control.Barycentric(point));

            return [.. uv_points.Select(uv => GridVector2.FromBarycentric(mapped.p1, mapped.p2, mapped.p3, uv.Y, uv.X).Round(Global.TransformSignificantDigits))];
        }

        public bool Equals(MappingGridTriangle other) =>
            ReferenceEquals(Nodes, other.Nodes) && N1 == other.N1 && N2 == other.N2 && N3 == other.N3;

        public static bool operator ==(MappingGridTriangle left, MappingGridTriangle right) => left.Equals(right);

        public static bool operator !=(MappingGridTriangle left, MappingGridTriangle right) => !left.Equals(right);

        public bool TryTransform(in GridVector2 Point, out GridVector2 translated)
        {
            GridVector2 uv = Mapped.Barycentric(Point);
            if (false == BarycentricCoordIsMappable(uv))
            {
                translated = default;
                return false;
            }

            translated = GridVector2.FromBarycentric(Control.p1, Control.p2, Control.p3, uv.Y, uv.X);
            translated = translated.Round(Global.TransformSignificantDigits);
            return true;
        }

        public bool[] TryTransform(in GridVector2[] Points, out GridVector2[] output)
        {
            GridTriangle mapped = Mapped;
            GridTriangle control = Control;
            output = mapped.Barycentric(Points);
            var wasMapped = output.Select(uv => BarycentricCoordIsMappable(uv)).ToArray();
            for (int i = 0; i < Points.Length; i++)
            {
                if (wasMapped[i] == false)
                {
                    output[i] = default;
                    continue;
                }

                output[i] = GridVector2.FromBarycentric(control.p1, control.p2, control.p3, output[i].Y, output[i].X);
                output[i] = output[i].Round(Global.TransformSignificantDigits);
            }

            return wasMapped;
        }

        public bool TryInverseTransform(in GridVector2 Point, out GridVector2 translated)
        {
            GridVector2 uv = Control.Barycentric(Point);
            if (false == BarycentricCoordIsMappable(uv))
            {
                translated = default;
                return false;
            }

            translated = GridVector2.FromBarycentric(Mapped.p1, Mapped.p2, Mapped.p3, uv.Y, uv.X);
            translated = translated.Round(Global.TransformSignificantDigits);
            return true;
        }

        public bool[] TryInverseTransform(in GridVector2[] Points, out GridVector2[] output)
        {
            GridTriangle mapped = Mapped;
            GridTriangle control = Control;
            output = control.Barycentric(Points);
            var wasMapped = output.Select(uv => BarycentricCoordIsMappable(uv)).ToArray();
            for (int i = 0; i < Points.Length; i++)
            {
                if (wasMapped[i] == false)
                {
                    output[i] = default;
                    continue;
                }

                output[i] = GridVector2.FromBarycentric(mapped.p1, mapped.p2, mapped.p3, output[i].Y, output[i].X);
                output[i] = output[i].Round(Global.TransformSignificantDigits);
            }

            return wasMapped;
        }
    }
}

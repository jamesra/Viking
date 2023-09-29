using System;
using System.Diagnostics;
using System.Linq;


namespace Geometry
{
    /// <summary>
    /// Maps points from one triangle to another using barycentric coordinates
    /// </summary>
    public class MappingGridTriangle : ICloneable, IEquatable<MappingGridTriangle>, ITransform
    {
        internal readonly MappingGridVector2[] Nodes;

        internal readonly int N1; //Index of first node
        internal readonly int N2; //Index of second node 
        internal readonly int N3; //Index of third node

        public override bool Equals(object obj)
        {
            if (obj is MappingGridTriangle TriObj)
            {
                if (TriObj.N1 == this.N1 &&
                    TriObj.N2 == this.N2 &&
                    TriObj.N3 == this.N3)
                    return true;
            }

            //We should test all the other index combos too I suppose..
            return false;
        }

        public override int GetHashCode()
        {
            return N1 + N2 + N3;
        }

        #region MapBounds

        //I use these to quickly determine if a triangle could intersect a point
        private double _MinMapX = double.MaxValue;
        private double _MaxMapX = double.MinValue;

        public double MinMapX
        {
            get
            {
                if (_MinMapX == double.MaxValue)
                {
                    _MinMapX = Math.Min(Nodes[N1].MappedPoint.X, Nodes[N2].MappedPoint.X);
                    _MinMapX = Math.Min(_MinMapX, Nodes[N3].MappedPoint.X);
                }

                return _MinMapX;
            }
        }

        public double MaxMapX
        {
            get
            {
                if (_MaxMapX == double.MinValue)
                {
                    _MaxMapX = Math.Max(Nodes[N1].MappedPoint.X, Nodes[N2].MappedPoint.X);
                    _MaxMapX = Math.Max(_MaxMapX, Nodes[N3].MappedPoint.X);
                }

                return _MaxMapX;
            }
        }

        //I use these to quickly determine if a triangle could intersect a point
        private double _MinMapY = double.MaxValue;
        private double _MaxMapY = double.MinValue;

        public double MinMapY
        {
            get
            {
                if (_MinMapY == double.MaxValue)
                {
                    _MinMapY = Math.Min(Nodes[N1].MappedPoint.Y, Nodes[N2].MappedPoint.Y);
                    _MinMapY = Math.Min(_MinMapY, Nodes[N3].MappedPoint.Y);
                }

                return _MinMapY;
            }
        }

        public double MaxMapY
        {
            get
            {
                if (_MaxMapY == double.MinValue)
                {
                    _MaxMapY = Math.Max(Nodes[N1].MappedPoint.Y, Nodes[N2].MappedPoint.Y);
                    _MaxMapY = Math.Max(_MaxMapY, Nodes[N3].MappedPoint.Y);
                }

                return _MaxMapY;
            }
        }

        public GridRectangle MappedBoundingBox => new GridRectangle(MinMapX, MaxMapX, MinMapY, MaxMapY);

        #endregion

        #region CtrlBounds

        //I use these to quickly determine if a triangle could intersect a point
        private double _MinCtrlX = double.MaxValue;
        private double _MaxCtrlX = double.MinValue;

        public double MinCtrlX
        {
            get
            {
                if (_MinCtrlX == double.MaxValue)
                {
                    _MinCtrlX = Math.Min(Nodes[N1].ControlPoint.X, Nodes[N2].ControlPoint.X);
                    _MinCtrlX = Math.Min(_MinCtrlX, Nodes[N3].ControlPoint.X);
                }

                return _MinCtrlX;
            }
        }

        public double MaxCtrlX
        {
            get
            {
                if (_MaxCtrlX == double.MinValue)
                {
                    _MaxCtrlX = Math.Max(Nodes[N1].ControlPoint.X, Nodes[N2].ControlPoint.X);
                    _MaxCtrlX = Math.Max(_MaxCtrlX, Nodes[N3].ControlPoint.X);
                }

                return _MaxCtrlX;
            }
        }

        //I use these to quickly determine if a triangle could intersect a point
        private double _MinCtrlY = double.MaxValue;
        private double _MaxCtrlY = double.MinValue;

        public double MinCtrlY
        {
            get
            {
                if (_MinCtrlY == double.MaxValue)
                {
                    _MinCtrlY = Math.Min(Nodes[N1].ControlPoint.Y, Nodes[N2].ControlPoint.Y);
                    _MinCtrlY = Math.Min(_MinCtrlY, Nodes[N3].ControlPoint.Y);
                }

                return _MinCtrlY;
            }
        }

        public double MaxCtrlY
        {
            get
            {
                if (_MaxCtrlY == double.MinValue)
                {
                    _MaxCtrlY = Math.Max(Nodes[N1].ControlPoint.Y, Nodes[N2].ControlPoint.Y);
                    _MaxCtrlY = Math.Max(_MaxCtrlY, Nodes[N3].ControlPoint.Y);
                }

                return _MaxCtrlY;
            }
        }

        #endregion

        public GridRectangle ControlBoundingBox => new GridRectangle(MinCtrlX, MaxCtrlX, MinCtrlY, MaxCtrlY);


        public GridTriangle Control => new GridTriangle(Nodes[N1].ControlPoint, Nodes[N2].ControlPoint, Nodes[N3].ControlPoint);

        public GridTriangle Mapped => new GridTriangle(Nodes[N1].MappedPoint, Nodes[N2].MappedPoint, Nodes[N3].MappedPoint);

        public MappingGridTriangle(MappingGridVector2[] nodes, int n1, int n2, int n3)
        {
            this.Nodes = nodes;
            this.N1 = n1;
            this.N2 = n2;
            this.N3 = n3;
        }

        public MappingGridTriangle Copy()
        {
            return (MappingGridTriangle)((ICloneable)this).Clone();
        }

        object ICloneable.Clone()
        {
            return this.MemberwiseClone();
        }

        public bool CanTransform(in GridVector2 Point)
        {
            return Mapped.Contains(Point);
        }

        public bool CanInverseTransform(in GridVector2 Point)
        {
            return Control.Contains(Point);
        }

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
            //Debug.Assert(BarycentricCoordIsMappable(uv));

            GridVector2 translated = GridVector2.FromBarycentric(Mapped.p1, Mapped.p2, Mapped.p3, uv.Y, uv.X);
            return translated.Round(Global.TransformSignificantDigits);
        }

        public GridVector2[] Transform(in GridVector2[] Points)
        {
            var uv_points = Points.Select(Point => Mapped.Barycentric(Point));
            Debug.Assert(uv_points.All(uv => uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0)));

            return uv_points.Select(uv => GridVector2.FromBarycentric(Control.p1, Control.p2, Control.p3, uv.Y, uv.X).Round(Global.TransformSignificantDigits)).ToArray();
        }

        public GridVector2[] InverseTransform(in GridVector2[] Points)
        {
            var uv_points = Points.Select(Point => Control.Barycentric(Point));
            //  Debug.Assert(uv_points.All(uv => uv.X >= 0.0 && uv.Y >= 0.0 && (uv.X + uv.Y <= 1.0)));

            return uv_points.Select(uv => GridVector2.FromBarycentric(Mapped.p1, Mapped.p2, Mapped.p3, uv.Y, uv.X).Round(Global.TransformSignificantDigits)).ToArray();
        }

        public bool Equals(MappingGridTriangle other)
        {
            if (object.ReferenceEquals(this, other))
                return true;

            if (!object.ReferenceEquals(this.Nodes, other.Nodes))
                return false;

            return this.N1 == other.N1 && this.N2 == other.N2 && this.N3 == other.N3;
        }
         
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
            output = Mapped.Barycentric(Points);
            var wasMapped = output.Select(uv => BarycentricCoordIsMappable(uv)).ToArray();
            for (int i = 0; i < Points.Length; i++)
            {
                if (wasMapped[i] == false)
                {
                    output[i] = default;
                    continue;
                }
                    
                output[i] = GridVector2.FromBarycentric(Control.p1, Control.p2, Control.p3, output[i].Y, output[i].X);
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
            output = Control.Barycentric(Points);
            var wasMapped = output.Select(uv => BarycentricCoordIsMappable(uv)).ToArray();
            for (int i = 0; i < Points.Length; i++)
            {
                if (wasMapped[i] == false)
                {
                    output[i] = default;
                    continue;
                }
                    
                output[i] = GridVector2.FromBarycentric(Mapped.p1, Mapped.p2, Mapped.p3, output[i].Y, output[i].X);
                output[i] = output[i].Round(Global.TransformSignificantDigits);
            } 
            
            return wasMapped;
        }
    }
}

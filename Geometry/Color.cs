using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry.Graphics
{
    [Serializable]
    public struct Color
    {
        public Byte R;
        public Byte G;
        public Byte B;
        public Byte A;

        public Color(Byte r, Byte g, Byte b, Byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            _HashCode = new int?();
        }

        public Color(Byte r, Byte g, Byte b)
        {
            R = r;
            G = g;
            B = b;
            A = 255;
            _HashCode = new int?();
        }

        public Color(double r, double g, double b) : this((byte)(r / (double)byte.MaxValue),
                                                          (byte)(g / (double)byte.MaxValue),
                                                          (byte)(b / (double)byte.MaxValue))
        {

        }

        public Color(double r, double g, double b, double a) : this(r,g,b)
        {
            A = (byte)(a / (double)byte.MaxValue);
        }

        static readonly public Color Blue = new Color(0x00, 0x00, 0xff);
        static readonly public Color Green = new Color(0x00, 0xff, 0x00);
        static readonly public Color Red = new Color(0xff, 0x00, 0x00);
        static readonly public Color Gold = new Color(0xff, 0xd7, 0x00);

        public override string ToString()
        {
            return "R: " + R.ToString() + " G: " + G.ToString() + " B: " + B.ToString();
        }

        public override bool Equals(object obj)
        {
            Color ColorB = (Color)obj;
            return this == ColorB;
        }

        private int? _HashCode;
        public override int GetHashCode()
        {
            if (!_HashCode.HasValue)
            {
                _HashCode = new int?(((int)A << 24 + (int)R << 16 + (int)G << 8 + (int)B)); 
            }

            return _HashCode.Value; 
        }

        public static bool operator ==(Color A, Color B)
        {
            return ((A.R == B.R) &&
                   (A.G == B.G) &&
                   (A.B == B.B) &&
                   (A.A == B.A));
        }

        public static bool operator !=(Color A, Color B)
        {
            return !(A == B);
        }
    }
}

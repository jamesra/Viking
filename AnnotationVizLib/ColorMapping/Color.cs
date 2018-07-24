using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationVizLib.ColorMapping
{
    interface IColor
    {
        double A { get; set; }
        double R { get; set; }
        double G { get; set; }
        double B { get; set; }
    }

    class Color : IColor
    {
        public double _A = 1.0;
        public double _R = 1.0;
        public double _G = 1.0;
        public double _B = 1.0;

        public static Color Empty = new Color(0, 0, 0, 0);

        public double A
        {
            get
            {
                return _A;
            }

            set
            {
                if (value < 0 || value > 1.0)
                    throw new ArgumentException("Value must be in range of 0 to 1.0");
                _A = value;

            }
        }

        public double R
        {
            get
            {
                return _R;
            }

            set
            {
                if (value < 0 || value > 1.0)
                    throw new ArgumentException("Value must be in range of 0 to 1.0");
                _R = value;

            }
        }

        public double G
        {
            get
            {
                return _G;
            }

            set
            {
                if (value < 0 || value > 1.0)
                    throw new ArgumentException("Value must be in range of 0 to 1.0");
                _G = value;

            }
        }

        public double B
        {
            get
            {
                return _B;
            }

            set
            {
                if (value < 0 || value > 1.0)
                    throw new ArgumentException("Value must be in range of 0 to 1.0");
                _B = value;

            }
        }

        public Color(double a, double r, double g, double b)
        {
            this.A = a;
            this.R = r;
            this.G = g;
            this.B = b;
        }
    }

}

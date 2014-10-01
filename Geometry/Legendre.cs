using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{
    public delegate double Poly(double x); 
    public delegate double dPoly(double x); 

    class Legendre
    {
        
            // Legendre polynomials:
        static double P0(double x)
        { return 1.0; }
        
        static double P1(double x)
        { return x; }
        
        static double P2(double x)
        {
          double xx = x * x;
          return (3.0 * xx - 1.0) / 2.0;
        }
        
        static double P3(double x)
        {
          double xx = x * x;
          return ((5.0 * xx - 3.0) * x) / 2.0;
        }
        
        static double P4(double  x)
        {
          double xx = x * x;
          return ((35.0 * xx - 30.0) * xx + 3.0) / 8.0;
        }
        
        static double P5(double  x)
        {
          double xx = x * x;
          return (((63.0 * xx - 70.0) * xx + 15.0) * x) / 8.0;
        }
        
        static double P6(double  x)
        {
          double xx = x * x;
          return (((231.0 * xx - 315.0) * xx + 105.0) * xx - 5.0) / 16.0;
        }
                
        //----------------------------------------------------------------
        // A partial table of the Legendre polynomials
        // 
        public static Poly[] P = { new Poly(P0), new Poly(P1), new Poly(P2), new Poly(P3), new Poly(P4), new Poly(P5), new Poly(P6) };
        
        // first derivatives of the Legendre polynomials:
        static double dP0(double x )
        { return 0.0; }
        
        static double dP1(double x )
        { return 1.0; }
        
        static double dP2(double  x)
        { return 3.0 * x; }
        
        static double dP3(double  x)
        {
          double xx = x * x;
          return (15.0 * xx - 3.0) / 2.0;
        }
        
        static double dP4(double  x)
        {
          double xx = x * x;
          return ((35.0 * xx - 15.0) * x) / 2.0;
        }
        
        static double dP5(double  x)
        {
          double xx = x * x;
          return ((315.0 * xx - 210.0) * xx + 15.0) / 8.0;
        }
        
        static double dP6(double  x)
        {
          double xx = x * x;
          return (((693.0 * xx - 630.0) * xx + 105.0) * x) / 8.0;
        }
        
        //----------------------------------------------------------------
        // A partial table of the derivatives of Legendre polynomials
        // 
        public static dPoly[] dP = { new dPoly(dP0), new dPoly(dP1), new dPoly(dP2), new dPoly(dP3), new dPoly(dP4), new dPoly(dP5), new dPoly(dP6) };
    }
}

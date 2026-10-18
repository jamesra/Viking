namespace Geometry
{
    public delegate double Poly(double x);

    class Legendre
    {

        // Legendre polynomials:
        static double P0(double x) => 1.0;

        static double P1(double x) => x;

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

        static double P4(double x)
        {
            double xx = x * x;
            return ((35.0 * xx - 30.0) * xx + 3.0) / 8.0;
        }

        static double P5(double x)
        {
            double xx = x * x;
            return (((63.0 * xx - 70.0) * xx + 15.0) * x) / 8.0;
        }

        static double P6(double x)
        {
            double xx = x * x;
            return (((231.0 * xx - 315.0) * xx + 105.0) * xx - 5.0) / 16.0;
        }

        //----------------------------------------------------------------
        // A partial table of the Legendre polynomials
        // 
        public static Poly[] P = [new(P0), new(P1), new(P2), new(P3), new(P4), new(P5), new(P6)];
    }
}

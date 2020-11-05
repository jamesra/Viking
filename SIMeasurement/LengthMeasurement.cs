using System;
using System.ComponentModel;

namespace SIMeasurement
{
    public enum SILengthUnits
    {
        [Description("yoctometre")] ym = 0,
        [Description("zeptometre")] zm = 1,
        [Description("attometre")] am = 2,
        [Description("femtometre")] fm = 3,
        [Description("picometre")] pm = 4,
        [Description("nanometre")] nm = 5,
        [Description("micrometre")] um = 6,
        [Description("millimetre")] mm = 7,
        [Description("metre")] m = 8,
        [Description("kilometre")] km = 9,
        [Description("megametre")] Mm = 10,
        [Description("gigametre")] Gm = 11,
        [Description("terametre")] Tm = 12,
        [Description("petametre")] Pm = 13,
        [Description("exametre")] Em = 14,
        [Description("zettametre")] Zm = 15,
        [Description("yottametre")] Ym = 16
    }

    public struct LengthMeasurement
    {
        public readonly SILengthUnits Units;
        public readonly double Length;

        public LengthMeasurement(SILengthUnits units, double scalar)
        {
            this.Units = units;
            this.Length = scalar;
        }

        public override string ToString()
        {
            return Length.ToString("#0.###") + " " + Units.ToString();
        }

        public string ToString(uint scale, bool PreserveNonSignificant = true)
        {
            if (PreserveNonSignificant)
            {
                return Length.ToString($"F{scale}") + " " + Units.ToString();
            }

            if (scale > 32)
                throw new ArgumentException("Scale must be between 0 and 64");


            string format = "#0.";
            while (scale > 0)
            {
                format += '0';
            }

            return Length.ToString(format) + " " + Units.ToString();
        }

        /// <summary>
        /// Given a starting distance and measurement we return a unit and scalar that will result in a distance of less than 1,000 
        /// </summary>
        /// <param name="UnitOfMeasure"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static LengthMeasurement ConvertToReadableUnits(SILengthUnits UnitOfMeasure, double distance)
        {
            if (distance <= 0)
                return new LengthMeasurement(UnitOfMeasure, 1.0);

            double numDigits = Convert.ToInt32(Math.Ceiling(Math.Log10(distance)));

            if (numDigits < 3 && numDigits > 0)
            {
                return new LengthMeasurement(UnitOfMeasure, distance);
            }

            int iStartUnit = (int)UnitOfMeasure;

            //Figure out how many 1,000 sized steps we make
            int numUnitHops = Convert.ToInt32(Math.Floor(numDigits / 3.0));
            int numUnitDefinitions = Enum.GetValues(typeof(SILengthUnits)).Length;

            //Handle units that are out of our range
            if (numUnitHops + iStartUnit >= numUnitDefinitions)
            {
                numUnitHops = (numUnitDefinitions - 1) - iStartUnit;
            }
            else if (numUnitHops + iStartUnit < 0)
            {
                numUnitHops = -iStartUnit;
            }

            int iUnit = numUnitHops + iStartUnit;


            System.Diagnostics.Debug.Assert(iUnit >= 0, "Unexpectedly small unit of measure found");
            System.Diagnostics.Debug.Assert(Enum.IsDefined(typeof(SILengthUnits), iUnit), "Unit not defined");

            //Handle units too large
            /*
            if(!Enum.IsDefined(typeof(SILengthUnits), iUnit))
            {
                while(iUnit < 0)
                {
                    iUnit++;
                    numUnitHops--;
                }
            }
            */

            double unitScalar = 1.0 / Math.Pow(1000, numUnitHops);
            SILengthUnits newUnit = (SILengthUnits)iUnit;
            return new LengthMeasurement(newUnit, distance * unitScalar);
        }

        public static LengthMeasurement ConvertToReadableUnits(LengthMeasurement measurement)
        {
            return LengthMeasurement.ConvertToReadableUnits(measurement.Units, measurement.Length);
        }

        public LengthMeasurement ConvertTo(SILengthUnits newUnit)
        {
            if (this.Units == newUnit)
                return new LengthMeasurement(this.Units, this.Length);

            int iStartUnit = (int)this.Units;
            int iTargetUnit = (int)newUnit;

            int numUnitHops = iTargetUnit - iStartUnit;
            double unitScalar = 1.0 / Math.Pow(1000, numUnitHops);

            return new LengthMeasurement(newUnit, this.Length * unitScalar);
        }


        /// <summary>
        /// Convert the other length measurement to our units
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        private LengthMeasurement ConvertToSameUnits(LengthMeasurement other)
        {
            return other.ConvertTo(this.Units);
        }

        private static SILengthUnits LargestUnitOfMeasure(LengthMeasurement A, LengthMeasurement B)
        {
            if (A.Units >= B.Units)
            {
                return A.Units;
            }
            else
            {
                return B.Units;
            }
        }

        public static LengthMeasurement operator +(LengthMeasurement A, LengthMeasurement B)
        {
            SILengthUnits DesiredUnits = LargestUnitOfMeasure(A, B);
            A = A.ConvertTo(DesiredUnits);
            B = B.ConvertTo(DesiredUnits);

            return LengthMeasurement.ConvertToReadableUnits(DesiredUnits, A.Length + B.Length);
        }

        public static LengthMeasurement operator -(LengthMeasurement A, LengthMeasurement B)
        {
            SILengthUnits DesiredUnits = LargestUnitOfMeasure(A, B);
            A = A.ConvertTo(DesiredUnits);
            B = B.ConvertTo(DesiredUnits);

            return LengthMeasurement.ConvertToReadableUnits(DesiredUnits, A.Length - B.Length);
        }

        /// <summary>
        /// Returns the length of A in units of B
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static double operator /(LengthMeasurement A, LengthMeasurement B)
        {
            A = A.ConvertTo(B.Units);

            return A.Length / B.Length;
        }

        /*
         * Returns an area... not needed at this time so I'm leaving it alone.
        public static double operator *(LengthMeasurement A, LengthMeasurement B)
        {
            SILengthUnits DesiredUnits = LargestUnitOfMeasure(A, B);
            A = A.ConvertTo(DesiredUnits);
            B = B.ConvertTo(DesiredUnits);

            return LengthMeasurement.ConvertToReadableUnits(DesiredUnits, A.Length * B.Length);
        }
        */
    }
}

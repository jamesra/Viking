using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="Legendre.P"/>, the P0..P6 table that <c>TransformFactory</c> and <c>GridTransformFactory</c> evaluate
    /// for Legendre polynomial section transforms. The golden table holds exact IEEE bit patterns captured from the code
    /// (identical on net48 and net9.0), so any change to an evaluation order or constant fails here; the remaining tests check the
    /// mathematics: endpoint values, values at zero, and Bonnet's recurrence.
    /// </summary>
    [TestClass]
    public class LegendreTests
    {
        /// <summary>Number of polynomials in the table (P0..P6).</summary>
        private const int Count = 7;

        /// <summary>
        /// Rows of { bits of x, bits of P0(x)..P6(x) }. x covers both endpoints, zero, thirds, and two points outside [-1, 1].
        /// </summary>
        private static readonly (long X, long[] P)[] Golden =
        {
            (-4616189618054758400L, new long[] { 4607182418800017408L, -4616189618054758400L, 4607182418800017408L, -4616189618054758400L, 4607182418800017408L, -4616189618054758400L, 4607182418800017408L }),
            (-4618441417868443648L, new long[] { 4607182418800017408L, -4618441417868443648L, 4599864069405540352L, 4589730970243956736L, -4623393618239946752L, 4601172488242593792L, -4624642388571193344L }),
            (-4620693217682128896L, new long[] { 4607182418800017408L, -4620693217682128896L, -4629700416936869888L, 4601552919265804288L, -4624493129867722752L, -4632233691727265792L, 4599494633498607616L }),
            (-4623695617433709227L, new long[] { 4607182418800017408L, -4623695617433709227L, -4623695617433709226L, 4601010819310657840L, 4578270421192206816L, -4623695617433709227L, 4596136553047289768L }),
            (-4631501856787818086L, new long[] { 4607182418800017408L, -4631501856787818086L, -4620963433659771126L, 4594482267850832610L, 4599759360714203988L, -4627761031829335331L, -4625238995771809530L }),
            (0L, new long[] { 4607182418800017408L, 0L, -4620693217682128896L, -9223372036854775808L, 4600427019358961664L, 0L, -4624070917402656768L }),
            (4591870180066957722L, new long[] { 4607182418800017408L, 4591870180066957722L, -4620963433659771126L, -4628889769003943198L, 4599759360714203988L, 4595611005025440477L, -4625238995771809530L }),
            (4598175219545276416L, new long[] { 4607182418800017408L, 4598175219545276416L, -4622382067542392832L, -4623648704937590784L, 4594850296382881792L, 4599791501638107136L, 4582654513407590400L }),
            (4599676419421066581L, new long[] { 4607182418800017408L, 4599676419421066581L, -4623695617433709226L, -4622361217544117968L, 4578270421192206816L, 4599676419421066581L, 4596136553047289768L }),
            (4602678819172646912L, new long[] { 4607182418800017408L, 4602678819172646912L, -4629700416936869888L, -4621819117588971520L, -4624493129867722752L, 4591138345127510016L, 4599494633498607616L }),
            (4604480259023595110L, new long[] { 4607182418800017408L, 4604480259023595110L, 4597634787589991952L, -4627268473138089817L, -4622277358851056468L, -4623121581119205204L, -4629690101441923414L }),
            (4606281698874543309L, new long[] { 4607182418800017408L, 4606281698874543309L, 4604615367012416226L, 4602183423213636163L, 4596659758270666249L, -4637282136837555565L, -4625515156500959906L }),
            (4607182418800017408L, new long[] { 4607182418800017408L, 4607182418800017408L, 4607182418800017408L, 4607182418800017408L, 4607182418800017408L, 4607182418800017408L, 4607182418800017408L }),
            (-4610560118520545280L, new long[] { 4607182418800017408L, -4610560118520545280L, 4621185798891372544L, -4593205426987728896L, 4639405531197341696L, -4574557606701432832L, 4658367852611371008L }),
            (4613937818241073152L, new long[] { 4607182418800017408L, 4613937818241073152L, 4623507967449235456L, 4634063279075885056L, 4644354707911868416L, 4655116727724539904L, 4666167369339502592L }),
        };

        [TestMethod]
        public void TableHasSevenEntries() => Assert.AreEqual(Count, Legendre.P.Length);

        [TestMethod]
        public void ValuesMatchCapturedBitsExactly()
        {
            foreach ((long xBits, long[] expected) in Golden)
            {
                double x = BitConverter.Int64BitsToDouble(xBits);
                for (int n = 0; n < Count; n++)
                {
                    Assert.AreEqual(expected[n], BitConverter.DoubleToInt64Bits(Legendre.P[n](x)), $"P{n}({x:R})");
                }
            }
        }

        [TestMethod]
        public void EndpointsAreExactlyPlusOrMinusOne()
        {
            for (int n = 0; n < Count; n++)
            {
                Assert.AreEqual(1.0, Legendre.P[n](1.0), $"P{n}(1)");
                Assert.AreEqual(n % 2 == 0 ? 1.0 : -1.0, Legendre.P[n](-1.0), $"P{n}(-1)");
            }
        }

        [TestMethod]
        public void ValuesAtZeroAreKnownConstants()
        {
            // P_n(0) = 0 for odd n, and (-1)^(n/2) (n-1)!! / n!! for even n: 1, -1/2, 3/8, -5/16.
            double[] expected = { 1.0, 0.0, -0.5, 0.0, 0.375, 0.0, -0.3125 };
            for (int n = 0; n < Count; n++)
                Assert.AreEqual(expected[n], Legendre.P[n](0.0), $"P{n}(0)");
        }

        [TestMethod]
        public void ValuesAtOneHalfAreKnownConstants()
        {
            // Exact dyadic values of P_n(1/2): 1, 1/2, -1/8, -7/16, -37/128, 23/256, 331/1024.
            double[] expected = { 1.0, 0.5, -0.125, -0.4375, -37.0 / 128.0, 23.0 / 256.0, 331.0 / 1024.0 };
            for (int n = 0; n < Count; n++)
                Assert.AreEqual(expected[n], Legendre.P[n](0.5), $"P{n}(1/2)");
        }

        /// <summary>
        /// Bonnet's recurrence (n+1) P_{n+1}(x) = (2n+1) x P_n(x) - n P_{n-1}(x) holds for x across [-1, 1] and a little beyond.
        /// The tolerance is relative to the magnitude of the terms (64 ulps of their sum), so it scales with |x|.
        /// </summary>
        [TestMethod]
        public void BonnetRecurrenceHolds()
        {
            Gen<double> xs = Gen.Choose(-1_500_000, 1_500_000).Select(i => i / 1_000_000.0);
            CoreCheck.Run(
                Prop.ForAll(Arb.From(xs), x =>
                {
                    for (int n = 1; n < Count - 1; n++)
                    {
                        double lhs = (n + 1) * Legendre.P[n + 1](x);
                        double a = (2 * n + 1) * x * Legendre.P[n](x);
                        double b = n * Legendre.P[n - 1](x);
                        double tolerance = 64 * 2.220446049250313e-16 * (Math.Abs(lhs) + Math.Abs(a) + Math.Abs(b));
                        if (Math.Abs(lhs - (a - b)) > tolerance)
                            return false;
                    }
                    return true;
                }),
                nameof(BonnetRecurrenceHolds));
        }

        /// <summary>Parity: P_n(-x) = (-1)^n P_n(x), exactly, since each P_n is a polynomial in x*x times x^(n mod 2).</summary>
        [TestMethod]
        public void ParityHoldsExactly()
        {
            Gen<double> xs = Gen.Choose(-1_000_000, 1_000_000).Select(i => i / 1_000_000.0);
            CoreCheck.Run(
                Prop.ForAll(Arb.From(xs), x =>
                {
                    for (int n = 0; n < Count; n++)
                    {
                        double sign = n % 2 == 0 ? 1.0 : -1.0;
                        if (Legendre.P[n](-x) != sign * Legendre.P[n](x))
                            return false;
                    }
                    return true;
                }),
                nameof(ParityHoldsExactly));
        }
    }
}

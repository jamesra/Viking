using System;

namespace VikingXNAGraphics
{
    public static class MathnetMatrixExtensions
    {
        public static MathNet.Numerics.LinearAlgebra.Matrix<double> ToMathnetMatrix(this Microsoft.Xna.Framework.Matrix m)
        {
            return MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.DenseOfRowArrays(
                new double[][] {  new double[] { m.M11, m.M12, m.M13, m.M14 },
                                new double[] { m.M21, m.M22, m.M23, m.M24 },
                                new double[] { m.M31, m.M32, m.M33, m.M34 },
                                new double[] { m.M41, m.M42, m.M43, m.M44 } });
        }

    }

    public static class MathnetVectorExtensions
    {
        public static Microsoft.Xna.Framework.Vector3 ToXNAVector3(this MathNet.Numerics.LinearAlgebra.Vector<double> v)
        {
            if (v.Count < 3)
                throw new ArgumentException("MathNet.Vector must have at least 3 elements to create XNA vector 3");

            return new Microsoft.Xna.Framework.Vector3((float)v[0], (float)v[1], (float)v[2]);
        }

        public static Microsoft.Xna.Framework.Vector2 ToXNAVector2(this MathNet.Numerics.LinearAlgebra.Vector<double> v)
        {
            if (v.Count >= 2)
                throw new ArgumentException("MathNet.Vector must have at least 3 elements to create XNA vector 3");

            return new Microsoft.Xna.Framework.Vector2((float)v[0], (float)v[1]);
        }
    } 
}

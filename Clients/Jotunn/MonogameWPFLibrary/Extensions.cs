using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonogameWPFLibrary
{
    public static class VectorExtensions
    {
        public static Microsoft.Xna.Framework.Vector2 ToXNAVector2(this System.Windows.Vector v)
        {
            return new Microsoft.Xna.Framework.Vector2((float)v.X, (float)v.Y);
        }

        public static Microsoft.Xna.Framework.Vector3 ToXNAVector3(this System.Windows.Vector v, float z = 0f)
        {
            return new Microsoft.Xna.Framework.Vector3((float)v.X, (float)v.Y, z);
        }
    }

    public static class GeometryMonogameExtensions
    {
        public static Microsoft.Xna.Framework.Vector2 ToXNAVector2(this Geometry.GridVector2 v)
        {
            return new Microsoft.Xna.Framework.Vector2((float)v.X, (float)v.Y);
        }

        public static Microsoft.Xna.Framework.Vector3 ToXNAVector3(this Geometry.GridVector3 v)
        {
            return new Microsoft.Xna.Framework.Vector3((float)v.X, (float)v.Y, (float)v.Z);
        }

        public static Microsoft.Xna.Framework.Vector3 ToXNAVector3(this Geometry.GridVector2 v, double z = 0)
        {
            return new Microsoft.Xna.Framework.Vector3((float)v.X, (float)v.Y, (float)z);
        }

        public static Geometry.GridVector3 ToGridVector3(this Microsoft.Xna.Framework.Vector3 v)
        {
            return new Geometry.GridVector3(v.X, v.Y, v.Z);
        }

        public static Geometry.GridVector3 ToGridVector3(this Microsoft.Xna.Framework.Vector2 v, double z = 0)
        {
            return new Geometry.GridVector3(v.X, v.Y, z);
        }
    }

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

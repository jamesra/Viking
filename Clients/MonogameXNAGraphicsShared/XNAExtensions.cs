using Microsoft.Xna.Framework;

namespace Microsoft.Xna.Framework
{
    public static class Vector4Extensions
    {
        public static Color ToColor(this Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);

        public static Vector4 ToVector(this float[] array)
        {
            if (array is null || array.Length != 4)
                throw new System.ArgumentException("Array must be of length 4", nameof(array));

            return new Vector4(array[0], array[1], array[2], array[3]);
        }

        public static Vector4 ToVector(this double[] array)
        {
            if (array is null || array.Length != 4)
                throw new System.ArgumentException("Array must be of length 4", nameof(array));

            return new Vector4((float)array[0], (float)array[1], (float)array[2], (float)array[3]);
        }
    }
}
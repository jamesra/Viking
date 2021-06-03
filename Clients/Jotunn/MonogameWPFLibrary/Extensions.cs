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

}

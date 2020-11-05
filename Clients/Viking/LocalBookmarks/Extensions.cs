using System;

namespace LocalBookmarks
{
    public static class Extensions
    {
        public static ShapeType ToShape(this string shape)
        {
            switch (shape.ToUpper())
            {
                case "ARROW":
                    return ShapeType.ARROW;
                case "RING":
                    return ShapeType.RING;
                case "STAR":
                    return ShapeType.STAR;
                case "INHERIT":
                    return ShapeType.INHERIT;
                default:
                    throw new ArgumentException("Unknown shape: " + shape);
            }
        }

        public static string ToShapeString(this ShapeType shape)
        {
            switch (shape)
            {
                case ShapeType.ARROW:
                    return "Arrow";
                case ShapeType.RING:
                    return "Ring";
                case ShapeType.STAR:
                    return "Star";
                case ShapeType.INHERIT:
                    return "Inherit";
                default:
                    throw new ArgumentException("Unknown shape: " + shape.ToString());
            }
        }

        public static Microsoft.Xna.Framework.Graphics.Texture2D ToTexture(this ShapeType shape)
        {
            switch (shape)
            {
                case ShapeType.ARROW:
                    return BookmarkOverlay.ArrowTexture;
                case ShapeType.RING:
                    return BookmarkOverlay.RingTexture;
                case ShapeType.STAR:
                    return BookmarkOverlay.StarTexture;
                case ShapeType.INHERIT:
                    return null;
                default:
                    throw new ArgumentException("Unknown shape: " + shape.ToString());
            }
        }
    }
}

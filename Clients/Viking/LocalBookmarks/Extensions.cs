using System;

namespace LocalBookmarks
{
    public static class Extensions
    {
        public static ShapeType ToShape(this string shape)
        {
            return shape.ToUpper() switch
            {
                "ARROW" => ShapeType.ARROW,
                "RING" => ShapeType.RING,
                "STAR" => ShapeType.STAR,
                "INHERIT" => ShapeType.INHERIT,
                _ => throw new ArgumentException("Unknown shape: " + shape),
            };
        }

        public static string ToShapeString(this ShapeType shape)
        {
            return shape switch
            {
                ShapeType.ARROW => "Arrow",
                ShapeType.RING => "Ring",
                ShapeType.STAR => "Star",
                ShapeType.INHERIT => "Inherit",
                _ => throw new ArgumentException("Unknown shape: " + shape.ToString()),
            };
        }

        public static Microsoft.Xna.Framework.Graphics.Texture2D? ToTexture(this ShapeType shape)
        {
            return shape switch
            {
                ShapeType.ARROW => BookmarkOverlay.ArrowTexture,
                ShapeType.RING => BookmarkOverlay.RingTexture,
                ShapeType.STAR => BookmarkOverlay.StarTexture,
                ShapeType.INHERIT => null,
                _ => throw new ArgumentException("Unknown shape: " + shape.ToString()),
            };
        }
    }
}

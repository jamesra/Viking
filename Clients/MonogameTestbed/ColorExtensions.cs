using Microsoft.Xna.Framework;
using MorphologyMesh;
using System;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    public static class TestbedColorExtensions
    {

        public static Color GetColor(this EdgeType type)
        {
            return type switch
            {
                EdgeType.INVALID => Color.GhostWhite.SetAlpha(0.25f),
                EdgeType.UNKNOWN => Color.Black,
                EdgeType.FLYING => Color.Pink.SetAlpha(0.5f),
                EdgeType.CONTOUR => Color.Cyan.SetAlpha(0.5f),
                EdgeType.SURFACE => Color.Blue.SetAlpha(0.5f),
                EdgeType.CORRESPONDING => Color.Gold.SetAlpha(0.5f),
                EdgeType.INTERNAL => Color.Red.SetAlpha(0.5f),
                EdgeType.FLAT => Color.Brown.SetAlpha(0.5f),
                EdgeType.INVAGINATION => Color.Orange.SetAlpha(0.5f),
                EdgeType.HOLE => Color.Purple.SetAlpha(0.5f),
                EdgeType.FLIPPED_DIRECTION => Color.Black.SetAlpha(0.5f),
                EdgeType.UNTILED => Color.Black.SetAlpha(1.0f),
                EdgeType.MEDIALAXIS => Color.LightCyan.SetAlpha(0.5f),
                EdgeType.CONTOUR_TO_MEDIALAXIS => Color.DarkCyan.SetAlpha(0.5f),
                EdgeType.ARTIFICIAL => Color.YellowGreen.SetAlpha(0.5f),
                _ => throw new ArgumentException("Unknown line type " + type.ToString()),
            };
        }

        public static Color GetColor(this RegionType type)
        {
            return type switch
            {
                RegionType.EXPOSED => Color.Blue.SetAlpha(0.5f),
                RegionType.HOLE => Color.GhostWhite.SetAlpha(0.5f),
                RegionType.INVAGINATION => Color.Purple.SetAlpha(0.5f),
                RegionType.UNTILED => Color.Green.SetAlpha(0.5f),
                _ => throw new ArgumentException("Unknown region type " + type.ToString()),
            };
        }
    }
}

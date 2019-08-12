using System;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using MorphologyMesh;

namespace MonogameTestbed
{
    public static class TestbedColorExtensions
    {

        public static Color GetColor(this EdgeType type)
        {
            switch (type)
            {
                case EdgeType.INVALID:
                    return Color.GhostWhite.SetAlpha(0.25f);
                case EdgeType.UNKNOWN:
                    return Color.Black;
                case EdgeType.FLYING:
                    return Color.Pink.SetAlpha(0.5f);
                case EdgeType.CONTOUR:
                    return Color.Cyan.SetAlpha(0.5f);
                case EdgeType.SURFACE:
                    return Color.Blue.SetAlpha(0.5f);
                case EdgeType.CORRESPONDING:
                    return Color.Gold.SetAlpha(0.5f);
                case EdgeType.INTERNAL:
                    return Color.Red.SetAlpha(0.5f);
                case EdgeType.FLAT:
                    return Color.Brown.SetAlpha(0.5f);
                case EdgeType.INVAGINATION:
                    return Color.Orange.SetAlpha(0.5f);
                case EdgeType.HOLE:
                    return Color.Purple.SetAlpha(0.5f);
                case EdgeType.FLIPPED_DIRECTION:
                    return Color.Black.SetAlpha(0.5f);
                case EdgeType.UNTILED:
                    return Color.Black.SetAlpha(1.0f);
                case EdgeType.MEDIALAXIS:
                    return Color.LightCyan.SetAlpha(0.5f);
                case EdgeType.CONTOUR_TO_MEDIALAXIS:
                    return Color.DarkCyan.SetAlpha(0.5f);
                case EdgeType.ARTIFICIAL:
                    return Color.YellowGreen.SetAlpha(0.5f);

                default:
                    throw new ArgumentException("Unknown line type " + type.ToString());
            }
        }

        public static Color GetColor(this RegionType type)
        {
            switch (type)
            {
                case RegionType.EXPOSED:
                    return Color.Blue.SetAlpha(0.5f);
                case RegionType.HOLE:
                    return Color.GhostWhite.SetAlpha(0.5f);
                case RegionType.INVAGINATION:
                    return Color.Purple.SetAlpha(0.5f);
                case RegionType.UNTILED:
                    return Color.Green.SetAlpha(0.5f);
                default:
                    throw new ArgumentException("Unknown region type " + type.ToString());
            }
        }
    }
}

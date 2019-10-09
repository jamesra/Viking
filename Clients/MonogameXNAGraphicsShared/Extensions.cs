using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    public enum LineStyle
    {
        Standard,
        AlphaGradient,
        NoBlur,
        AnimatedLinear,
        AnimatedBidirectional,
        AnimatedRadial,
        Ladder,
        Tubular,
        HalfTube,
        Glow,
        Textured
    }

    public struct HSLColor
    {
        /// <summary>
        /// Alpha from 0 to 1
        /// </summary>
        public float Alpha;

        /// <summary>
        /// Hue angle in degrees
        /// </summary>
        public float Hue;

        /// <summary>
        /// Saturation from 0 to 1
        /// </summary>
        public float Saturation;

        /// <summary>
        /// Luminance from 0 to 1
        /// </summary>
        public float Luminance;
    }

    public static class FloatExtensions
    {
        /// <summary>
        /// Return the power of two less than or equal to the passed value
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static double FloorToPowerOfTwo(this double value)
        {
            return Math.Pow(Math.Floor(Math.Log(value, 2)), 2);
        }
    }

    public static class LineManagerExtensions
    {
        public static string ToString(this LineStyle style)
        {
            switch (style)
            {
                case LineStyle.Standard:
                    return "Standard";
                case LineStyle.AlphaGradient:
                    return "AlphaGradient";
                case LineStyle.NoBlur:
                    return "NoBlur";
                case LineStyle.AnimatedLinear:
                    return "AnimatedLinear";
                case LineStyle.AnimatedBidirectional:
                    return "AnimatedBidirectional";
                case LineStyle.AnimatedRadial:
                    return "AnimatedRadial";
                case LineStyle.Ladder:
                    return "Ladder";
                case LineStyle.Tubular:
                    return "Tubular";
                case LineStyle.HalfTube:
                    return "HalfTube";
                case LineStyle.Glow:
                    return "Glow";
                case LineStyle.Textured:
                    return "Textured";
                default:
                    throw new ArgumentException("Unknown line style " + style.ToString());
            }
        }
    }

    public static class MeshExtensions
    {
        public static MeshModel<VertexPositionColor> ToVertexPositionColorMeshModel(this Geometry.Meshing.DynamicRenderMesh mesh, Color color)
        {
            MeshModel<VertexPositionColor> model = new MeshModel<VertexPositionColor>();

            mesh.ConvertAllFacesToTriangles();

            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionColor(v.Position.ToXNAVector3(), color)).ToArray();
            model.Edges = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
        }

        public static MeshModel<VertexPositionColor> ToVertexPositionColorMeshModel(this Geometry.Meshing.DynamicRenderMesh mesh, Color[] colors)
        {
            if(mesh.Verticies.Count != colors.Length)
            {
                throw new ArgumentException("Number of colors must match number of verticies");
            }

            MeshModel<VertexPositionColor> model = new MeshModel<VertexPositionColor>();

            //Convert model to triangles if needed 
            mesh.ConvertAllFacesToTriangles();

            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionColor(v.Position.ToXNAVector3(), colors[i])).ToArray();
            model.Edges = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
        }

        public static MeshModel<VertexPositionNormalColor> ToVertexPositionNormalColorMeshModel(this Geometry.Meshing.DynamicRenderMesh mesh, Color color)
        {
            MeshModel<VertexPositionNormalColor> model = new MeshModel<VertexPositionNormalColor>();

            mesh.ConvertAllFacesToTriangles();

            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionNormalColor(v.Position.ToXNAVector3(), v.Normal.ToXNAVector3(), color)).ToArray();
            model.Edges = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
        }

        public static MeshModel<VertexPositionNormalColor> ToVertexPositionNormalColorMeshModel(this Geometry.Meshing.DynamicRenderMesh mesh, Color[] colors)
        {
            if (mesh.Verticies.Count != colors.Length)
            {
                throw new ArgumentException("Number of colors must match number of verticies");
            }

            MeshModel<VertexPositionNormalColor> model = new MeshModel<VertexPositionNormalColor>();

            //Convert model to triangles if needed 
            mesh.ConvertAllFacesToTriangles();

            model.Verticies = mesh.Verticies.Select((v, i) => new VertexPositionNormalColor(v.Position.ToXNAVector3(), v.Normal.ToXNAVector3(), colors[i])).ToArray();
            model.Edges = mesh.Faces.SelectMany(f => f.iVerts).ToArray();
            return model;
        }

    }


    public static class VectorExtensions
    {
        public static Microsoft.Xna.Framework.Vector2 ToXNAVector2(this Geometry.GridVector2 vec)
        {
            return new Vector2((float)vec.X, (float)vec.Y);
        }

        public static Geometry.GridVector2 ToGridVector2(this Vector2 vec)
        {
            return new Geometry.GridVector2(vec.X, vec.Y);
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

    public static class ColorExtensions
    {
        /// <summary>
        /// Return alpha 0 to 1
        /// </summary>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        /// <returns></returns>
        public static float GetAlpha(this Microsoft.Xna.Framework.Color color)
        {
            return (float)color.A / 255.0f;
        }

        public static Microsoft.Xna.Framework.Color SetAlpha(this Microsoft.Xna.Framework.Color color, float alpha)
        {
            if (alpha > 1.0f || alpha < 0f)
            {
                throw new ArgumentOutOfRangeException("Alpha value must be between 0 to 1.0");
            }

            Vector3 colorVector = color.ToVector3();
            return new Microsoft.Xna.Framework.Color(colorVector.X, colorVector.Y, colorVector.Z, alpha);
        }

        public static Microsoft.Xna.Framework.Color Invert(this Microsoft.Xna.Framework.Color color, float? alpha = new float?())
        {
            if(alpha.HasValue == false)
            {
                alpha = color.GetAlpha();
            }

            Vector3 colorVector = Vector3.One - color.ToVector3(); 
            return new Microsoft.Xna.Framework.Color(colorVector.X, colorVector.Y, colorVector.Z, alpha.Value);
        }

        private static Microsoft.Xna.Framework.Color ToXNAColor(int[] ARGB)
        {
            return new Microsoft.Xna.Framework.Color(ARGB[1],
                                                    ARGB[2],
                                                    ARGB[3],
                                                    ARGB[0]);
        }

        private enum ColorComponent
        {
            ALPHA = 24,
            RED = 16,
            GREEN = 8,
            BLUE = 0
        };

        public static byte[] ToBytes(this Color color)
        {
            return new byte[] { color.R, color.G, color.B, color.A };
        }

        public static string ToHexString(this Color color)
        {
            return "0x" + BitConverter.ToString(color.ToBytes()).Replace("-",string.Empty);
        }

        private static int GetColorComponent(int color, ColorComponent comp)
        {
            int mask = 0x000000FF;

            switch (comp)
            {
                case ColorComponent.ALPHA:
                    color = color >> 24;
                    return color;
                case ColorComponent.RED:
                    color = color >> 16;
                    color &= mask;
                    return color;
                case ColorComponent.GREEN:
                    color = color >> 8;
                    color &= mask;
                    return color;
                case ColorComponent.BLUE:
                    color &= mask;
                    return color;
            }

            throw new ArgumentException("Unexpected color component requested");
        }

        /// <summary>
        /// Convert an integer color into an array of ARGB bytes
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private static int[] GetColorComponents(int color)
        {
            int mask = 0x000000FF;

            int[] ARGB = new int[4];
            ARGB[0] = (color >> 24) & mask;
            ARGB[1] = (color >> 16) & mask;
            ARGB[2] = (color >> 8) & mask;
            ARGB[3] = color & mask;

            return ARGB;
        }

        /// <summary>
        /// Convert an integer color into an array of normalized ARGB floats
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private static float[] GetColorFloatComponents(int color)
        {
            float[] ARGB = new float[4];
            int[] iARGB = GetColorComponents(color);

            ARGB[0] = ((float)iARGB[0]) / 256f;
            ARGB[1] = ((float)iARGB[1]) / 256f;
            ARGB[2] = ((float)iARGB[2]) / 256f;
            ARGB[3] = ((float)iARGB[3]) / 256f;

            return ARGB;
        }

        static System.Random rgen = new System.Random();

        public static Microsoft.Xna.Framework.Color Random(this Color color)
        {
            Color c = new Color((float)rgen.NextDouble(),
                                (float)rgen.NextDouble(),
                                (float)rgen.NextDouble());
            return c;
        }

        /// <summary>
        /// Return a shade of grey matching the value.  0 is black. 1 is white.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Microsoft.Xna.Framework.Color CreateGrayscale(double value)
        {
            System.Diagnostics.Debug.Assert(value >= 0 && value <= 1.0);

            Color c = new Color((float)value,
                                (float)value,
                                (float)value);
            return c;
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this int color, float alpha)
        {
            int[] ARGB = GetColorComponents(color);
            ARGB[0] = (int)(alpha * 255.0f);
            return ToXNAColor(ARGB);
        }

        public static Microsoft.Xna.Framework.Color ToXNAColor(this int color)
        {
            int[] ARGB = GetColorComponents(color);
            return ToXNAColor(ARGB);
        }

        /// <summary>
        /// Returns HSL value with Hue in degrees
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static HSLColor GetHSL(this Microsoft.Xna.Framework.Color color)
        {
            float A = (float)color.A / 255f;
            float R = (float)color.R / 255f;
            float G = (float)color.G / 255f;
            float B = (float)color.B / 255f;

            byte min = Math.Min(color.R, Math.Min(color.G, color.B));
            byte max = Math.Max(color.R, Math.Max(color.G, color.B));

            float f_max = ((float)max) / 255f;
            float f_min = ((float)min) / 255f;

            int max_min_diff = max - min;
            float f_max_min_diff = f_max - f_min;

            HSLColor hsl;
            hsl.Alpha = A;
            hsl.Luminance = (((float)(max + min)) / 2f) / 255f;

            if (min == max)
            {
                //If the min & max are equal we have a shade of grey, there is no hue or saturation, only luminance.
                hsl.Hue = 0;
                hsl.Saturation = 0;
                return hsl;
            }

            if (hsl.Luminance < 0.5)
            {
                hsl.Saturation = f_max_min_diff / (f_max + f_min);
            }
            else
            {
                hsl.Saturation = f_max_min_diff / (2f - f_max_min_diff);
            }

            if (max == color.R)
            {
                hsl.Hue = (G - B) / f_max_min_diff;
            }
            else if (max == color.G)
            {
                hsl.Hue = 2f + (B - R) / f_max_min_diff;
            }
            else // max == color.B
            {
                hsl.Hue = 4f + (R - G) / f_max_min_diff;
            }

            //Convert Hue to degrees
            hsl.Hue *= 60;

            if (hsl.Hue < 0)
            {
                hsl.Hue += 360;
            }

            return hsl;
        }

        public static Microsoft.Xna.Framework.Color ConvertToHSL(this Microsoft.Xna.Framework.Color color, float alpha)
        {
            HSLColor hsl = color.GetHSL();

            Microsoft.Xna.Framework.Color HSLColor = new Microsoft.Xna.Framework.Color();
            HSLColor.R = (byte)(255.0 * (hsl.Hue / 360.0));
            HSLColor.G = (byte)(255.0 * hsl.Saturation);
            HSLColor.B = (byte)(255.0 * hsl.Luminance);
            HSLColor.A = (byte)(hsl.Alpha * 255f);

            return HSLColor;
        }

        public static Microsoft.Xna.Framework.Color ConvertToHSL(this Microsoft.Xna.Framework.Color color)
        {
            return color.ConvertToHSL((float)color.A / 255f);
        }

        /// <summary>
        /// Passed an HSL color, reverse the hue angle 180 degrees
        /// </summary>
        /// <param name="colorHSL">Input color in HSL space</param>
        /// <param name="rotation_degrees">Angle in degrees to add to hue</param>
        /// <param name="alpha">0 to 1</param>
        /// <returns></returns>
        public static Microsoft.Xna.Framework.Color AdjustHSLHue(this Microsoft.Xna.Framework.Color colorHSL, float rotation_degrees, float? alpha= null)
        {
            float HueAngle = ((float)colorHSL.R / 255f) * 360;

            HueAngle += rotation_degrees;
            if (HueAngle >= 360.0f)
                HueAngle -= 360.0f;
                          
            Microsoft.Xna.Framework.Color OutputHSL = new Microsoft.Xna.Framework.Color();
            OutputHSL.R = (byte)(255.0 * (HueAngle / 360.0));
            OutputHSL.G = colorHSL.G;
            OutputHSL.B = colorHSL.B;

            if(alpha.HasValue)
                OutputHSL.A = (byte)(alpha * 255f);
            else
                OutputHSL.A = colorHSL.A;

            return OutputHSL;
        }

        public static Vector2[] MeasureStrings(this SpriteFont font, string[] lines)
        {
            return lines.Select(line => font.MeasureString(line)).ToArray();
        }
    }

    public static class BasicEffectExtensions
    {
        public static void SetScene(this BasicEffect basicEffect, VikingXNA.Scene scene)
        {
            basicEffect.Projection = scene.Projection;
            basicEffect.View = scene.View;
            basicEffect.World = scene.World;
        }

        public static void SetScene(this BasicEffect basicEffect, VikingXNA.IScene scene)
        {
            basicEffect.Projection = scene.Projection;
            basicEffect.View = scene.View;
            basicEffect.World = scene.World;
        }
    }

    public static class RenderTarget2DExtensions
    {
        public static byte[] GetData(this RenderTarget2D renderTarget)
        {
            Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[] Data = new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4[renderTarget.Bounds.Width * renderTarget.Bounds.Height];

            renderTarget.GetData<Microsoft.Xna.Framework.Graphics.PackedVector.Byte4>(Data);

            byte[] byteArray = new Byte[Data.Length * 4];
            int iByte = 0;
            for (int iData = 0; iData < Data.Length; iData++, iByte += 4)
            {
                byteArray[iByte] = (Byte)(Data[iData].PackedValue >> 16);
                byteArray[iByte + 1] = (Byte)(Data[iData].PackedValue >> 8);
                byteArray[iByte + 2] = (Byte)(Data[iData].PackedValue >> 0);
                byteArray[iByte + 3] = (Byte)(Data[iData].PackedValue >> 24);
            }

            return byteArray;
        }
    }

}


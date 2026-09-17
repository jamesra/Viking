using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Links two poly lines by drawing a lines between the first pair and last pair of verticies of each polyline.
    /// </summary>
    public class LinkedPolyLineSimpleView : IColorView
    {
        Geometry.Vector2[] Source;
        Geometry.Vector2[] Target;
        public LineView[] Lines;

        public LineStyle Style
        {
            get { return Lines.First().Style; }
            set { foreach (LineView l in Lines) { l.Style = value; } }
        }


        public float LineWidth
        {
            get { return Lines.First().LineWidth; }
            set { foreach (LineView l in Lines) { l.LineWidth = value; } }
        }

        public Microsoft.Xna.Framework.Color Color
        {
            get { return Lines.First().Color; }
            set { foreach (LineView l in Lines) { l.Color = value; } }
        }

        public float Alpha
        {
            get { return Color.GetAlpha(); }
            set { Color = Color.SetAlpha(value); }
        }

        public LinkedPolyLineSimpleView(Geometry.Vector2[] source, Geometry.Vector2[] target, float linewidth, Color color, LineStyle style)
        {
            Lines = CreateViewData(source, target, linewidth, color, style);
            Source = source;
            Target = target; 
        }

        protected static bool SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(Geometry.Vector2[] source, Geometry.Vector2[] target)
        {
            try
            {
                LineSegment LineA = new LineSegment(source[0], target[0]);
                LineSegment LineB = new LineSegment(source.Last(), target.Last());
                Geometry.Vector2 intersectionPoint;
                return !LineA.Intersects(LineB, out intersectionPoint);
            }
            catch(ArgumentException e)
            {
                //This occurs when the source and target points are identical
                return false; 
            }
        }

        protected static LineView[] CreateViewData(Geometry.Vector2[] source, Geometry.Vector2[] target, double linewidth, Color color, LineStyle style)
        {
            //Figure out which orientation the lines have to each other
            if (!SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(source, target))
                target = target.Reverse().ToArray();

            //Draw triangles from each vertex on source to each vertex on target
            List<LineView> listLines = new List<LineView>(2);
            LineView lineA = new LineView(source.First(), target.First(), linewidth, color, style);
            LineView lineB = new LineView(source.Last(), target.Last(), linewidth, color, style);

            listLines.Add(lineA);
            listLines.Add(lineB);

            return listLines.ToArray();
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          LinkedPolyLineSimpleView[] listToDraw)
        {
            LineView[] lineToDraw = listToDraw.SelectMany(l => l.Lines).ToArray();
            LineView.Draw(device, scene, lineManager, lineToDraw);
        }
    }

    /// <summary>
    /// Given two poly lines that do not cross this function creates a polygon that links them.
    /// </summary>
    class LinkedPolyLineView
    {
        Geometry.Vector2[] Source;
        Geometry.Vector2[] Target;

        public LinkedPolyLineView(Geometry.Vector2[] source, Geometry.Vector2[] target)
        {
            Source = source;
            Target = target;
        }

        protected static bool SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(Geometry.Vector2[] source, Geometry.Vector2[] target)
        {
            LineSegment LineA = new LineSegment(source[0], target[0]);
            LineSegment LineB = new LineSegment(source.Last(), target.Last());
            Geometry.Vector2 intersectionPoint;
            return !LineA.Intersects(LineB, out intersectionPoint);
        }

        protected static void CreatePolygons(Geometry.Vector2[] source, Geometry.Vector2[] target)
        {
            //Figure out which orientation the lines have to each other
            if (!SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(source, target))
                target = target.Reverse().ToArray();

            //Draw triangles from each vertex on source to each vertex on target
            List<int> indicies = new List<int>(source.Length * 3);
            List<Geometry.Vector2> verticies = new List<Geometry.Vector2>((source.Length + target.Length) * 3);
            int iTarget = 0;
            for(int iSource = 0; iSource < source.Length; iSource++)
            {
                
            }
        }
    }
}

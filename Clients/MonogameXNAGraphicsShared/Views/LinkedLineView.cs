﻿using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Links two poly lines by drawing a lines between the first pair and last pair of verticies of each polyline.
    /// </summary>
    public class LinkedPolyLineSimpleView : IColorView
    {
        readonly GridVector2[] Source;
        readonly GridVector2[] Target;
        public readonly LineView[] Lines;

        public LineStyle Style
        {
            get => Lines.First().Style;
            set { foreach (LineView l in Lines) { l.Style = value; } }
        }


        public float LineWidth
        {
            get => Lines.First().LineWidth;
            set { foreach (LineView l in Lines) { l.LineWidth = value; } }
        }

        public Microsoft.Xna.Framework.Color Color
        {
            get => Lines.First().Color;
            set { foreach (LineView l in Lines) { l.Color = value; } }
        }

        public float Alpha
        {
            get => Color.GetAlpha();
            set => Color = Color.SetAlpha(value);
        }

        public LinkedPolyLineSimpleView(GridVector2[] source, GridVector2[] target, float linewidth, Color color, LineStyle style)
        {
            Lines = CreateViewData(source, target, linewidth, color, style);
            Source = source;
            Target = target; 
        }

        protected static bool SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(GridVector2[] source, GridVector2[] target)
        {
            try
            {
                GridLineSegment LineA = new GridLineSegment(source[0], target[0]);
                GridLineSegment LineB = new GridLineSegment(source.Last(), target.Last());
                GridVector2 intersectionPoint;
                return !LineA.Intersects(LineB, out intersectionPoint);
            }
            catch(ArgumentException)
            {
                //This occurs when the source and target points are identical
                return false; 
            }
        }

        protected static LineView[] CreateViewData(GridVector2[] source, GridVector2[] target, double linewidth, Color color, LineStyle style)
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
        readonly GridVector2[] Source;
        readonly GridVector2[] Target;

        public LinkedPolyLineView(GridVector2[] source, GridVector2[] target)
        {
            Source = source;
            Target = target;
        }

        protected static bool SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(GridVector2[] source, GridVector2[] target)
        {
            GridLineSegment LineA = new GridLineSegment(source[0], target[0]);
            GridLineSegment LineB = new GridLineSegment(source.Last(), target.Last());
            GridVector2 intersectionPoint;
            return !LineA.Intersects(LineB, out intersectionPoint);
        }

        protected static void CreatePolygons(GridVector2[] source, GridVector2[] target)
        {
            //Figure out which orientation the lines have to each other
            if (!SourceAndTargetLinesCanBothUseAscendingIndexWithoutCrossingLines(source, target))
                target = target.Reverse().ToArray();

            //Draw triangles from each vertex on source to each vertex on target
            List<int> indicies = new List<int>(source.Length * 3);
            List<GridVector2> verticies = new List<GridVector2>((source.Length + target.Length) * 3);
            //int iTarget = 0;
            /*
            for(int iSource = 0; iSource < source.Length; iSource++)
            {
                
            }*/
        }
    }
}

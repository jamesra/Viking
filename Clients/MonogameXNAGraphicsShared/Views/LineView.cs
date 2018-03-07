using System;
using System.Linq;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using VikingXNAGraphics;

namespace VikingXNAGraphics
{
    public class LineView : IColorView, IViewPosition2D
    {
        public static double time = 0;
        protected RoundLineCode.RoundLine line;
        public LineStyle Style;

        public bool UseHSLColor = false; 

        public GridVector2 Source
        {
            get { return line.P0.ToGridVector2(); }
            set { line.P0 = value.ToXNAVector2(); }
        }

        public GridVector2 Destination
        {
            get { return line.P1.ToGridVector2(); }
            set { line.P1 = value.ToXNAVector2(); }
        }

        public float LineWidth;

        protected Microsoft.Xna.Framework.Color _Color;
        public Microsoft.Xna.Framework.Color Color
        {
            get { return _Color; }
            set { _Color = value; _HSLColor = value.ConvertToHSL(); }
        }

        public float Alpha
        {
            get { return _Color.GetAlpha(); }
            set { _Color = _Color.SetAlpha(value); }
        }

        /// <summary>
        /// Returns Center of lineView
        /// </summary>
        public GridVector2 Position
        {
            get
            {
                Microsoft.Xna.Framework.Vector2 v = line.P0 + line.P1;
                return new GridVector2(v.X / 2.0, v.Y / 2.0);
            }

            set
            {
                GridVector2 offset = value - Position;
                line.P0 += offset.ToXNAVector2();
                line.P1 += offset.ToXNAVector2();
            }
        }

        protected Microsoft.Xna.Framework.Color _HSLColor;

        public LineView(GridVector2 source, GridVector2 destination, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle, bool UseHSLColor)
        {
            line = new RoundLineCode.RoundLine(source.ToXNAVector2(), destination.ToXNAVector2());
            this.LineWidth = (float)width;
            this.Color = color; 
            this.Style = lineStyle;
            this.UseHSLColor = UseHSLColor;
        }

        public LineView(GridLineSegment line, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle, bool UseHSLColor) : this(line.A, line.B, width, color, lineStyle, UseHSLColor)
        {
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          LineView[] listToDraw)
        {
            var techniqueGroups = listToDraw.GroupBy(l => l.Style);
            foreach (var group in techniqueGroups)
            {
                lineManager.Draw(group.Select(l => l.line).ToArray(),
                             group.Select(l => l.LineWidth / 2.0f).ToArray(),
                             group.Select(l => l.UseHSLColor ? l._HSLColor : l.Color).ToArray(),
                             scene.Camera.View * scene.Projection,
                             (float)(DateTime.UtcNow.Millisecond / 1000.0),
                             group.Key.ToString());
            }
        }
    }
}
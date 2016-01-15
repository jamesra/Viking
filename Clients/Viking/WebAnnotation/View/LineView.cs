using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Geometry;
using WebAnnotation.ViewModel;
using Viking.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;

namespace WebAnnotation.View
{
    class LineView
    {
        public static double time = 0;
        RoundLineCode.RoundLine line;
        public LineStyle Style; 

        public GridVector2 Source
        {
            get { return line.P0.ToGridVector(); }
            set { line.P0 = value.ToVector2(); }
        }

        public GridVector2 Destination
        {
            get { return line.P1.ToGridVector(); }
            set { line.P1 = value.ToVector2(); }
        }

        public float LineWidth;

        private Microsoft.Xna.Framework.Color _Color;
        public Microsoft.Xna.Framework.Color Color
        {
            get { return _Color; }
            set { _Color = value; _HSLColor = value.ConvertToHSL(); }
        }
        private Microsoft.Xna.Framework.Color _HSLColor;

        public LineView(GridVector2 source, GridVector2 destination, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle)
        {
            line = new RoundLineCode.RoundLine(source.ToVector2(), destination.ToVector2());
            this.LineWidth = (float)width;
            this.Color = color; 
            this.Style = lineStyle;
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
                             group.Select(l => l._HSLColor).ToArray(),
                             scene.Camera.View * scene.Projection,
                             (float)(DateTime.UtcNow.Millisecond / 1000.0),
                             group.Key.ToString());
            }
        }
    }
}
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
        public string TechniqueName; 

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

        public Microsoft.Xna.Framework.Color Color;

        public LineView(GridVector2 source, GridVector2 destination, double width, Microsoft.Xna.Framework.Color color, string techniqueName)
        {
            line = new RoundLineCode.RoundLine(source.ToVector2(), destination.ToVector2());
            this.LineWidth = (float)width;
            this.Color = color;
            this.TechniqueName = techniqueName;
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          LineView[] listToDraw)
        {
            var techniqueGroups = listToDraw.GroupBy(l => l.TechniqueName);
            foreach (var group in techniqueGroups)
            {
                lineManager.Draw(group.Select(l => l.line).ToArray(),
                             group.Select(l => l.LineWidth).ToArray(),
                             group.Select(l => l.Color).ToArray(),
                             scene.Camera.View * scene.Projection,
                             (float)(DateTime.UtcNow.Millisecond / 1000.0),
                             group.Key);
            }
        }
    }
}
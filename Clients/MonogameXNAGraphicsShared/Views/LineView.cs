using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using VikingXNA;

namespace VikingXNAGraphics
{
    public class LineView : IColorView, IViewPosition2D, IRenderable
    {
        public static double time = 0;
        internal RoundLineCode.RoundLine line;
        public LineStyle Style;

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


        private float? _DashLength;
        public float? DashLength
        {
            get
            {
                //Only return a DashLength for Styles that use it
                switch (this.Style)
                {
                    case LineStyle.Ladder:
                    case LineStyle.Dashed:
                        return _DashLength;
                    default:
                        return new float?();
                }
            }
            set
            {
                if (value != _DashLength)
                {
                    _DashLength = value;
                }
            }
        }

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

        public LineView(GridVector2 source, GridVector2 destination, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle)
        {
            line = new RoundLineCode.RoundLine(source.ToXNAVector2(), destination.ToXNAVector2());
            this.LineWidth = (float)width;
            this.Color = color; 
            this.Style = lineStyle;
        }

        public LineView(GridLineSegment line, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle) : this(line.A, line.B, width, color, lineStyle)
        {
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          LineView[] listToDraw)
        {
            bool UseHSLColor = lineManager.UseHSLColor;

            var renderGroups = listToDraw.GroupBy(l => new { color = UseHSLColor ? l._HSLColor : l.Color, style = l.Style, width = l.LineWidth, dashLength = l.DashLength });

            foreach(var renderGroup in renderGroups)
            {
                if(renderGroup.Key.dashLength.HasValue)
                {
                    lineManager.DashLength = renderGroup.Key.dashLength.Value;
                }

                lineManager.Draw(renderGroup.Select(rg => rg.line).ToArray(),
                                 renderGroup.Key.width / 2.0f,
                                 renderGroup.Key.color,
                                 scene.ViewProj,
                                 (float)(DateTime.UtcNow.Millisecond / 1000.0),
                                 renderGroup.Key.style.ToString());
            }
        }

        public static void Draw(GraphicsDevice device, IScene scene, OverlayStyle overlay, LineView[] items)
        {
            RoundLineCode.RoundLineManager line_manager = overlay.GetLineManager(device);
            bool UseHSLColor = line_manager.UseHSLColor;

            var listToDraw = items.Select(i => i as LineView).Where(i => i != null).ToArray();

            var renderGroups = listToDraw.GroupBy(l => new { color = UseHSLColor ? l._HSLColor : l.Color, style = l.Style, width = l.LineWidth, dashLength = l.DashLength });

            foreach (var renderGroup in renderGroups)
            {
                if (renderGroup.Key.dashLength.HasValue)
                {
                    line_manager.DashLength = renderGroup.Key.dashLength.Value;
                }

                line_manager.Draw(renderGroup.Select(rg => rg.line).ToArray(),
                                 renderGroup.Key.width / 2.0f,
                                 renderGroup.Key.color,
                                 scene.ViewProj,
                                 (float)(DateTime.UtcNow.Millisecond / 1000.0),
                                 renderGroup.Key.style.ToString());
            }
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle overlay, IRenderable[] items)
        {
            Draw(device, scene, overlay, items.Select(i => i as LineView).Where(i => i != null).ToArray());
        }

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle overlay)
        {
            RoundLineCode.RoundLineManager line_manager = overlay.GetLineManager(device);
            bool UseHSLColor = line_manager.UseHSLColor;
            var color = UseHSLColor ? this._HSLColor : this.Color;
            if(this.DashLength.HasValue)
            {
                line_manager.DashLength = this.DashLength.Value;
            }

            line_manager.Draw(this.line, 
                              this.LineWidth / 2.0f, 
                              color, 
                              scene.ViewProj,
                              (float)(DateTime.UtcNow.Millisecond / 1000.0),
                              this.Style.ToString());
        }
    }
}
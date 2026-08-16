using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using VikingXNA;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace VikingXNAGraphics
{
    public class LineView : IColorView, IViewPosition2D, IRenderable
    {
        public static double time = 0;
        internal RoundLineCode.RoundLine line;
        public LineStyle Style;

        public Geometry.Vector2 Source
        {
            get => line.P0.ToVector2();
            set => line.P0 = value.ToXNAVector2();
        }

        public Geometry.Vector2 Destination
        {
            get => line.P1.ToVector2();
            set => line.P1 = value.ToXNAVector2();
        }

        public float LineWidth;


        private float? _DashLength;
        public float? DashLength
        {
            get
            {
                //Only return a DashLength for Styles that use it
                return this.Style switch
                {
                    LineStyle.Ladder or LineStyle.Dashed => _DashLength,
                    _ => new float?(),
                };
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
            get => _Color;
            set { _Color = value; _HSLColor = value.ConvertToHCL(); }
        }

        public float Alpha
        {
            get => _Color.GetAlpha();
            set => _Color = _Color.SetAlpha(value);
        }

        /// <summary>
        /// Returns Center of lineView
        /// </summary>
        public Geometry.Vector2 Position
        {
            get
            {
                Microsoft.Xna.Framework.Vector2 v = line.P0 + line.P1;
                return new Geometry.Vector2(v.X / 2.0, v.Y / 2.0);
            }

            set
            {
                Geometry.Vector2 offset = value - Position;
                line.P0 += offset.ToXNAVector2();
                line.P1 += offset.ToXNAVector2();
            }
        }

        protected Microsoft.Xna.Framework.Color _HSLColor;

        public LineView(Geometry.Vector2 source, Geometry.Vector2 destination, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle)
        {
            line = new RoundLineCode.RoundLine(source.ToXNAVector2(), destination.ToXNAVector2());
            this.LineWidth = (float)width;
            this.Color = color;
            this.Style = lineStyle;
        }

        public LineView(LineSegment line, double width, Microsoft.Xna.Framework.Color color, LineStyle lineStyle) : this(line.A, line.B, width, color, lineStyle)
        {
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.IScene scene,
                          RoundLineCode.RoundLineManager lineManager,
                          LineView[] listToDraw)
        {
            bool UseHSLColor = lineManager.UseHSLColor;

            var renderGroups = listToDraw.GroupBy(l => new { color = UseHSLColor ? l._HSLColor : l.Color, style = l.Style, width = l.LineWidth, dashLength = l.DashLength });

            foreach (var renderGroup in renderGroups)
            {
                if (renderGroup.Key.dashLength.HasValue)
                {
                    lineManager.DashLength = renderGroup.Key.dashLength.Value;
                }

                lineManager.Draw([.. renderGroup.Select(rg => rg.line)],
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

                line_manager.Draw([.. renderGroup.Select(rg => rg.line)],
                                 renderGroup.Key.width / 2.0f,
                                 renderGroup.Key.color,
                                 scene.ViewProj,
                                 (float)(DateTime.UtcNow.Millisecond / 1000.0),
                                 renderGroup.Key.style.ToString());
            }
        }

        public void DrawBatch(GraphicsDevice device, IScene scene, OverlayStyle overlay, IRenderable[] items) => Draw(device, scene, overlay, [.. items.Select(i => i as LineView).Where(i => i != null)]);

        public void Draw(GraphicsDevice device, IScene scene, OverlayStyle overlay)
        {
            RoundLineCode.RoundLineManager line_manager = overlay.GetLineManager(device);
            bool UseHSLColor = line_manager.UseHSLColor;
            var color = UseHSLColor ? this._HSLColor : this.Color;
            if (this.DashLength.HasValue)
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
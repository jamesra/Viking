#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Controls;
using VikingXNAGraphics;
using WebAnnotation.UI.Commands;
using GeometryCircle = Geometry.Circle;
using GeometryVector2 = Geometry.Vector2;

namespace WebAnnotation.UI.AutoPolygonize
{
    /// <summary>
    /// Draws a proposal's accept button (a green circle with a white check) and hide button (a red rounded square with a
    /// black X). The shapes and symbols differ as well as the colours, so they can be told apart without colour vision.
    /// Both are saturated, a little transparent and edged with a thin contrasting outline. Call from the render thread.
    /// </summary>
    internal sealed class ProposalButtonsView(SectionViewerControl parent)
    {
        internal static readonly Color AcceptColor = new(20, 170, 60);
        internal static readonly Color HideColor = new(225, 35, 35);

        /// <summary>How much darker a pressed button is drawn.</summary>
        private const float PressedShade = 0.65f;

        /// <summary>How far a hovered button's fill moves towards white.</summary>
        private const float HoverTint = 0.25f;

        /// <summary>A pressed button shrinks to this fraction of its radius, so it looks pushed in.</summary>
        private const double PressedScale = 0.92;

        /// <summary>Half the side of the hide square, as a fraction of the button radius, so its area is close to the circle's.</summary>
        private const double SquareHalfSide = 0.88;

        /// <summary>Corner radius of the hide square, as a fraction of its half side.</summary>
        private const double SquareCornerFraction = 0.35;

        private const int SquareCornerSegments = 6;

        /// <summary>Width of the check and X strokes, as a fraction of the button radius.</summary>
        private const double GlyphStrokeFraction = 0.16;

        /// <summary>Height of the "?", as a fraction of the button radius.</summary>
        private const double QuestionMarkFontFraction = 1.4;

        /// <summary>Thinnest outline of the hide square, in screen pixels.</summary>
        private const double MinOutlinePixels = 1.5;

        private readonly SectionViewerControl parent = parent ?? throw new ArgumentNullException(nameof(parent));

        /// <summary>Unit rounded-square fill meshes by colour; placed with their model matrix, so they are built once.</summary>
        private readonly Dictionary<Color, PositionColorMeshModel> squareMeshes = [];

        private LabelView? questionMark;

        /// <summary>The outline of a rounded square of half side 1 centred on the origin, counter-clockwise.</summary>
        internal static IReadOnlyList<GeometryVector2> UnitRoundedSquare { get; } = BuildUnitRoundedSquare();

        /// <summary>
        /// Draws both buttons of <paramref name="placement"/>. <paramref name="phaseOf"/> gives each button's phase,
        /// <paramref name="hovered"/> the one under the cursor, and <paramref name="alpha"/> the overall opacity.
        /// </summary>
        public void Draw(
            GraphicsDevice graphicsDevice,
            VikingXNA.Scene scene,
            ProposalButtonPlacement placement,
            Func<ProposalButtonKind, ProposalButtonPhase> phaseOf,
            ProposalButtonKind? hovered,
            float alpha)
        {
            if (alpha <= 0f || placement.Radius <= 0)
                return;

            foreach (ProposalButtonKind kind in new[] { ProposalButtonKind.Accept, ProposalButtonKind.Hide })
            {
                ProposalButtonPhase phase = phaseOf(kind);
                bool isHovered = hovered == kind;
                double radius = placement.Radius * (phase == ProposalButtonPhase.Pressed ? PressedScale : 1);
                Color fill = FillColor(kind, phase, isHovered);
                Color outline = isHovered ? Color.White : Color.Black;
                GeometryVector2 center = placement.CenterOf(kind);

                if (kind == ProposalButtonKind.Accept)
                    DrawDisc(graphicsDevice, scene, center, radius, fill, outline, alpha);
                else
                    DrawRoundedSquare(graphicsDevice, scene, center, radius, fill, outline, alpha);

                switch (phase)
                {
                    case ProposalButtonPhase.Idle:
                        DrawSymbol(graphicsDevice, scene, kind, center, radius, alpha);
                        break;
                    case ProposalButtonPhase.Armed:
                        DrawQuestionMark(scene, center, radius, PenButtonStyle.LabelColorFor(fill), alpha);
                        break;
                }
            }
        }

        /// <summary>The fill of <paramref name="kind"/> in <paramref name="phase"/>: darker when pressed, lighter when hovered.</summary>
        internal static Color FillColor(ProposalButtonKind kind, ProposalButtonPhase phase, bool hovered)
        {
            Color color = kind == ProposalButtonKind.Accept ? AcceptColor : HideColor;
            if (phase == ProposalButtonPhase.Pressed)
                return new Color((int)(color.R * PressedShade), (int)(color.G * PressedShade), (int)(color.B * PressedShade));
            if (hovered)
                return Color.Lerp(color, Color.White, HoverTint);
            return color;
        }

        private static void DrawDisc(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, GeometryVector2 center, double radius, Color fill, Color outline, float alpha)
        {
            GeometryCircle disc = new(center, radius);
            RingCircleView ring = PenButtonStyle.RingFor(disc, outline);
            ring.Alpha = alpha;
            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, [new CircleView(disc, fill) { Alpha = alpha }, ring]);
        }

        private void DrawRoundedSquare(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, GeometryVector2 center, double radius, Color fill, Color outline, float alpha)
        {
            double halfSide = radius * SquareHalfSide;
            if (!squareMeshes.TryGetValue(fill, out PositionColorMeshModel? mesh))
            {
                mesh = BuildUnitSquareMesh(fill);
                squareMeshes[fill] = mesh;
            }

            mesh.Alpha = alpha;
            mesh.ModelMatrix = Matrix.CreateScale((float)halfSide, (float)halfSide, 1) *
                               Matrix.CreateTranslation((float)center.X, (float)center.Y, 0);
            MeshView<VertexPositionColor>.Draw(graphicsDevice, scene, (BasicEffect?)null, CullMode.None, FillMode.Solid, meshmodels: [mesh]);

            List<GeometryVector2> ring = new(UnitRoundedSquare.Count + 1);
            foreach (GeometryVector2 p in UnitRoundedSquare)
                ring.Add(new GeometryVector2(center.X + p.X * halfSide, center.Y + p.Y * halfSide));
            ring.Add(ring[0]);

            double lineWidth = Math.Max(MinOutlinePixels * scene.Camera.Downsample, radius * PenButtonStyle.RingThickness * 2);
            PolyLineView line = new(ring, outline, null, lineWidth) { Alpha = alpha };
            line.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
        }

        private static void DrawSymbol(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, ProposalButtonKind kind, GeometryVector2 center, double radius, float alpha)
        {
            double width = radius * GlyphStrokeFraction;
            Color color = kind == ProposalButtonKind.Accept ? Color.White : Color.Black;
            foreach (GeometryVector2[] stroke in Symbol(kind))
            {
                GeometryVector2[] points = new GeometryVector2[stroke.Length];
                for (int i = 0; i < stroke.Length; i++)
                    points[i] = new GeometryVector2(center.X + stroke[i].X * radius, center.Y + stroke[i].Y * radius);

                PolyLineView line = new(points, color, null, width) { Alpha = alpha };
                line.Draw(graphicsDevice, scene, OverlayStyle.Alpha);
            }
        }

        /// <summary>
        /// The strokes of a button's symbol as offsets from its centre in button radii, world y up: the check follows the
        /// segmentation pen buttons' check, the X spans most of the square.
        /// </summary>
        internal static GeometryVector2[][] Symbol(ProposalButtonKind kind) => kind == ProposalButtonKind.Accept
            ? [[new(-0.43, 0), new(-0.13, -0.33), new(0.43, 0.37)]]
            : [[new(-0.42, -0.42), new(0.42, 0.42)], [new(-0.42, 0.42), new(0.42, -0.42)]];

        private void DrawQuestionMark(VikingXNA.Scene scene, GeometryVector2 center, double radius, Color color, float alpha)
        {
            if (parent.spriteBatch is null || parent.fontArial is null)
                return;

            questionMark ??= new LabelView("?", GeometryVector2.Zero, color, Alignment.CenterCenter, Anchor.CenterCenter, scaleFontWithScene: true);
            questionMark.Position = center;
            questionMark.Color = color;
            questionMark.Alpha = alpha;
            questionMark.FontSize = radius * QuestionMarkFontFraction;
            LabelView.Draw(parent.spriteBatch, parent.fontArial, scene, [questionMark]);
        }

        private static PositionColorMeshModel BuildUnitSquareMesh(Color color)
        {
            IReadOnlyList<GeometryVector2> outline = UnitRoundedSquare;
            VertexPositionColor[] vertices = new VertexPositionColor[outline.Count + 1];
            vertices[0] = new VertexPositionColor(Vector3.Zero, color);
            for (int i = 0; i < outline.Count; i++)
                vertices[i + 1] = new VertexPositionColor(new Vector3((float)outline[i].X, (float)outline[i].Y, 0), color);

            int[] indices = new int[outline.Count * 3];
            for (int i = 0; i < outline.Count; i++)
            {
                indices[i * 3] = 0;
                indices[i * 3 + 1] = i + 1;
                indices[i * 3 + 2] = (i + 1) % outline.Count + 1;
            }

            return new PositionColorMeshModel { Verticies = vertices, Edges = indices };
        }

        private static IReadOnlyList<GeometryVector2> BuildUnitRoundedSquare()
        {
            double corner = SquareCornerFraction;
            double inner = 1 - corner;
            (double X, double Y, double StartAngle)[] corners =
            [
                (inner, -inner, -Math.PI / 2),
                (inner, inner, 0),
                (-inner, inner, Math.PI / 2),
                (-inner, -inner, Math.PI),
            ];

            List<GeometryVector2> points = new(corners.Length * (SquareCornerSegments + 1));
            foreach ((double cx, double cy, double start) in corners)
            {
                for (int i = 0; i <= SquareCornerSegments; i++)
                {
                    double angle = start + (Math.PI / 2) * i / SquareCornerSegments;
                    points.Add(new GeometryVector2(cx + corner * Math.Cos(angle), cy + corner * Math.Sin(angle)));
                }
            }

            return points;
        }
    }
}

using Geometry;
using Viking.Input;
using Rectangle = Geometry.Rectangle;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework.Graphics;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a location on an adjacent section that is overlapped by an annotation on the visible section.
    /// </summary>
    public class OverlappedLocationView : LocationCanvasView, IColorView, ILabelView, IViewLocation
    {
        public TextureCircleView circleView;
        public LabelView label;

        public override SqlGeometry VolumeShapeAsRendered => Circle.ToSqlGeometry(Z);

        public Circle Circle
        {
            get => circleView.Circle;
            set => circleView.Circle = value;
        }

        public double Radius
        {
            get => Circle.Radius;
            set => circleView.Circle = new Circle(Circle.Center, value);
        }

        public Geometry.Vector2 Position
        {
            get => Circle.Center;
            set => circleView.Circle = new Circle(value, Circle.Radius);
        }

        private readonly ICollection<long> _OverlappedLinks;
        public override ICollection<long> OverlappedLinks
        {
            protected get => _OverlappedLinks;

            set => throw new NotImplementedException();
        }

        public OverlappedLocationView(LocationObj obj, Circle gridCircle, bool Up) : base(obj)
        {
            label = new LabelView(LocationLabel(obj), gridCircle.Center)
            {
                _Color = Microsoft.Xna.Framework.Color.Red
            };
            Microsoft.Xna.Framework.Color color = obj.Parent.Type.Color.ToXNAColor(0.75f);
            circleView = Up ? TextureCircleView.CreateUpArrow(gridCircle, color) : TextureCircleView.CreateDownArrow(gridCircle, color);
        }

        private static string LocationLabel(LocationObj obj) => obj.Z.ToString();

        public override bool IsVisible(VikingXNA.Scene scene) => circleView.IsVisible(scene);

        public bool IsLabelVisible(VikingXNA.Scene scene) => label.IsVisible(scene);

        public override bool Contains(Geometry.Vector2 Position) => Circle.Covers(Position);

        public override bool Intersects(LineSegment line) => Circle.Intersects(line);

        public override bool Intersects(SqlGeometry shape) => throw new NotImplementedException();

        public override double Distance(Geometry.Vector2 Position)
        {
            double Distance = Geometry.Vector2.Distance(Position, Circle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }

        public override double DistanceFromCenterNormalized(Geometry.Vector2 Position) => Geometry.Vector2.Distance(Position, Circle.Center) / Radius;

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          OverlappedLocationView[] listToDraw)
        {
            TextureCircleView[] backgroundCircles = [.. listToDraw.Select(l => l.circleView)];
            TextureCircleView.Draw(device, scene, OverlayStyle.Luma, [.. backgroundCircles]);
        }

        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene)
        {
            double DesiredRowsOfText = 4.0;
            double DefaultFontSize = (Radius * 2) / DesiredRowsOfText;
            label.FontSize = DefaultFontSize;
            label.MaxLineWidth = Radius * 2;

            label.Draw(spriteBatch, font, scene);
        }

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            if (modifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.NONE;
            }

            return LocationAction.CREATELINKEDLOCATION;
        }

        public override LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = ID;

            return LocationAction.NONE;
        }

        public override List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => throw new NotImplementedException();//return LocationAction.CREATELINKEDLOCATION;

        public override string[] HelpStrings => [
                    "Hold left click + drag on inscribed arrow: Create additional annotation for this structure linked to the annotation on the adjacent section."
                ];

#if NETFRAMEWORK
        public new ContextMenuStrip ContextMenu => new Location_CanvasContextMenuView(ID).ContextMenu;
#endif

        public override Rectangle BoundingBox => Circle.BoundingBox;

        public Microsoft.Xna.Framework.Color Color
        {
            get => circleView.Color;

            set => circleView.Color = value;
        }

        public float Alpha
        {
            get => circleView.Alpha;

            set => circleView.Alpha = value;
        }
    }
}

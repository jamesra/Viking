using Geometry;
using Viking.Input;
using Rectangle = Geometry.Rectangle;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif
using Viking.AnnotationServiceTypes;
using VikingXNA;
using VikingXNAGraphics;
using WebAnnotation.UI;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    /// <summary>
    /// Renders arrows for location links that are overlapped by an annotation on the section
    /// </summary>
    internal class OverlappedLocationLinkView : ICanvasGeometryView, IColorView, ILabelView,
                                       IMouseActionSupport, IPenActionSupport, IViewLocationLink, IViewLocation, Viking.Common.IHelpStrings
    {
        private readonly TextureCircleView circleView;
        private readonly LabelView label;
        private readonly LocationLinkKey linkKey;

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

        public Rectangle BoundingBox => Circle.BoundingBox;


        public bool IsVisible(Scene scene) => circleView.IsVisible(scene);

        public bool Contains(Geometry.Vector2 Position) => Circle.Covers(Position);

        public bool Intersects(LineSegment line) => Circle.Intersects(line);

        public double Distance(Geometry.Vector2 Position)
        {
            double Distance = Geometry.Vector2.Distance(Position, Circle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }

        public double Distance(SqlGeometry Position) => throw new NotImplementedException();

        public double DistanceFromCenterNormalized(Geometry.Vector2 Position) => Geometry.Vector2.Distance(Position, Circle.Center) / Radius;

        public long LocationID
        {
            get;
            set;
        }

        public long OffSectionLocationID => linkKey.A == LocationID ? linkKey.B : linkKey.A;

        LocationLinkKey IViewLocationLink.Key => linkKey;

        /// <summary>
        /// Return the ID of the location we are representing with the view.
        /// </summary>
        long IViewLocation.ID => OffSectionLocationID;

        public OverlappedLocationLinkView(long locationID, LocationObj linkedObj, Circle gridCircle, bool Up)
        {
            LocationID = locationID;
            linkKey = new LocationLinkKey(locationID, linkedObj.ID);
            label = new LabelView(((int)linkedObj.Z).ToString(), gridCircle.Center)
            {
                _Color = Microsoft.Xna.Framework.Color.Red
            };
            Microsoft.Xna.Framework.Color color = linkedObj.Parent.Type.Color.ToXNAColor(0.75f);
            circleView = Up ? TextureCircleView.CreateUpArrow(gridCircle, color) : TextureCircleView.CreateDownArrow(gridCircle, color);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          OverlayShaderEffect overlayEffect,
                          OverlappedLocationLinkView[] listToDraw)
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

        public bool IsLabelVisible(Scene scene) => label.IsVisible(scene);

        public LocationAction GetPenContactActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = OffSectionLocationID;
            return LocationAction.NONE;
        }

        public LocationAction GetMouseClickActionForPositionOnAnnotation(Geometry.Vector2 WorldPosition, int VisibleSectionNumber, Viking.Input.ModifierKeys modifierKeys, out long LocationID)
        {
            LocationID = OffSectionLocationID;
            if (modifierKeys.ShiftOrCtrlPressed())
            {
                return LocationAction.NONE;
            }

            return LocationAction.CREATELINKEDLOCATION;
        }

        public List<IAction> GetPenActionsForShapeAnnotation(Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber) => [];

        public string[] HelpStrings => [
                    "Hold left click + drag: Create additional annotation for this structure linked to the annotation on the adjacent section."
                ];

        int ICanvasView.VisualHeight => 0;
    }
}

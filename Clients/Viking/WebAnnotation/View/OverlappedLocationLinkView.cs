using System;
using System.Collections.Generic;
using System.Linq;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotation;
using WebAnnotationModel;
using System.Windows.Forms;
using WebAnnotation.ViewModel;
using Microsoft.SqlServer.Types;
using VikingXNAGraphics;
using SqlGeometryUtils;
using VikingXNA;

namespace WebAnnotation.View
{
    /// <summary>
    /// Renders arrows for location links that are overlapped by an annotation on the section
    /// </summary>
    class OverlappedLocationLinkView : ICanvasGeometryView, IColorView, ILabelView, Viking.Common.IContextMenu, IMouseActionSupport, IViewLocationLink, IViewLocation, Viking.Common.IHelpStrings
    {
        public TextureCircleView circleView;
        public LabelView label;

        readonly LocationLinkKey linkKey;

        public GridCircle Circle
        {
            get { return circleView.Circle; }
            set { circleView.Circle = value; }
        }

        public double Radius
        {
            get { return Circle.Radius; }
            set { circleView.Circle = new GridCircle(Circle.Center, value); }
        }

        public GridVector2 Position
        {
            get { return Circle.Center; }
            set { circleView.Circle = new GridCircle(value, Circle.Radius); }
        }

        public Microsoft.Xna.Framework.Color Color
        {
            get
            {
                return circleView.Color;
            }

            set
            {
                circleView.Color = value;
            }
        }

        public float Alpha
        {
            get
            {
                return circleView.Alpha;
            }

            set
            {
                circleView.Alpha = value;
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return Circle.BoundingBox;
            }
        }

        public ContextMenu ContextMenu
        {
            get
            {
                LocationLink_CanvasContextMenuView contextMenuView = new LocationLink_CanvasContextMenuView(this.linkKey);
                return contextMenuView.ContextMenu;
            }
        }

        public bool IsVisible(Scene scene)
        {
            return this.circleView.IsVisible(scene);
        }

        public bool Contains(GridVector2 Position)
        {
            return Circle.Contains(Position);
        }

        public bool Intersects(GridLineSegment line)
        {
            return Circle.Intersects(line);
        }

        public double Distance(GridVector2 Position)
        {
            double Distance = GridVector2.Distance(Position, this.Circle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }

        public double Distance(SqlGeometry Position)
        {
            throw new NotImplementedException();
        }

        public double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.Circle.Center) / this.Radius;
        }

        public long LocationID
        {
            get;
            set;
        }

        public long OffSectionLocationID
        {
            get { return linkKey.A == this.LocationID ? linkKey.B : linkKey.A; }
        }

        LocationLinkKey IViewLocationLink.Key
        {
            get
            {
                return this.linkKey;
            }
        }

        /// <summary>
        /// Return the ID of the location we are representing with the view.
        /// </summary>
        long IViewLocation.ID
        {
            get
            {
                return this.OffSectionLocationID;
            }
        }

        public OverlappedLocationLinkView(long locationID, LocationObj linkedObj, GridCircle gridCircle, bool Up)
        {
            this.LocationID = locationID;
            this.linkKey = new LocationLinkKey(locationID, linkedObj.ID);
            label = new LabelView(((int)linkedObj.Z).ToString(), gridCircle.Center);
            label._Color = Microsoft.Xna.Framework.Color.Red;
            Microsoft.Xna.Framework.Color color = linkedObj.Parent.Type.Color.ToXNAColor(0.75f);
            circleView = Up ? TextureCircleView.CreateUpArrow(gridCircle, color) : TextureCircleView.CreateDownArrow(gridCircle, color);
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          AnnotationOverBackgroundLumaEffect overlayEffect,
                          OverlappedLocationLinkView[] listToDraw)
        {
            TextureCircleView[] backgroundCircles = listToDraw.Select(l => l.circleView).ToArray();
            TextureCircleView.Draw(device, scene, basicEffect, overlayEffect, backgroundCircles.ToArray());
        }

        public void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene)
        {
            double DesiredRowsOfText = 4.0;
            double DefaultFontSize = (this.Radius * 2) / DesiredRowsOfText;
            label.FontSize = DefaultFontSize;
            label.MaxLineWidth = this.Radius * 2;

            label.Draw(spriteBatch, font, scene);
        }

        public bool IsLabelVisible(Scene scene)
        {
            return label.IsVisible(scene);
        }

        public LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.OffSectionLocationID;
            if (ModifierKeys.ShiftOrCtrlPressed())
                return LocationAction.NONE;

            return LocationAction.CREATELINKEDLOCATION;
        }

        public string[] HelpStrings
        {
            get
            {
                return new string[] {
                    "Hold left click + drag: Create additional annotation for this structure linked to the annotation on the adjacent section."
                };
            }
        }

        int ICanvasView.VisualHeight
        {
            get
            {
                return 0;
            }
        }
    }
}

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

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a location on an adjacent section that is overlapped by an annotation on the visible section.
    /// </summary>
    public class OverlappedLocationView : LocationCanvasView, IColorView, ILabelView
    {
        public TextureCircleView circleView;
        public LabelView label;

        public override SqlGeometry VolumeShapeAsRendered
        {
            get
            {
                return Circle.ToSqlGeometry(this.Z);
            }
        }

        public GridCircle Circle
        {
            get { return circleView.Circle; }
            set { circleView.Circle = value; }
        }

        public double Radius
        {
            get { return Circle.Radius; }
            set { circleView.Circle = new GridCircle(Circle.Center, value);}
        }

        public GridVector2 Position
        {
            get { return Circle.Center; }
            set { circleView.Circle = new GridCircle(value, Circle.Radius); }
        }

        private ICollection<long> _OverlappedLinks;
        public override ICollection<long> OverlappedLinks
        {
            protected get
            {
                return _OverlappedLinks;
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public OverlappedLocationView(LocationObj obj, GridCircle gridCircle, bool Up) : base(obj)
        {
            label = new LabelView(LocationLabel(obj), gridCircle.Center);
            label._Color = Microsoft.Xna.Framework.Color.Red;
            Microsoft.Xna.Framework.Color color = obj.Parent.Type.Color.ToXNAColor(0.75f);
            circleView = Up ? TextureCircleView.CreateUpArrow(gridCircle, color) : TextureCircleView.CreateDownArrow(gridCircle, color); 
        }
        
        private static string LocationLabel(LocationObj obj)
        {
            return obj.Z.ToString();
        }

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return this.circleView.IsVisible(scene);
        }

        public bool IsLabelVisible(VikingXNA.Scene scene)
        {
            return label.IsVisible(scene);
        }

        public override bool Intersects(GridVector2 Position)
        {
            return Circle.Contains(Position);
        }

        public override bool Intersects(SqlGeometry shape)
        {
            throw new NotImplementedException();
        }
        
        public override double Distance(GridVector2 Position)
        {
            double Distance = GridVector2.Distance(Position, this.Circle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.Circle.Center) / this.Radius;
        }

        public static void Draw(GraphicsDevice device,
                          VikingXNA.Scene scene,
                          BasicEffect basicEffect,
                          VikingXNA.AnnotationOverBackgroundLumaEffect overlayEffect,
                          OverlappedLocationView[] listToDraw)
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

        public override LocationAction GetMouseClickActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber, System.Windows.Forms.Keys ModifierKeys, out long LocationID)
        {
            LocationID = this.ID;

            if (ModifierKeys.ShiftOrCtrlPressed())
                return LocationAction.NONE;

            return LocationAction.CREATELINKEDLOCATION;
        }
        
        public ContextMenu ContextMenu
        {
            get
            {
                return new Location_CanvasContextMenuView(this.modelObj).ContextMenu;
            }
        }

        public override GridRectangle BoundingBox
        {
            get
            {
                return Circle.BoundingBox;
            }
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
    }
}

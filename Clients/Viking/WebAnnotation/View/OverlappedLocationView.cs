using System;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using Viking.Common;
using WebAnnotation;
using WebAnnotationModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using Common.UI;
using WebAnnotation.UI.Commands;
using WebAnnotation.ViewModel;
using Microsoft.SqlServer.Types;
using VikingXNAGraphics;

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a location on an adjacent section that is overlapped by an annotation on the visible section.
    /// </summary>
    public class OverlappedLocationView : LocationCanvasView
    {
        public readonly LocationLinkView link; 
        public TextureCircleView circleView;
        public LabelView label;

        public GridCircle gridCircle
        {
            get { return circleView.Circle; }
            set { circleView.Circle = value; }
        }

        public double Radius
        {
            get { return gridCircle.Radius; }
            set { circleView.Circle = new GridCircle(gridCircle.Center, value);}
        }

        public GridVector2 Position
        {
            get { return gridCircle.Center; }
            set { circleView.Circle = new GridCircle(value, gridCircle.Radius); }
        }

        public OverlappedLocationView(LocationObj obj, GridCircle gridCircle, bool Up) : base(obj)
        {
            label = new LabelView(LocationLabel(obj), gridCircle.Center);
            label.Color = Microsoft.Xna.Framework.Color.Red;
            circleView = Up ? TextureCircleView.CreateUpArrow(gridCircle) : TextureCircleView.CreateDownArrow(gridCircle);
            circleView.BackgroundColor = obj.Parent.Type.Color.ToXNAColor(0.75f); 
        }
        
        private static string LocationLabel(LocationObj obj)
        {
            return obj.Z.ToString();
        }

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            return this.circleView.IsVisible(scene);
        }
        
        public override bool Intersects(GridVector2 Position)
        {
            return gridCircle.Contains(Position);
        }

        public override bool Intersects(SqlGeometry shape)
        {
            throw new NotImplementedException();
        }

        public override bool IntersectsOnAdjacent(GridVector2 Position)
        {
            return gridCircle.Contains(Position);
        }

        public override double Distance(GridVector2 Position)
        {
            double Distance = GridVector2.Distance(Position, this.gridCircle.Center) - Radius;
            Distance = Distance < 0 ? 0 : Distance;
            return Distance;
        }

        public override double DistanceFromCenterNormalized(GridVector2 Position)
        {
            return GridVector2.Distance(Position, this.gridCircle.Center) / this.Radius;
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

        public override void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, VikingXNA.Scene scene, float MagnificationFactor, int DirectionToVisiblePlane)
        {
            double DesiredRowsOfText = 4.0;
            double NumUnscaledRows = (this.Radius * 2) / font.LineSpacing;
            double DefaultFontSize = NumUnscaledRows / DesiredRowsOfText;
            label.FontSize = DefaultFontSize;
            label.MaxLineWidth = this.Radius * 2;

            label.Draw(spriteBatch, font, scene, MagnificationFactor);
        }

        public override LocationAction GetActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            return LocationAction.CREATELINK;
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
                return gridCircle.BoundingBox;
            }
        }

        public override IList<LocationCanvasView> OverlappingLinks
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}

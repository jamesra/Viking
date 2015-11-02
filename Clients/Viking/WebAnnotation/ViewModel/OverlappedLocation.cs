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

namespace WebAnnotation.View
{
    /// <summary>
    /// Represents a location on an adjacent section that is overlapped by an annotation on the visible section.
    /// </summary>
    public class OverlappedLocation : LocationCanvasView
    {
        public readonly LocationLinkView link;
        public readonly GridCircle gridCircle;

        public override bool IsVisible(VikingXNA.Scene scene)
        {
            throw new NotImplementedException();
        }

        public override bool Intersects(GridVector2 Position)
        {
            throw new NotImplementedException();
        }

        public override double Distance(GridVector2 Position)
        {
            throw new NotImplementedException();
        }

        public override void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Vector2 LocationCenterScreenPosition, float MagnificationFactor, int DirectionToVisiblePlane)
        {
            throw new NotImplementedException();
        }

        public override LocationAction GetActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            return LocationAction.CREATELINK;
        }

        public OverlappedLocation(LocationObj overlappedLocation, LocationLinkView link, GridCircle circle) : base(overlappedLocation)
        {
            this.link = link;
            gridCircle = circle; 
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

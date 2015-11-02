using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.SqlServer.Types;
using WebAnnotationModel;
using SqlGeometryUtils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WebAnnotation.View
{
    class LocationLineView : LocationCanvasView
    {
        public override bool IsVisible(VikingXNA.Scene scene)
        {
            throw new NotImplementedException();
        }

        public override GridRectangle BoundingBox
        {
            get
            {
                return this.RenderedVolumeShape.Envelope();
            }
        }

        public override bool Intersects(GridVector2 Position)
        {
            return this.RenderedVolumeShape.Intersects(Position);
        }

        public override double Distance(GridVector2 Position)
        {
            return this.RenderedVolumeShape.Distance(Position);
        }

        protected override void OnObjPropertyChanged(object o, PropertyChangedEventArgs args)
        {
            _RenderedVolumeShape = null;
        }

        public override void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, Vector2 LocationCenterScreenPosition, float MagnificationFactor, int DirectionToVisiblePlane)
        {
            throw new NotImplementedException();
        }

        public override LocationAction GetActionForPositionOnAnnotation(GridVector2 WorldPosition, int VisibleSectionNumber)
        {
            throw new NotImplementedException();
        }

        public override IList<LocationCanvasView> OverlappingLinks
        {
            get
            {
                return new List<LocationCanvasView>();
            }
        }

        public virtual double Width
        {
            get { return modelObj.Radius; }
        }

        public LocationLineView(LocationObj obj) : base(obj)
        { }

        private SqlGeometry _RenderedVolumeShape;
        public virtual SqlGeometry RenderedVolumeShape
        {
            get
            {
                if (_RenderedVolumeShape == null)
                {
                    _RenderedVolumeShape = this.modelObj.VolumeShape.STBuffer(this.Width);
                }

                return _RenderedVolumeShape;
            }
        }
        
        public SqlGeometry VolumeShape
        {
            get { return this.VolumeShape; }
        }
    }
}

using Geometry;
using Microsoft.Xna.Framework;
using System.Linq;
using WebAnnotationModel;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal abstract class LocationCurveView(LocationObj obj, Viking.VolumeModel.LocationVolumeGeometry geometry) : LocationLineViewBase(obj, geometry), VikingXNAGraphics.IColorView
    {
        /// <summary>The smoothed curve in volume space, the points of <see cref="Viking.VolumeModel.LocationVolumeGeometry.Shape"/>.</summary>
        public abstract Geometry.Vector2[] VolumeCurveControlPoints { get; }
        public abstract Color Color { get; set; }
        public abstract float Alpha { get; set; }

        protected override Geometry.Vector2[] HitTestPoints => VolumeCurveControlPoints;
    }
}

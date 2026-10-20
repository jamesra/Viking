using Geometry;
using Microsoft.Xna.Framework;
using System.Linq;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace WebAnnotation.View
{
    internal abstract class LocationCurveView(LocationObj obj, Viking.VolumeModel.IVolumeToSectionTransform mapper) : LocationLineViewBase(obj, mapper), VikingXNAGraphics.IColorView
    {
        public abstract Geometry.Vector2[] MosaicCurveControlPoints { get; }
        public abstract Geometry.Vector2[] VolumeCurveControlPoints { get; }
        public abstract Color Color { get; set; }
        public abstract float Alpha { get; set; }

        protected override Geometry.Vector2[] HitTestPoints => VolumeCurveControlPoints;
    }
}

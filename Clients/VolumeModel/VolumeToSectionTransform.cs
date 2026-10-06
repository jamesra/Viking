using Geometry;
using Geometry.Transforms;
using System.Runtime.CompilerServices;

namespace Viking.VolumeModel
{
    public class VolumeToSectionTransform(string Name, ITransform transform) : IVolumeToSectionTransform
    {
        readonly string _Name = Name;
        readonly Geometry.ITransform Transform = transform;

        /// <summary>
        /// One fallback wrapper per underlying transform. GetSectionToVolumeTransform builds a new
        /// <see cref="VolumeToSectionTransform"/> on every call, so caching per instance would rebuild the
        /// RBF (and re-solve its weights) each time. Entries die with the underlying transform.
        /// </summary>
        static readonly ConditionalWeakTable<Geometry.ITransform, Geometry.ITransform> FallbackCache = new();

        /// <summary>
        /// Returns a mapper that behaves exactly like this one wherever the grid/mesh can map a point, and
        /// extrapolates with an RBF built from the same control points where it cannot.
        /// Returns this instance when the transform is already continuous or has no control points to fit.
        /// The RBF weights are solved lazily, so calling this is cheap until an unmappable point is mapped.
        /// </summary>
        public IVolumeToSectionTransform WithContinuousFallback()
        {
            if (Transform is IContinuousTransform ||
                Transform is not IDiscreteTransform discrete ||
                Transform is not ITransformControlPoints controlPoints)
                return this;

            Geometry.ITransform fallback = FallbackCache.GetValue(Transform, t =>
            {
                TransformBasicInfo info = (t as ITransformInfo)?.Info;
                return new DiscreteTransformWithContinuousFallback(discrete, new RBFTransform(controlPoints.MapPoints, info), info);
            });

            return new VolumeToSectionTransform(_Name + " RBF fallback", fallback);
        }

        public override string ToString() => _Name;

        public long ID => _Name.GetHashCode();

        public Rectangle? SectionBounds
        {
            get
            {
                if (Transform as IDiscreteTransform != null)
                {
                    return ((IDiscreteTransform)Transform).MappedBounds;
                }
                else
                {
                    return new Rectangle?();
                }
            }
        }

        public Rectangle? VolumeBounds
        {
            get
            {
                if (Transform as IDiscreteTransform != null)
                {
                    return ((IDiscreteTransform)Transform).ControlBounds;
                }
                else
                {
                    return new Rectangle?();
                }
            }
        }

        public Vector2[] SectionToVolume(Vector2[] Points) => Transform.Transform(Points);

        public Vector2 SectionToVolume(Vector2 P) => Transform.Transform(P);

        public bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP) => Transform.TryTransform(Points, out transformedP);

        public bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) => Transform.TryTransform(P, out transformedP);

        public bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP) => Transform.TryInverseTransform(Points, out transformedP);

        public bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) => Transform.TryInverseTransform(P, out transformedP);

        public Vector2[] VolumeToSection(Vector2[] Points) => Transform.InverseTransform(Points);

        public Vector2 VolumeToSection(Vector2 P) => Transform.InverseTransform(P);
    }
}

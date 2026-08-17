using Geometry;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Wraps one section's stos. ITransform.Transform is section→volume; ControlBounds is volume, MappedBounds is mosaic.
    /// </summary>
    public class VolumeToSectionTransform(string Name, ITransform transform) : IVolumeToSectionTransform
    {
        readonly string _Name = Name;
        readonly Geometry.ITransform Transform = transform;

        public override string ToString() => _Name;

        public long ID => _Name.GetHashCode();

        /// <summary>Mosaic-space hull (stos MappedBounds). Null when the transform is continuous.</summary>
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

        /// <summary>Volume-space hull (stos ControlBounds). Null when the transform is continuous.</summary>
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

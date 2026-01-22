using Geometry;

namespace Viking.VolumeModel
{
    public class VolumeToSectionTransform(string Name, ITransform transform) : IVolumeToSectionTransform
    {
        readonly string _Name = Name;
        readonly Geometry.ITransform Transform = transform;

        public override string ToString() => _Name;

        public long ID => _Name.GetHashCode();

        public GridRectangle? SectionBounds
        {
            get
            {
                if (Transform as IDiscreteTransform != null)
                {
                    return ((IDiscreteTransform)Transform).MappedBounds;
                }
                else
                {
                    return new GridRectangle?();
                }
            }
        }

        public GridRectangle? VolumeBounds
        {
            get
            {
                if (Transform as IDiscreteTransform != null)
                {
                    return ((IDiscreteTransform)Transform).ControlBounds;
                }
                else
                {
                    return new GridRectangle?();
                }
            }
        }

        public GridVector2[] SectionToVolume(GridVector2[] Points) => Transform.Transform(Points);

        public GridVector2 SectionToVolume(GridVector2 P) => Transform.Transform(P);

        public bool[] TrySectionToVolume(in GridVector2[] Points, out GridVector2[] transformedP) => Transform.TryTransform(Points, out transformedP);

        public bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP) => Transform.TryTransform(P, out transformedP);

        public bool[] TryVolumeToSection(in GridVector2[] Points, out GridVector2[] transformedP) => Transform.TryInverseTransform(Points, out transformedP);

        public bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP) => Transform.TryInverseTransform(P, out transformedP);

        public GridVector2[] VolumeToSection(GridVector2[] Points) => Transform.InverseTransform(Points);

        public GridVector2 VolumeToSection(GridVector2 P) => Transform.InverseTransform(P);
    }
}

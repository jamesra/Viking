using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace Viking.VolumeModel
{
    public class VolumeToSectionTransform : IVolumeToSectionTransform
    {
        readonly string _Name;
        readonly Geometry.ITransform Transform;

        public VolumeToSectionTransform(string Name, ITransform transform)
        {
            this._Name = Name;
            this.Transform = transform;
        }

        public override string ToString()
        {
            return _Name;
        }

        public long ID
        {
            get
            {
                return _Name.GetHashCode();
            }
        }

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

        public GridVector2[] SectionToVolume(GridVector2[] Points)
        {
            return Transform.Transform(Points);
        }

        public GridVector2 SectionToVolume(GridVector2 P)
        {
            return Transform.Transform(P);
        }

        public bool[] TrySectionToVolume(GridVector2[] Points, out GridVector2[] transformedP)
        {
            return Transform.TryTransform(Points, out transformedP);
        }

        public bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP)
        {
            return Transform.TryTransform(P, out transformedP);
        }

        public bool[] TryVolumeToSection(GridVector2[] Points, out GridVector2[] transformedP)
        {
            return Transform.TryInverseTransform(Points, out transformedP);
        }

        public bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP)
        {
            return Transform.TryInverseTransform(P, out transformedP);
        }

        public GridVector2[] VolumeToSection(GridVector2[] Points)
        {
            return Transform.InverseTransform(Points);
        }

        public GridVector2 VolumeToSection(GridVector2 P)
        {
            return Transform.InverseTransform(P);
        }
    }
}

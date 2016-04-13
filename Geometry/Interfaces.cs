using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public interface ITransform
    {
        bool CanTransform(GridVector2 Point);
        GridVector2 Transform(GridVector2 Point);
        GridVector2[] Transform(GridVector2[] Points);
        bool TryTransform(GridVector2 Point, out GridVector2 v);
        bool[] TryTransform(GridVector2[] Points, out GridVector2[] v);

        bool CanInverseTransform(GridVector2 Point);
        GridVector2 InverseTransform(GridVector2 Point);
        GridVector2[] InverseTransform(GridVector2[] Points);
        bool TryInverseTransform(GridVector2 Point, out GridVector2 v);
        bool[] TryInverseTransform(GridVector2[] Points, out GridVector2[] v);

        void Translate(GridVector2 vector);
    }

}

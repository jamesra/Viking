using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization; 

namespace Geometry
{
    [Serializable]
    abstract public class TransformBase
    {
        /// <summary>
        /// This records the modified date of the file the transform was loaded from
        /// </summary>
        public DateTime LastModified = DateTime.MinValue; 

        abstract public bool CanTransform(GridVector2 Point);
        abstract public GridVector2 Transform(GridVector2 Point);
        abstract public bool TryTransform(GridVector2 Point, out GridVector2 v);

        abstract public bool CanInverseTransform(GridVector2 Point);
        abstract public GridVector2 InverseTransform(GridVector2 Point);
        abstract public bool TryInverseTransform(GridVector2 Point, out GridVector2 v);
    }
}

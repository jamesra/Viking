using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace MorphologyMesh
{
    public class SliceChord : IEquatable<SliceChord>
    {
        public GridLineSegment Line;
        public PointIndex Origin; //The vertex originating the slice chord
        public PointIndex Target; //The target vertex

        public SliceChord(PointIndex O, PointIndex T, GridLineSegment line)
        {
            this.Line = line;
            this.Origin = O;
            this.Target = T;
        }

        public SliceChord(PointIndex O, PointIndex T, GridPolygon[] polygons)
        {
            this.Line = new GridLineSegment(O.Point(polygons), T.Point(polygons));
            this.Origin = O;
            this.Target = T;
        }

        public bool Equals(SliceChord other)
        {
            if (other.Origin == this.Origin &&
               other.Target == this.Target)
                return true;

            return false;
        }
    }
}
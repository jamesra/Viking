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

        public SliceChordTestType PassedTests; //Tests we know this chord has passed.
        public SliceChordTestType FailedTests; //Tests we know this chord has failed.
         
        public SliceChord(PointIndex O, PointIndex T, GridPolygon[] polygons)
        {
            this.Line = new GridLineSegment(O.Point(polygons), T.Point(polygons));
            this.Origin = O;
            this.Target = T;
            this.Orientation = EdgeTypeExtensions.Orientation(Origin, Target, polygons);
        }

        public bool Equals(SliceChord other)
        {
            if (other.Origin == this.Origin &&
               other.Target == this.Target)
                return true;

            return false;
        }

        public double Orientation
        {
            get; private set;
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Origin, Target);
        }
    }
}
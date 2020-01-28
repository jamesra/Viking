using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public class Mesh2D<VERTEX> : Mesh2DBase<VERTEX>
        where VERTEX : IVertex2D
    {
    }

    public class Mesh2D : Mesh2DBase<IVertex2D>
    {
        
    }

    public abstract class Mesh2DBase<VERTEX> : MeshBase<VERTEX>, IMesh2D<VERTEX>
        where VERTEX : IVertex2D
    {
        private GridRectangle? _BoundingBox = new GridRectangle?();


        public override IReadOnlyList<VERTEX> Verticies { get { return _Verticies; } }

        public GridRectangle BoundingBox
        {
            get
            {
                if(_BoundingBox.HasValue)
                {
                    return _BoundingBox.Value;
                }
                else if(Verticies.Count > 0)
                {
                    UpdateBoundingBox(this.Verticies);
                    return _BoundingBox.Value;
                }
                else
                {
                    return new GridRectangle();
                }
            }
        }

        protected override void UpdateBoundingBox(VERTEX vert)
        {
            if (_BoundingBox == null)
                _BoundingBox = new GridRectangle(vert.Position, 0);
            else
            {
                _BoundingBox.Value.Union(vert.Position);
            }
        }

        protected override void UpdateBoundingBox(IEnumerable<VERTEX> verts)
        {
            GridVector2[] points = verts.Select(v => v.Position).ToArray();
            if (_BoundingBox == null)
                _BoundingBox = points.BoundingBox();
            else
            {
                _BoundingBox.Value.Union(points.BoundingBox());
            }
        }
         
        public GridLineSegment ToGridLineSegment(IEdgeKey key)
        {
            return new GridLineSegment(this[key.A].Position, this[key.B].Position);
        }

        public GridLineSegment ToGridLineSegment(long A, long B)
        {
            return new GridLineSegment(this[A].Position, this[B].Position);
        }

        /// <summary>
        /// Return a normalized vector with origin at A towards B
        /// </summary> 
        /// <returns></returns>
        public GridLine ToGridLine(IEdgeKey key)
        {
            GridVector2 O = this[key.A].Position;
            return new GridLine(O, GridVector2.Normalize(this[key.B].Position - O));
        }

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public GridLine ToGridLine(long Origin, long Direction)
        {
            GridVector2 O = this[Origin].Position;
            return new GridLine(O, GridVector2.Normalize(this[Direction].Position - O));
        }

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public GridPolygon ToPolygon(IFace f)
        {
            var positions = f.iVerts.Select(v => this[v].Position);
            GridPolygon poly = new GridPolygon(positions);
            return poly;
        }

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public GridTriangle ToTriangle(IFace f)
        {
            var positions = f.iVerts.Select(v => this[v].Position).ToArray();
            GridTriangle tri = new GridTriangle(positions);
            return tri;
        }

        public RotationDirection Winding(IFace f)
        {
            return IsClockwise(f.iVerts) ? RotationDirection.CLOCKWISE : RotationDirection.COUNTERCLOCKWISE;
        }

        public bool IsClockwise(IFace f)
        {
            return IsClockwise(f.iVerts);
        }

        public bool IsClockwise(IEnumerable<int> verts)
        {
            return verts.Select(v => this[v].Position).ToArray().AreClockwise();
        }
    }
}

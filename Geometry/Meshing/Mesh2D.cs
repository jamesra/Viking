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
        public GridRectangle BoundingBox;

        protected override void UpdateBoundingBox(VERTEX vert)
        {
            if (BoundingBox == null)
                BoundingBox = new GridRectangle(vert.Position, 0);
            else
            {
                BoundingBox.Union(vert.Position);
            }
        }

        protected override void UpdateBoundingBox(IEnumerable<VERTEX> verts)
        {
            GridVector2[] points = verts.Select(v => v.Position).ToArray();
            if (BoundingBox == null)
                BoundingBox = points.BoundingBox();
            else
            {
                BoundingBox.Union(points.BoundingBox());
            }
        }
    }
}

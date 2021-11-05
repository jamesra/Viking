using Geometry.JSON;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

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
                if (_BoundingBox.HasValue)
                {
                    return _BoundingBox.Value;
                }
                else if (Verticies.Count > 0)
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
                _BoundingBox += vert.Position;
            }
        }

        protected override void UpdateBoundingBox(IEnumerable<VERTEX> verts)
        {
            var points = verts.Select(v => v.Position);
            if (_BoundingBox == null)
                _BoundingBox = points.BoundingBox();
            else
            {
                _BoundingBox = _BoundingBox.Value + points.BoundingBox();
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

        public GridVector2 Centroid(IFace f)
        {
            return this.ToTriangle(f).Centroid;
        }

        public RotationDirection Winding(IFace f)
        {
            return this[f].Select(v => v.Position).ToArray().Winding();
        }

        public bool IsClockwise(IFace f)
        {
            return IsClockwise(f.iVerts);
        }

        public bool IsClockwise(IEnumerable<int> verts)
        {
            return verts.Select(v => this[v].Position).ToArray().AreClockwise();
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces with 3 or 4 verticies.  It removes the face and adds the split faces from the mesh
        /// </summary>
        /// <param name="Duplicator">A constructor that can copy attributes of a face object</param>
        /// <returns></returns>
        public override void SplitFace(IFace face)
        {
            if (face.IsTriangle())
                return;

            if (face.IsQuad())
            {
                RemoveFace(face);

                GridVector2[] positions = this[face.iVerts].Select(v => v.Position).ToArray();
                if (GridVector2.Distance(positions[0], positions[2]) < GridVector2.Distance(positions[1], positions[3]))
                {
                    IFace ABC = CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[2] });
                    IFace ACD = CreateFace(new int[] { face.iVerts[0], face.iVerts[2], face.iVerts[3] });
                    AddFace(ABC);
                    AddFace(ACD);
                }
                else
                {
                    IFace ABD = CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[3] });
                    IFace BCD = CreateFace(new int[] { face.iVerts[1], face.iVerts[2], face.iVerts[3] });
                    AddFace(ABD);
                    AddFace(BCD);
                }
            }
        }


        /// <summary>
        /// Adds a face to edges.  This is a virtual method so that 2D meshes can throw an error if an edge has more than two faces
        /// </summary>
        /// <param name="face"></param>
        protected override void AddFaceToEdges(IFace face)
        {
            foreach (IEdgeKey e in face.Edges)
            {
                AddEdge(e);
                Edges[e].AddFace(face);
                /*
                if(Edges[e].Faces.Count() > 2)
                {
                    throw new ArgumentException("Cannot add more than two faces to a 2D mesh edge");
                }*/
            }
        }

        protected virtual JObject ToJObject()
        {
            dynamic jObj = new JObject();
            jObj.verts = this.Verticies.Select(v => v.Position).ToJArray();
            jObj.edges = new JArray(this.Edges.Values.Select(e => e.ToJObject()));
            jObj.faces = new JArray(this.Faces.Select(f => f.ToJObject()));
            return jObj;
        }

        public virtual string ToJSON()
        {
            return this.ToJObject().ToString();
        }

        public override string ToString()
        {
            return string.Format("{0} Verts {1} Edges {2} Faces", this.Verticies.Count, this.Edges.Count, this.Faces.Count);
        }
    }
}

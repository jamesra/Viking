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
        private Rectangle? _BoundingBox = new Rectangle?();


        public override IReadOnlyList<VERTEX> Vertices => _Verticies;

        public Rectangle BoundingBox
        {
            get
            {
                if (_BoundingBox.HasValue)
                {
                    return _BoundingBox.Value;
                }
                else if (Vertices.Count > 0)
                {
                    UpdateBoundingBox(this.Vertices);
                    return _BoundingBox.Value;
                }
                else
                {
                    return new Rectangle();
                }
            }
        }

        protected override void UpdateBoundingBox(VERTEX vert)
        {
            if (_BoundingBox is null)
                _BoundingBox = new Rectangle(vert.Position, 0);
            else
            {
                _BoundingBox += vert.Position;
            }
        }

        protected override void UpdateBoundingBox(IEnumerable<VERTEX> verts)
        {
            var points = verts.Select(v => v.Position);
            _BoundingBox = _BoundingBox is null ? points.BoundingBox() : _BoundingBox.Value + points.BoundingBox();
        }

        public LineSegment ToLineSegment(IEdgeKey key) => new LineSegment(this[key.A].Position, this[key.B].Position);

        public LineSegment ToLineSegment(long A, long B) => new LineSegment(this[A].Position, this[B].Position);

        /// <summary>
        /// Return a normalized vector with origin at A towards B
        /// </summary> 
        /// <returns></returns>
        public Line ToLine(IEdgeKey key)
        {
            Vector2 O = this[key.A].Position;
            return new Line(O, Vector2.Normalize(this[key.B].Position - O));
        }

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public Line ToLine(long Origin, long Direction)
        {
            Vector2 O = this[Origin].Position;
            return new Line(O, Vector2.Normalize(this[Direction].Position - O));
        }

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public Polygon ToPolygon(IFace f)
        {
            var positions = f.iVerts.Select(v => this[v].Position);
            Polygon poly = new(positions);
            return poly;
        }

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        public Triangle ToTriangle(IFace f)
        {
            var positions = f.iVerts.Select(v => this[v].Position).ToArray();
            Triangle tri = new(positions);
            return tri;
        }

        public Vector2 Centroid(IFace f) => this.ToTriangle(f).Centroid;

        public RotationDirection Winding(IFace f) => this[f].Select(v => v.Position).ToArray().Winding();

        public bool IsClockwise(IFace f) => IsClockwise(f.iVerts);

        public bool IsClockwise(IEnumerable<int> verts) => verts.Select(v => this[v].Position).ToArray().AreClockwise();

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

                Vector2[] positions = [.. this[face.iVerts].Select(v => v.Position)];
                if (Vector2.Distance(positions[0], positions[2]) < Vector2.Distance(positions[1], positions[3]))
                {
                    IFace ABC = CreateFace([face.iVerts[0], face.iVerts[1], face.iVerts[2]]);
                    IFace ACD = CreateFace([face.iVerts[0], face.iVerts[2], face.iVerts[3]]);
                    AddFace(ABC);
                    AddFace(ACD);
                }
                else
                {
                    IFace ABD = CreateFace([face.iVerts[0], face.iVerts[1], face.iVerts[3]]);
                    IFace BCD = CreateFace([face.iVerts[1], face.iVerts[2], face.iVerts[3]]);
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

        public virtual JObject ToJObject()
        {
            dynamic jObj = new JObject();
            jObj.verts = this.Vertices.Select(v => v.Position).ToJArray();
            jObj.edges = new JArray(this.Edges.Values.Select(e => e.ToJObject()));
            jObj.faces = new JArray(this.Faces.Select(f => f.ToJObject()));
            return jObj;
        }

        public virtual string ToJSON() => this.ToJObject().ToString();

        public override string ToString() => string.Format("{0} Verts {1} Edges {2} Faces", this.Vertices.Count, this.Edges.Count, this.Faces.Count);
    }
}

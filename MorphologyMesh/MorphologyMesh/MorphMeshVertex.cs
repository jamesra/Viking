using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MorphologyMesh
{

    public class MorphMeshVertex : Vertex3D, IVertex2D
    {
        /// <summary>
        /// Vertices we add to close holes will not have a poly index.  The medial axis verticies must have faces added because at this point they will not autocomplete.
        /// </summary>
        public readonly IShapeIndex ShapeIndex;

        public readonly MedialAxisIndex? MedialAxisIndex;

        /// <summary>
        /// Contains ID of corresponding vertex if the vertex is part of a corresponding vertex pair 
        /// Corresponding is same XY position, different Z levels)
        /// </summary>
        public int? Corresponding;

        /// <summary>
        /// Set to true if this vertex has a continuous wall of faces to the adjacent verticies in the shape
        /// </summary>
        public bool FacesAreComplete = false;

        public VertexOrigin Type
        {
            get
            {
                if (ShapeIndex is not null)
                {
                    return VertexOrigin.CONTOUR;
                }
                else if (MedialAxisIndex.HasValue)
                {
                    return VertexOrigin.MEDIALAXIS;
                }

                throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis");
            }
        }

        Vector2 IVertex2D.Position => this.Position.XY();

        public MorphMeshVertex(IShapeIndex shapeIndex, Vector3 p) : base(p)
        {
            ShapeIndex = shapeIndex;
        }

        public MorphMeshVertex(IShapeIndex shapeIndex, Vector3 p, Vector3 n) : base(p, n)
        {
            ShapeIndex = shapeIndex;
        }

        protected MorphMeshVertex(int index, IShapeIndex shapeIndex, Vector3 p, Vector3 n) : base(index, p, n)
        {
            ShapeIndex = shapeIndex;
        }

        public MorphMeshVertex(MedialAxisIndex medialIndex, Vector3 p) : base(p)
        {
            MedialAxisIndex = medialIndex;
        }

        public MorphMeshVertex(MedialAxisIndex medialIndex, Vector3 p, Vector3 n) : base(p, n)
        {
            MedialAxisIndex = medialIndex;
        }
        protected MorphMeshVertex(int index, MedialAxisIndex medialIndex, Vector3 p, Vector3 n) : base(index, p, n)
        {
            MedialAxisIndex = medialIndex;
        }

        public static MorphMeshVertex Duplicate(MorphMeshVertex old)
        {
            if (old is MorphMeshVertex vert)
            {
                return vert.Type switch
                {
                    VertexOrigin.MEDIALAXIS => new MorphMeshVertex(vert.MedialAxisIndex.Value, vert.Position, vert.Normal),
                    VertexOrigin.CONTOUR => new MorphMeshVertex(vert.ShapeIndex, vert.Position, vert.Normal),
                    _ => throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis"),
                };
            }

            throw new ArgumentException("Vertex must be not null");
            //return new Vertex3D(old.Position, old.Normal);
        }

        /// <summary>
        /// Return a copy of this vertex with a PointIndex pointing at a different polygon index, if applicable
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public static MorphMeshVertex Reindex(MorphMeshVertex old, int iShape)
        {
            if (old is MorphMeshVertex vert)
            {
                return vert.Type switch
                {
                    VertexOrigin.MEDIALAXIS => new MorphMeshVertex(vert.MedialAxisIndex.Value, vert.Position, vert.Normal),
                    VertexOrigin.CONTOUR => new MorphMeshVertex(vert.ShapeIndex.Reindex(iShape), vert.Position, vert.Normal),
                    _ => throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis"),
                };
            }

            throw new ArgumentException("Vertex must be not null");

            //return new Vertex3D(old.Position, old.Normal);
        }

        public override IVertex ShallowCopy()
        {
            return Type switch
            {
                VertexOrigin.MEDIALAXIS => new MorphMeshVertex(Index, MedialAxisIndex.Value, Position, Normal),
                VertexOrigin.CONTOUR => new MorphMeshVertex(Index, ShapeIndex, Position, Normal),
                _ => throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis"),
            };
        }

        public override IVertex ShallowCopy(int index)
        {
            return Type switch
            {
                VertexOrigin.MEDIALAXIS => new MorphMeshVertex(index, MedialAxisIndex.Value, Position, Normal),
                VertexOrigin.CONTOUR => new MorphMeshVertex(index, ShapeIndex, Position, Normal),
                _ => throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis"),
            };
        }

        /// <summary>
        /// Return true if there are continuous faces between the two adjacent verticies along the contour this vertex is part of
        /// </summary>
        /// <param name="mesh"></param>
        public bool IsFaceSurfaceComplete(MorphRenderMesh mesh)
        {
            if (ShapeIndex is null) //Not part of the contour of a polygon, we need to ensure we can walk faces from one of the verticies edges around in a circle back to the same edge
                return true;

            //Once we know the faces are complete for this vertex we can stop testing it
            if (FacesAreComplete)
                return true;

            IShapeIndex prev = ShapeIndex.Previous;
            IShapeIndex next = ShapeIndex.Next;

            MorphMeshVertex prevVertex = mesh[prev];
            MorphMeshVertex nextVertex = mesh[next];

            IEnumerable<IEdgeKey> startEdges = this.Edges.Where(e => mesh[e].Contains(prevVertex.Index));
            if (!startEdges.Any())
                return false;

            IEnumerable<IEdgeKey> endingEdges = this.Edges.Where(e => mesh[e].Contains(nextVertex.Index));
            if (!endingEdges.Any())
                return false;

            MorphMeshEdge start = mesh[startEdges.First()];
            MorphMeshEdge end = mesh[endingEdges.First()];

            //OK, walk the faces and determine if there is a path from the starting edge to the ending edge

            if (start.Faces.Count == 0)
                return false;

            //We expect the starting vertex to be a contour vertex
            Debug.Assert(start.Type == EdgeType.CONTOUR);

            //TODO: We probably need to ensure the path doesn't wrap all the away around the contours the long way at this step instead of later
            List<IFace> path = mesh.FindFacesInPath(start.Faces.First(), (face) => face.iVerts.Contains(this.Index), (face) => face.Edges.Contains(end));
            if (path is null)
                return false;

            //Check that every face in the shortest path always includes the vertex we are testing.
            FacesAreComplete = path.All(f => f.iVerts.Contains(this.Index));
            return FacesAreComplete;
        }

        int IComparable<IVertex2D>.CompareTo(IVertex2D other) => this.Index.CompareTo(other.Index);

        bool IEquatable<IVertex2D>.Equals(IVertex2D other) => this.Index == other.Index;
    }

}

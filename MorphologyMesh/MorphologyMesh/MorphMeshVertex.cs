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
        /// Verticies we add to close holes will not have a poly index.  The medial axis verticies must have faces added because at this point they will not autocomplete.
        /// </summary>
        public readonly PolygonIndex? PolyIndex;

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
                if (PolyIndex.HasValue)
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

        GridVector2 IVertex2D.Position { get { return this.Position.XY(); } }

        public MorphMeshVertex(PolygonIndex polyIndex, GridVector3 p) : base(p)
        {
            PolyIndex = polyIndex;
        }

        public MorphMeshVertex(PolygonIndex polyIndex, GridVector3 p, GridVector3 n) : base(p, n)
        {
            PolyIndex = polyIndex;
        }

        public MorphMeshVertex(MedialAxisIndex medialIndex, GridVector3 p) : base(p)
        {
            MedialAxisIndex = medialIndex;
        }

        public MorphMeshVertex(MedialAxisIndex medialIndex, GridVector3 p, GridVector3 n) : base(p, n)
        {
            MedialAxisIndex = medialIndex;
        }

        public static MorphMeshVertex Duplicate(MorphMeshVertex old)
        {
            MorphMeshVertex vert = old as MorphMeshVertex;
            if (vert != null)
            {
                switch (vert.Type)
                {
                    case VertexOrigin.MEDIALAXIS:
                        return new MorphMeshVertex(vert.MedialAxisIndex.Value, vert.Position, vert.Normal);
                    case VertexOrigin.CONTOUR:
                        return new MorphMeshVertex(vert.PolyIndex.Value, vert.Position, vert.Normal);
                    default:
                        throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis");
                }
            }

            throw new ArgumentException("Vertex must be not null");
            //return new Vertex3D(old.Position, old.Normal);
        }

        /// <summary>
        /// Return a copy of this vertex with a PointIndex pointing at a different polygon index, if applicable
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public static MorphMeshVertex Reindex(MorphMeshVertex old, int iPoly)
        {
            MorphMeshVertex vert = old as MorphMeshVertex;
            if (vert != null)
            {
                switch (vert.Type)
                {
                    case VertexOrigin.MEDIALAXIS:
                        return new MorphMeshVertex(vert.MedialAxisIndex.Value, vert.Position, vert.Normal);
                    case VertexOrigin.CONTOUR:
                        return new MorphMeshVertex(vert.PolyIndex.Value.Reindex(iPoly), vert.Position, vert.Normal);
                    default:
                        throw new InvalidOperationException("Vertex must be either part of a contour or on a medial axis");
                }
            }

            throw new ArgumentException("Vertex must be not null");

            //return new Vertex3D(old.Position, old.Normal);
        }

        /// <summary>
        /// Return true if there are continuos faces between the two adjacent verticies along the contour this vertex is part of
        /// </summary>
        /// <param name="mesh"></param>
        public bool IsFaceSurfaceComplete(MorphRenderMesh mesh)
        {
            if (!PolyIndex.HasValue) //Not part of the contour of a polygon, we need to ensure we can walk faces from one of the verticies edges around in a circle back to the same edge
                return true;

            //Once we know the faces are complete for this vertex we can stop testing it
            if (FacesAreComplete)
                return true;

            PolygonIndex prev = PolyIndex.Value.Previous;
            PolygonIndex next = PolyIndex.Value.Next;

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
            if (path == null)
                return false;

            //Check that every face in the shortest path always includes the vertex we are testing.
            FacesAreComplete = path.All(f => f.iVerts.Contains(this.Index));
            return FacesAreComplete;
        }

        int IComparable<IVertex2D>.CompareTo(IVertex2D other)
        {
            return this.Index.CompareTo(other.Index);
        }

        bool IEquatable<IVertex2D>.Equals(IVertex2D other)
        {
            return this.Index == other.Index;
        }
    }

}

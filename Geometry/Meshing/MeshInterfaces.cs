using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Geometry.Meshing
{
    public interface IVertex : IComparable<IVertex>, IEquatable<IVertex>
    {
        /// <summary>
        /// Index of the vertex in a mesh.  Throws InvalidOperationException if not set.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Sets the vertex index.  Can only be set once.  If a different index is desired use CreateShallowCopy(int).
        /// </summary>
        /// <param name="index"></param>
        void SetIndex(int index);

        /// <summary>
        /// True if an index has been set for this vertex
        /// </summary>
        bool HasIndex { get;}

        ImmutableSortedSet<IEdgeKey> Edges { get; }

        bool AddEdge(IEdgeKey e);

        void RemoveEdge(IEdgeKey e);

        /// <summary>
        /// Returns a duplicate of the vertex.
        /// </summary>
        /// <returns></returns>
        IVertex ShallowCopy();

        /// <summary>
        /// Returns a duplicate of the vertex with a new index value assigned
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        IVertex ShallowCopy(int index);
    }

    public interface IVertex2D : IVertex, IComparable<IVertex2D>, IEquatable<IVertex2D>
    {
        GridVector2 Position { get; }
    }

    public interface INormal
    {
        GridVector3 Normal { get; }
    }

    public interface IVertex2D<out T> : IVertex2D
    {
        T Data { get; }
    }


    public interface IVertex3D : IVertex, INormal, IComparable<IVertex3D>, IEquatable<IVertex3D>
    {
        GridVector3 Position { get; set; }
        new GridVector3 Normal { get; set; }
    }

    public interface IVertex3D<out T> : IVertex3D
    {
        T Data { get; }
    }

    public struct EdgeAngle
    {
        public long Origin;
        public long Target;
        public double Angle;
        public bool IsClockwise;

        public EdgeAngle(long origin, long target, double angle, bool clockwise)
        {
            Origin = origin;
            Target = target;
            Angle = angle;
            IsClockwise = clockwise;
        }

        public override string ToString()
        {
            return string.Format("{0}->{1} a: {2} cw: {3}", Origin, Target, Angle, IsClockwise);
        }
    }

    /// <summary>
    /// An interface  indicating the vertex can sort edges based on angle
    /// </summary>
    public interface IVertexSortEdgeByAngle
    {
        /// <summary>
        /// Given an edge on the vertex, return the vertex edges in either clockwise or counterclockwise order.
        /// With the passed origin edge being the first result as it has an angle of zero
        /// </summary>
        /// <param name="mesh">Mesh containing the indexed verticies</param>
        /// <param name="origin_edge">Edge connected vertex indicating the origin line.  Throws an argument exception if the edge doesn't exist.</param>
        /// <param name="clockwise">True if edges should be returned in clockwise order.  Default is counter-clockwise.</param>
        /// <returns>Indicies of partner verticies in sorted order.</returns>
        IEnumerable<long> EdgesByAngle(IComparer<IEdgeKey> comparer, long origin_edge, bool clockwise);
    }

    public interface IEdgeKey : IComparable<IEdgeKey>, IEquatable<IEdgeKey>
    {
        int A { get; }
        int B { get; }
        /// <summary>
        /// Return the endpoint opposite the paramter
        /// </summary>
        /// <param name="A"></param>
        /// <returns></returns>
        int OppositeEnd(int A);

        long OppositeEnd(long A);

        /// <summary>
        /// Returns true if the index is part of the key
        /// i.e. key.A == vertex || key.B == vertex
        /// </summary>
        /// <param name="vertex"></param>
        /// <returns></returns>
        bool Contains(long vertex);

        /// <summary>
        /// Returns true if the key shares an enpoint with the edge, but is not identical to the edge
        /// </summary>
        /// <param name="vertex"></param>
        /// <returns></returns>
        bool Adjacent(IEdgeKey edge);
    }

    public interface IEdge : IEdgeKey, IComparable<IEdge>, IEquatable<IEdge>
    {
        IEdgeKey Key { get; }
        ImmutableSortedSet<IFace> Faces { get; }

        void AddFace(IFace f);
        void RemoveFace(IFace f);

        IFace OppositeFace(IFace f);

        IEdge Clone();
    }

    public interface IFace : IComparable<IFace>, IEquatable<IFace>
    {
        ImmutableArray<int> iVerts { get; }
        ImmutableArray<IEdgeKey> Edges { get; }

        /// <summary>
        /// The object's iVerts array, but sorted.  Profiling showed not having this cache was considerably slower during bajaj mesh generation.  It was originally 20% of the composite mesh generation time.
        /// </summary>
        ImmutableArray<int> sortedVerts { get; }

        IFace Clone();
    }

    /// <summary>
    /// A face that always has three verticies
    /// </summary>
    public interface ITriangleFace : IFace
    {
        /// <summary>
        /// The vertex opposite the edge, the vertex in the triangle not part of the edge
        /// </summary>
        /// <param name="vertex"></param>
        /// <returns></returns>
        IEdgeKey OppositeEdge(int vertex);

        /// <summary>
        /// The edge opposite the triangle vertex
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        int OppositeVertex(IEdgeKey edge);
    }



}

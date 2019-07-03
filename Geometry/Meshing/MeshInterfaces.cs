using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using System.Collections.Immutable;

namespace Geometry.Meshing
{
    public interface IVertex : IComparable<IVertex>, IEquatable<IVertex>
    {
        GridVector3 Position { get; set; }
        GridVector3 Normal { get; set; }

        int Index { get; set; }

        ImmutableSortedSet<IEdgeKey> Edges { get; }

        bool AddEdge(IEdgeKey e);

        void RemoveEdge(IEdgeKey e);
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
    }

    public interface IEdge : IEdgeKey, IComparable<IEdge>, IEquatable<IEdge>
    {
        IEdgeKey Key { get; }
        ImmutableSortedSet<IFace> Faces { get; }

        void AddFace(IFace f);
        void RemoveFace(IFace f);
        
        
        //int OppositeEnd(int A);
    }

    public interface IFace : IComparable<IFace>, IEquatable<IFace>
    {
        ImmutableArray<int> iVerts { get; }
        ImmutableArray<IEdgeKey> Edges { get; }
    }


}

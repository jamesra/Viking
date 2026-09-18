using System;
using System.Collections;
using System.Collections.Generic;

namespace Geometry.Meshing
{
    /// <summary>
    /// Unordered endpoint pair used as the mesh edge dictionary key.
    /// Identity matches <see cref="EdgeKey"/> / <see cref="IEdgeKey"/>: only A and B, order-independent.
    /// </summary>
    readonly struct EndpointPair : IEquatable<EndpointPair>
    {
        public readonly int A;
        public readonly int B;

        public EndpointPair(int a, int b)
        {
            if (a <= b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public static EndpointPair From(IEdgeKey key)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));

            return new EndpointPair(key.A, key.B);
        }

        public static EndpointPair From(in EdgeKey key) => new(key.A, key.B);

        public EdgeKey ToEdgeKey() => new(A, B);

        public bool Equals(EndpointPair other) => A == other.A && B == other.B;

        public override bool Equals(object obj) => obj is EndpointPair other && Equals(other);

        public override int GetHashCode() => (int)(((long)A * (long)B) & int.MaxValue);
    }

    /// <summary>
    /// Edge lookup keyed by endpoint pair so insert and Contains do not box <see cref="IEdgeKey"/>.
    /// Enumeration of keys still boxes to <see cref="IEdgeKey"/> for the public mesh API.
    /// </summary>
    public sealed class MeshEdgeMap : IReadOnlyDictionary<IEdgeKey, IEdge>
    {
        readonly Dictionary<EndpointPair, IEdge> _map = [];

        public int Count => _map.Count;

        public IEdge this[IEdgeKey key] => _map[EndpointPair.From(key)];

        public IEdge this[EdgeKey key] => _map[EndpointPair.From(in key)];

        public bool ContainsKey(IEdgeKey key) => _map.ContainsKey(EndpointPair.From(key));

        public bool ContainsKey(EdgeKey key) => _map.ContainsKey(EndpointPair.From(in key));

        public bool Contains(int a, int b) => _map.ContainsKey(new EndpointPair(a, b));

        public bool TryGetValue(IEdgeKey key, out IEdge value) => _map.TryGetValue(EndpointPair.From(key), out value);

        public bool TryGetValue(EdgeKey key, out IEdge value) => _map.TryGetValue(EndpointPair.From(in key), out value);

        public void Add(IEdgeKey key, IEdge edge) => _map.Add(EndpointPair.From(key), edge);

        public void Add(EdgeKey key, IEdge edge) => _map.Add(EndpointPair.From(in key), edge);

        public bool Remove(IEdgeKey key) => _map.Remove(EndpointPair.From(key));

        public bool Remove(EdgeKey key) => _map.Remove(EndpointPair.From(in key));

        public IEnumerable<IEdgeKey> Keys
        {
            get
            {
                foreach (EndpointPair pair in _map.Keys)
                    yield return pair.ToEdgeKey();
            }
        }

        public IEnumerable<IEdge> Values => _map.Values;

        public IEnumerator<KeyValuePair<IEdgeKey, IEdge>> GetEnumerator()
        {
            foreach (KeyValuePair<EndpointPair, IEdge> kvp in _map)
                yield return new KeyValuePair<IEdgeKey, IEdge>(kvp.Key.ToEdgeKey(), kvp.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

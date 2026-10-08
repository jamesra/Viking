using System;
using System.Collections.Generic;

namespace Geometry.Meshing
{
    /// <summary>
    /// One constrained triangulation of a polygon, built once so a pen cut can recolor distant
    /// triangles instead of meshing the whole ring again. Vertex positions are in the same space
    /// as <see cref="Polygon"/> (the centering used during triangulation is undone).
    /// The retrace choice fill starts <see cref="Create"/> when the command or the polygon view begins,
    /// then calls <see cref="Separate"/> and <see cref="CapTriangleOrdinals"/> on the UI thread.
    /// </summary>
    public sealed class PolygonCutFill
    {
        readonly Vector2[] _positions;
        readonly int[] _triangles;
        readonly Dictionary<(int A, int B), List<int>> _edgeToTriangles;

        PolygonCutFill(Vector2[] positions, int[] triangles, Dictionary<(int A, int B), List<int>> edgeToTriangles)
        {
            _positions = positions;
            _triangles = triangles;
            _edgeToTriangles = edgeToTriangles;
        }

        /// <summary>Vertex positions in polygon space. Index order matches the triangulation.</summary>
        public IReadOnlyList<Vector2> Positions => _positions;

        /// <summary>Three vertex indices per triangle, in mesh order.</summary>
        public IReadOnlyList<int> TriangleIndices => _triangles;

        public int TriangleCount => _triangles.Length / 3;

        /// <summary>
        /// Triangulates <paramref name="polygon"/> off to the side of the pen stroke.
        /// Returns an empty fill when the polygon has no faces. Throws when triangulation throws;
        /// callers treat a faulted task as "use the pen path until a fallback mesh exists".
        /// </summary>
        public static PolygonCutFill Create(Polygon polygon)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));

            Vector2 centroid = polygon.Centroid;
            TriangulationMesh<IVertex2D<PolygonIndex>> mesh = polygon.Triangulate();
            Vector2[] positions = new Vector2[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                IVertex2D vertex = mesh.Vertices[i];
                positions[vertex.Index] = vertex.Position + centroid;
            }

            List<int> triangles = new(mesh.Faces.Count * 3);
            foreach (IFace face in mesh.Faces)
            {
                if (face.iVerts.Length != 3)
                    continue;

                triangles.Add(face.iVerts[0]);
                triangles.Add(face.iVerts[1]);
                triangles.Add(face.iVerts[2]);
            }

            int[] triangleArray = [.. triangles];
            return new PolygonCutFill(positions, triangleArray, BuildAdjacency(triangleArray));
        }

        /// <summary>
        /// Splits triangles into those the path does not meet and those it crosses.
        /// Kept triples are the original vertex indices in the original order.
        /// A path with fewer than two points leaves every triangle kept.
        /// </summary>
        public PolygonCutSeparation Separate(IReadOnlyList<Vector2> path)
        {
            if (path is null || path.Count < 2 || _triangles.Length == 0)
                return new PolygonCutSeparation((int[])_triangles.Clone(), []);

            LineSegment[] segments = ToSegments(path);
            List<int> kept = new(_triangles.Length);
            List<int> cavity = new();
            for (int i = 0; i < _triangles.Length; i += 3)
            {
                if (PathMeetsTriangle(i, segments))
                {
                    cavity.Add(_triangles[i]);
                    cavity.Add(_triangles[i + 1]);
                    cavity.Add(_triangles[i + 2]);
                }
                else
                {
                    kept.Add(_triangles[i]);
                    kept.Add(_triangles[i + 1]);
                    kept.Add(_triangles[i + 2]);
                }
            }

            return new PolygonCutSeparation([.. kept], [.. cavity]);
        }

        /// <summary>
        /// Triangle ordinals on the cap side of a shrink cut: the flood from the cap's boundary arc,
        /// not crossing triangles the path meets. The choice fill drops these from the cached
        /// cell so the cap mesh is not drawn twice. Empty when no arc edge is still a mesh edge.
        /// </summary>
        public int[] CapTriangleOrdinals(IReadOnlyList<Vector2> path, IReadOnlyList<Vector2> capRing)
        {
            if (path is null || path.Count < 2 || capRing is null || capRing.Count < 2 || _triangles.Length == 0)
                return [];

            LineSegment[] segments = ToSegments(path);
            bool[] cavity = new bool[TriangleCount];
            for (int t = 0; t < cavity.Length; t++)
                cavity[t] = PathMeetsTriangle(t * 3, segments);

            bool[] cap = new bool[TriangleCount];
            Queue<int> pending = new();
            // p - centroid + centroid is not always bitwise identical, so match on a rounded key.
            Dictionary<Vector2, int> vertexAt = new(_positions.Length);
            for (int i = 0; i < _positions.Length; i++)
                vertexAt[_positions[i].Round(Global.TransformSignificantDigits)] = i;

            int unique = capRing.Count > 1 && capRing[0] == capRing[capRing.Count - 1] ? capRing.Count - 1 : capRing.Count;
            for (int i = 0; i < unique; i++)
            {
                if (!vertexAt.TryGetValue(capRing[i].Round(Global.TransformSignificantDigits), out int a))
                    continue;
                if (!vertexAt.TryGetValue(capRing[(i + 1) % unique].Round(Global.TransformSignificantDigits), out int b))
                    continue;
                if (!_edgeToTriangles.TryGetValue(Edge(a, b), out List<int> owners))
                    continue;

                foreach (int triangle in owners)
                {
                    if (cavity[triangle] || cap[triangle])
                        continue;

                    cap[triangle] = true;
                    pending.Enqueue(triangle);
                }
            }

            while (pending.Count > 0)
            {
                int triangle = pending.Dequeue();
                int offset = triangle * 3;
                for (int corner = 0; corner < 3; corner++)
                {
                    int a = _triangles[offset + corner];
                    int b = _triangles[offset + ((corner + 1) % 3)];
                    if (!_edgeToTriangles.TryGetValue(Edge(a, b), out List<int> owners))
                        continue;

                    foreach (int neighbor in owners)
                    {
                        if (cavity[neighbor] || cap[neighbor])
                            continue;

                        cap[neighbor] = true;
                        pending.Enqueue(neighbor);
                    }
                }
            }

            List<int> ordinals = new();
            for (int t = 0; t < cap.Length; t++)
            {
                if (cap[t])
                    ordinals.Add(t);
            }

            return [.. ordinals];
        }

        /// <summary>
        /// Vertex-index triples for every triangle except <paramref name="excludedOrdinals"/>.
        /// The remaining triples keep the cached vertex indices.
        /// </summary>
        public int[] IndicesExcludingOrdinals(IReadOnlyList<int> excludedOrdinals)
        {
            if (excludedOrdinals is null || excludedOrdinals.Count == 0)
                return (int[])_triangles.Clone();

            bool[] skip = new bool[TriangleCount];
            foreach (int ordinal in excludedOrdinals)
            {
                if ((uint)ordinal < (uint)skip.Length)
                    skip[ordinal] = true;
            }

            List<int> indices = new(_triangles.Length);
            for (int t = 0; t < skip.Length; t++)
            {
                if (skip[t])
                    continue;

                int offset = t * 3;
                indices.Add(_triangles[offset]);
                indices.Add(_triangles[offset + 1]);
                indices.Add(_triangles[offset + 2]);
            }

            return [.. indices];
        }

        bool PathMeetsTriangle(int offset, LineSegment[] segments)
        {
            Vector2 a = _positions[_triangles[offset]];
            Vector2 b = _positions[_triangles[offset + 1]];
            Vector2 c = _positions[_triangles[offset + 2]];
            double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (Math.Abs(cross) <= Global.Epsilon)
                return false;

            Triangle triangle;
            try
            {
                triangle = new Triangle(a, b, c);
            }
            catch (ArgumentException)
            {
                return false;
            }

            foreach (LineSegment segment in segments)
            {
                if (triangle.Intersects(segment))
                    return true;
            }

            return false;
        }

        static LineSegment[] ToSegments(IReadOnlyList<Vector2> path)
        {
            LineSegment[] segments = new LineSegment[path.Count - 1];
            for (int i = 0; i < segments.Length; i++)
                segments[i] = new LineSegment(path[i], path[i + 1]);
            return segments;
        }

        static Dictionary<(int A, int B), List<int>> BuildAdjacency(int[] triangles)
        {
            Dictionary<(int A, int B), List<int>> edges = new();
            for (int t = 0; t < triangles.Length / 3; t++)
            {
                int offset = t * 3;
                AddEdge(edges, triangles[offset], triangles[offset + 1], t);
                AddEdge(edges, triangles[offset + 1], triangles[offset + 2], t);
                AddEdge(edges, triangles[offset + 2], triangles[offset], t);
            }

            return edges;
        }

        static void AddEdge(Dictionary<(int A, int B), List<int>> edges, int a, int b, int triangle)
        {
            (int A, int B) key = Edge(a, b);
            if (!edges.TryGetValue(key, out List<int> owners))
            {
                owners = new List<int>(2);
                edges.Add(key, owners);
            }

            owners.Add(triangle);
        }

        static (int A, int B) Edge(int a, int b) => a < b ? (a, b) : (b, a);
    }

    /// <summary>
    /// Vertex-index triples from <see cref="PolygonCutFill.Separate"/>. Kept triangles are the ones
    /// the cut does not cross; their indices are unchanged from the cached mesh.
    /// </summary>
    public readonly struct PolygonCutSeparation
    {
        public PolygonCutSeparation(int[] keptTriangleIndices, int[] cavityTriangleIndices)
        {
            KeptTriangleIndices = keptTriangleIndices ?? [];
            CavityTriangleIndices = cavityTriangleIndices ?? [];
        }

        public int[] KeptTriangleIndices { get; }
        public int[] CavityTriangleIndices { get; }
    }
}

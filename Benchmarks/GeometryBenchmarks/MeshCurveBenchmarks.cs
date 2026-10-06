using BenchmarkDotNet.Attributes;
using Geometry;
using Geometry.Meshing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// Meshing and curve-fitting hot spots: Bowyer-Watson Delaunay, the divide-and-conquer mesh generator that
    /// exercises <c>EdgesByAngle</c> and the edge and face hashes, Catmull-Rom curve fitting for an annotation-sized
    /// ring, <see cref="EdgeKey"/> construction plus hashing, and the face path search.
    /// All inputs are deterministic. Coordinates reach several hundred thousand pixels as they do in volume space.
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    public class MeshCurveBenchmarks
    {
        private Vector2[] _randomPoints;
        private Vector2[] _gridPoints;
        private Vector2[] _meshPoints;
        private Vector2[] _ring;
        private (int A, int B)[] _edgePairs;
        private TriangulationMesh<TriangulationVertex> _mesh;
        private IFace _pathStart;
        private int _pathVertex;
        private IEdgeKey _pathEnd;
        private IFace _widePathTarget;
        private Vector2 _widePathCenter;

        [GlobalSetup]
        public void Setup()
        {
            Random random = new(7);

            _randomPoints = new Vector2[1000];
            for (int i = 0; i < _randomPoints.Length; i++)
                _randomPoints[i] = new Vector2(random.NextDouble() * 500000, random.NextDouble() * 500000);

            _gridPoints = new Vector2[40 * 40];
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    _gridPoints[(y * 40) + x] = new Vector2(100000 + (x * 128), 200000 + (y * 128));

            _meshPoints = new Vector2[500];
            for (int i = 0; i < _meshPoints.Length; i++)
                _meshPoints[i] = new Vector2(random.NextDouble() * 500000, random.NextDouble() * 500000);

            _ring = new Vector2[201];
            for (int i = 0; i < 200; i++)
            {
                double angle = 2 * Math.PI * i / 200;
                double radius = 5000 * (1 + (0.1 * random.NextDouble()));
                _ring[i] = new Vector2(250000 + (radius * Math.Cos(angle)), 250000 + (radius * Math.Sin(angle)));
            }
            _ring[200] = _ring[0];

            _edgePairs = new (int, int)[10000];
            for (int i = 0; i < _edgePairs.Length; i++)
                _edgePairs[i] = (random.Next(0, 5000), random.Next(5000, 10000));

            _mesh = DelaunayMeshGenerator2D.TriangulateToMesh(_meshPoints);

            TriangulationVertex hub = _mesh.Vertices.OrderByDescending(v => v.Edges.Count()).First();
            _pathVertex = hub.Index;
            IFace[] fan = [.. _mesh.Faces.Where(f => f.iVerts.Contains(_pathVertex))];
            _pathStart = fan[0];
            int[] spokes = [.. hub.Edges.Select(e => e.OppositeEnd(_pathVertex))];
            _pathEnd = new EdgeKey(_pathVertex, spokes[spokes.Length / 2]);

            _widePathCenter = hub.Position;
            _widePathTarget = _mesh.Faces
                .Where(f => f.iVerts.All(v => Vector2.Distance(_mesh[v].Position, _widePathCenter) < 60000))
                .OrderByDescending(f => Vector2.Distance(_mesh[f.iVerts[0]].Position, _widePathCenter))
                .First();
        }

        [Benchmark]
        public int DelaunayRandom1000() => Delaunay2D.Triangulate(_randomPoints).Length;

        [Benchmark]
        public int DelaunayGrid40x40() => Delaunay2D.Triangulate(_gridPoints).Length;

        [Benchmark]
        public int CatmullRomClosedRing200() => _ring.CalculateCurvePoints(8, true).Length;

        [Benchmark]
        public int EdgeKeyHashSet10000()
        {
            HashSet<EdgeKey> set = [];
            foreach ((int a, int b) in _edgePairs)
                set.Add(new EdgeKey(b, a));
            return set.Count;
        }

        [Benchmark]
        public int TriangulateToMesh500() => DelaunayMeshGenerator2D.TriangulateToMesh(_meshPoints).Faces.Count;

        [Benchmark]
        public int FacePathAroundVertex() => _mesh.FindFacesInPath(_pathStart,
            face => face.iVerts.Contains(_pathVertex),
            face => face.Edges.Contains(_pathEnd)).Count;

        [Benchmark]
        public int FacePathWideRegion() => _mesh.FindFacesInPath(_pathStart,
            face => face.iVerts.All(v => Vector2.Distance(_mesh[v].Position, _widePathCenter) < 60000),
            face => face.Equals(_widePathTarget))?.Count ?? 0;
    }
}

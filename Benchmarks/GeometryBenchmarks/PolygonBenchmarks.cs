using BenchmarkDotNet.Attributes;
using Geometry;
using System;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// Point-in-polygon, validity, area, centroid, segment-query and circle checks at annotation-like sizes.
    /// The polygon is a slightly irregular circle so no segment is axis-aligned.
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    public class PolygonBenchmarks
    {
        [Params(50, 500, 5000)]
        public int Vertices;

        private Polygon _polygon;
        private Vector2[] _ring;
        private Vector2[] _queries;
        private Rectangle[] _queryRects;
        private Circle _circle;
        private Vector2[] _circumcircle;

        [GlobalSetup]
        public void Setup()
        {
            Random random = new(4);
            Vector2[] ring = new Vector2[Vertices + 1];
            for (int i = 0; i < Vertices; i++)
            {
                double angle = 2 * Math.PI * i / Vertices;
                double radius = 10000 * (1 + (0.02 * random.NextDouble()));
                ring[i] = new Vector2(250000 + (radius * Math.Cos(angle)), 250000 + (radius * Math.Sin(angle)));
            }
            ring[Vertices] = ring[0];
            _ring = ring;
            _polygon = new Polygon(ring);

            _queries = new Vector2[200];
            for (int i = 0; i < _queries.Length; i++)
                _queries[i] = new Vector2(250000 + (random.NextDouble() * 24000) - 12000, 250000 + (random.NextDouble() * 24000) - 12000);

            _queryRects = new Rectangle[200];
            for (int i = 0; i < _queryRects.Length; i++)
                _queryRects[i] = new Rectangle(_queries[i], 100.0);

            _circle = new Circle(new Vector2(250000, 250000), 10000);
            _circumcircle = [new Vector2(240000, 250000), new Vector2(250000, 260000), new Vector2(260000, 250000)];
        }

        [Benchmark]
        public int Contains200Points()
        {
            int inside = 0;
            foreach (Vector2 p in _queries)
                if (_polygon.Contains(p))
                    inside++;
            return inside;
        }

        [Benchmark]
        public bool IsValid() => _polygon.IsValid();

        [Benchmark]
        public double PolygonArea() => _ring.PolygonArea();

        [Benchmark]
        public Vector2 Centroid() => Polygon.CalculateCentroid(_ring);

        [Benchmark]
        public int SegmentsInRect200()
        {
            int count = 0;
            foreach (Rectangle r in _queryRects)
                count += _polygon.GetIntersectingSegments(r).Count;
            return count;
        }

        [Benchmark]
        public int AllSegmentsCount() => _polygon.AllSegments.Count;

        [Benchmark]
        public int CircleRelation200()
        {
            int touching = 0;
            foreach (Vector2 p in _queries)
                if (_circle.GetRelation(p) == ShapeRelation.Touching)
                    touching++;
            return touching;
        }

        [Benchmark]
        public int InCircle200()
        {
            int contained = 0;
            foreach (Vector2 p in _queries)
                if (Circle.Contains(_circumcircle, p) == ShapeRelation.Contained)
                    contained++;
            return contained;
        }

        [Benchmark]
        public double Magnitude200()
        {
            double sum = 0;
            foreach (Vector2 p in _queries)
                sum += p.Magnitude;
            return sum;
        }
    }
}

using BenchmarkDotNet.Attributes;
using Geometry;
using Geometry.Transforms;
using System.Linq;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// The point lookup inside inverse mapping: find the control-space triangle under a point. Compares collecting every
    /// candidate (<c>Intersects</c>), the iterator that can stop early (<c>IntersectionGenerator</c>), and the stoppable
    /// visitor the transforms now use (<c>TryFindFirst</c>).
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    public class RTreeBenchmarks
    {
        private RTree.RTree<MappingTriangle> _tree;
        private Vector2[] _queries;

        [GlobalSetup]
        public void Setup()
        {
            var stos = (TriangulationTransform)Rc2Data.LoadSectionToVolume();
            _tree = stos.controlTrianglesRTree;
            _queries = Rc2Data.Points(stos.ControlBounds, 1000, 5);
        }

        [Benchmark(Baseline = true)]
        public int IntersectsThenTest()
        {
            int found = 0;
            foreach (Vector2 p in _queries)
            {
                foreach (MappingTriangle t in _tree.Intersects(p.ToRTreeRect(0)))
                {
                    if (t.CanInverseTransform(p))
                    {
                        found++;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>The query the transforms use: stops at the first match and allocates nothing.</summary>
        [Benchmark]
        public int TryFindFirst()
        {
            int found = 0;
            foreach (Vector2 p in _queries)
            {
                if (_tree.TryFindFirst(p.X, p.Y, 0, p, static (t, q) => t.CanInverseTransform(q), out _))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public int GeneratorFirstHit()
        {
            int found = 0;
            foreach (Vector2 p in _queries)
            {
                if (_tree.IntersectionGenerator(p.ToRTreeRect(0)).Any(t => t.CanInverseTransform(p)))
                    found++;
            }
            return found;
        }
    }
}

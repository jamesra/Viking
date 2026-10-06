using BenchmarkDotNet.Attributes;
using Geometry;
using Geometry.Transforms;
using System.Linq;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// The RBF fallback used for points outside a discrete transform: the weight solve (MathNet, MKL when loaded) and
    /// point mapping. Control points are a subset of the RC2 section 646 stos.
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    public class RbfBenchmarks
    {
        [Params(100, 400)]
        public int ControlPoints;

        private MappingVector2[] _mapPoints;
        private Vector2[] _mapped;
        private Vector2[] _control;
        private RBFTransform _rbf;
        private Vector2[] _queries;

        [GlobalSetup]
        public void Setup()
        {
            MathNet.Numerics.Control.TryUseNativeMKL();
            var stos = (ITransformControlPoints)Rc2Data.LoadSectionToVolume();
            int stride = System.Math.Max(1, stos.MapPoints.Length / ControlPoints);
            _mapPoints = [.. stos.MapPoints.Where((_, i) => i % stride == 0).Take(ControlPoints)];
            _mapped = [.. _mapPoints.Select(p => p.MappedPoint)];
            _control = [.. _mapPoints.Select(p => p.ControlPoint)];
            _rbf = new RBFTransform(_mapPoints, new TransformBasicInfo(System.DateTime.UtcNow));
            _queries = Rc2Data.Points(stos.MappedBounds, 1000, 3);
            _rbf.Transform(_queries[0]);
        }

        [Benchmark]
        public object SolveWeights() => RBFTransform.CalculateRBFWeights(_mapped, _control, RBFTransform.StandardBasisFunction);

        [Benchmark]
        public Vector2[] TransformBatch() => _rbf.Transform(_queries);

        [Benchmark]
        public double TransformSinglePoints()
        {
            double sum = 0;
            foreach (Vector2 p in _queries)
                sum += _rbf.Transform(p).X;
            return sum;
        }
    }
}

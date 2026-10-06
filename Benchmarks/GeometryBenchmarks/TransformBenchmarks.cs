using BenchmarkDotNet.Attributes;
using Geometry;
using Geometry.Transforms;
using System.Linq;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// Point mapping and transform composition on RC2 section 646. The section-to-volume stos is a grid transform;
    /// composing a mosaic tile through it produces the mesh transform Viking draws.
    /// </summary>
    [MemoryDiagnoser]
    [InProcess]
    public class TransformBenchmarks
    {
        private const int PointCount = 1000;

        private ITransform _sectionToVolume;
        private IControlPointTriangulation[] _tiles;
        private ITransform _composedTile;
        private Vector2[] _sectionPoints;
        private Vector2[] _volumePoints;
        private Vector2[] _tilePoints;

        [GlobalSetup]
        public void Setup()
        {
            _sectionToVolume = Rc2Data.LoadSectionToVolume();
            ITransform[] mosaic = Rc2Data.LoadMosaicTiles();

            Rectangle mapped = ((ITransformControlPoints)_sectionToVolume).MappedBounds;
            Rectangle center = new(mapped.Center.X - (mapped.Width / 8), mapped.Center.X + (mapped.Width / 8),
                                   mapped.Center.Y - (mapped.Height / 8), mapped.Center.Y + (mapped.Height / 8));

            _tiles = [.. mosaic.Cast<IControlPointTriangulation>()
                .Where(t => center.Intersects(((ITransformControlPoints)t).ControlBounds))
                .Take(8)];

            _sectionPoints = Rc2Data.Points(mapped, PointCount, 1);
            _sectionToVolume.TryTransform(_sectionPoints, out _volumePoints);

            _composedTile = (ITransform)TriangulationTransform.Transform(_sectionToVolume, _tiles[0], ((ITransformInfo)_tiles[0]).Info);
            _tilePoints = Rc2Data.Points(((ITransformControlPoints)_tiles[0]).MappedBounds, PointCount, 2);
        }

        /// <summary>The section warp: compose 8 mosaic tiles with the section-to-volume transform.</summary>
        [Benchmark]
        public int ComposeEightTiles()
        {
            int points = 0;
            foreach (IControlPointTriangulation tile in _tiles)
                points += TriangulationTransform.Transform(_sectionToVolume, tile, ((ITransformInfo)tile).Info).MapPoints.Length;
            return points;
        }

        [Benchmark]
        public int GridForwardSinglePoints()
        {
            int mapped = 0;
            foreach (Vector2 p in _sectionPoints)
                if (_sectionToVolume.TryTransform(p, out _))
                    mapped++;
            return mapped;
        }

        [Benchmark]
        public bool[] GridForwardBatch() => _sectionToVolume.TryTransform(_sectionPoints, out _);

        [Benchmark]
        public bool[] GridInverseBatch() => _sectionToVolume.TryInverseTransform(_volumePoints, out _);

        [Benchmark]
        public bool[] MeshForwardBatch() => _composedTile.TryTransform(_tilePoints, out _);
    }
}

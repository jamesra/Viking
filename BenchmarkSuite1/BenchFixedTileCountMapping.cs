using System.Threading;
using System.Threading.Tasks;
using Geometry;
using Viking.VolumeModel;

namespace VolumeModel.Benchmarks
{
    /// <summary>
    /// Test double exposing <see cref="FixedTileCountMapping.VisibleTiles"/> for benchmarks.
    /// </summary>
    public sealed class BenchFixedTileCountMapping : FixedTileCountMapping
    {
        private ITransform[] _transforms = [];
        private readonly GridRectangle _bounds;

        public BenchFixedTileCountMapping(Section section, string name, GridRectangle bounds)
            : base(section, name, string.Empty, string.Empty)
        {
            _bounds = bounds;
        }

        public override string CachedTransformsFileName => "bench";

        public override bool Initialized => _transforms.Length > 0;

        public override GridRectangle ControlBounds => _bounds;

        public override GridRectangle? SectionBounds => _bounds;

        public override GridRectangle? VolumeBounds => _bounds;

        public override Task Initialize(CancellationToken token) => Task.CompletedTask;

        public override Task<ITransform[]> GetOrCreateTransforms(CancellationToken token) =>
            Task.FromResult(_transforms);

        public override ITransform[] GetLoadedTransformsOrNull() => _transforms;

        public void SetTransforms(ITransform[] transforms) => _transforms = transforms;

        public override TilePyramid VisibleTiles(GridRectangle visibleBounds, double downSample) =>
            VisibleTiles(visibleBounds, null, downSample);

        public override bool TrySectionToVolume(GridVector2 p, out GridVector2 transformedP)
        {
            transformedP = p;
            return true;
        }

        public override bool TryVolumeToSection(GridVector2 p, out GridVector2 transformedP)
        {
            transformedP = p;
            return true;
        }

        public override bool[] TrySectionToVolume(in GridVector2[] points, out GridVector2[] transformedP)
        {
            transformedP = points;
            return new bool[points.Length];
        }

        public override bool[] TryVolumeToSection(in GridVector2[] points, out GridVector2[] transformedP)
        {
            transformedP = points;
            return new bool[points.Length];
        }

        public override GridVector2[] SectionToVolume(GridVector2[] p) => p;

        public override GridVector2[] VolumeToSection(GridVector2[] p) => p;
    }
}

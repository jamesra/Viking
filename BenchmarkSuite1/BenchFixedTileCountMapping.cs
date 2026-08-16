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
        private readonly Rectangle _bounds;

        public BenchFixedTileCountMapping(Section section, string name, Rectangle bounds)
            : base(section, name, string.Empty, string.Empty)
        {
            _bounds = bounds;
        }

        public override string CachedTransformsFileName => "bench";

        public override bool Initialized => _transforms.Length > 0;

        public override Rectangle ControlBounds => _bounds;

        public override Rectangle? SectionBounds => _bounds;

        public override Rectangle? VolumeBounds => _bounds;

        public override Task Initialize(CancellationToken token) => Task.CompletedTask;

        public override Task<ITransform[]> GetOrCreateTransforms(CancellationToken token) =>
            Task.FromResult(_transforms);

        public override ITransform[] GetLoadedTransformsOrNull() => _transforms;

        public void SetTransforms(ITransform[] transforms) => _transforms = transforms;

        public override TilePyramid VisibleTiles(Rectangle visibleBounds, double downSample) =>
            VisibleTiles(visibleBounds, null, downSample);

        public override bool TrySectionToVolume(Vector2 p, out Vector2 transformedP)
        {
            transformedP = p;
            return true;
        }

        public override bool TryVolumeToSection(Vector2 p, out Vector2 transformedP)
        {
            transformedP = p;
            return true;
        }

        public override bool[] TrySectionToVolume(in Vector2[] points, out Vector2[] transformedP)
        {
            transformedP = points;
            return new bool[points.Length];
        }

        public override bool[] TryVolumeToSection(in Vector2[] points, out Vector2[] transformedP)
        {
            transformedP = points;
            return new bool[points.Length];
        }

        public override Vector2[] SectionToVolume(Vector2[] p) => p;

        public override Vector2[] VolumeToSection(Vector2[] p) => p;
    }
}

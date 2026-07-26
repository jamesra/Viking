using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Geometry;
using Geometry.Transforms;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Viking.VolumeModel;

namespace VolumeModel.Benchmarks
{
    /// <summary>
    /// Benchmarks the TilePyramid stall fix: dual-interface mosaic transforms must not double-add tiles.
    /// </summary>
    [SimpleJob(warmupCount: 3, iterationCount: 15)]
    public class TilePyramidVisibleTilesBenchmarks
    {
        private const int TransformCount = 200;
        private const int DownsampleLevel = 4;
        private static readonly int[] PyramidLevels = [1, 2, 4, 8, 16, 32, 64];

        private BenchFixedTileCountMapping _mapping = null!;
        private GridRectangle _visibleBounds;
        private TilePyramid _pyramidForAddTileBench = null!;
        private TileViewModel _sampleTile = null!;

        [GlobalSetup]
        public void Setup()
        {
            _visibleBounds = new GridRectangle(new GridVector2(0, 0), TransformCount * 1100 + 1024, 1024);
            var section = CreateBenchSection(sectionNumber: 42);
            var pyramid = CreateBenchPyramid(section);
            _mapping = new BenchFixedTileCountMapping(section, "bench", _visibleBounds)
            {
                CurrentPyramid = pyramid,
            };

            var transforms = new ITransform[TransformCount];
            Viking.VolumeModel.Global.TileCache = new TileCache();

            for (int i = 0; i < TransformCount; i++)
            {
                var info = new TileTransformInfo($"tile_{i:D4}.png", i, DateTime.UtcNow, 1024, 1024);
                var transform = new BenchDualInterfaceTileTransform(info, offsetX: i * 1100, offsetY: 0);
                transforms[i] = transform;

                foreach (int level in PyramidLevels.Where(l => l >= DownsampleLevel))
                {
                    var key = TileUniqueKey.Create(section.Number, _mapping.Name, pyramid.Name, level, info.TileFileName);
                    var verts = new PositionNormalTextureVertex[]
                    {
                        new(new GridVector3(0, 0, 0), GridVector3.UnitZ, new GridVector2(0, 0)),
                        new(new GridVector3(1024, 0, 0), GridVector3.UnitZ, new GridVector2(1, 0)),
                        new(new GridVector3(0, 1024, 0), GridVector3.UnitZ, new GridVector2(0, 1)),
                    };
                    Viking.VolumeModel.Global.TileCache.ConstructTile(
                        key,
                        verts,
                        [0, 1, 2],
                        $"bench/{level:D3}/{info.TileFileName}",
                        $"bench\\{level:D3}\\{info.TileFileName}",
                        _mapping.Name,
                        level,
                        1);
                }
            }

            _mapping.SetTransforms(transforms);

            var visible = _mapping.VisibleTiles(_visibleBounds, DownsampleLevel);
            int expectedTiles = TransformCount * PyramidLevels.Count(l => l >= DownsampleLevel);
            int actualTiles = visible.AvailableLevels.Sum(level => visible.GetTilesForLevel(level).Count);
            if (actualTiles != expectedTiles)
            {
                throw new InvalidOperationException(
                    $"VisibleTiles correctness check failed: expected {expectedTiles} unique tiles, got {actualTiles}. " +
                    "Pre-fix builds would throw ArgumentException on duplicate AddTile before completing.");
            }

            _pyramidForAddTileBench = new TilePyramid(_visibleBounds);
            _sampleTile = Viking.VolumeModel.Global.TileCache.ConstructTile(
                TileUniqueKey.Create(section.Number, _mapping.Name, pyramid.Name, DownsampleLevel, "sample.png"),
                [
                    new(new GridVector3(0, 0, 0), GridVector3.UnitZ, new GridVector2(0, 0)),
                    new(new GridVector3(64, 0, 0), GridVector3.UnitZ, new GridVector2(1, 0)),
                    new(new GridVector3(0, 64, 0), GridVector3.UnitZ, new GridVector2(0, 1)),
                ],
                [0, 1, 2],
                "bench/sample.png",
                "bench\\sample.png",
                _mapping.Name,
                DownsampleLevel,
                1);
        }

        [Benchmark(Description = "VisibleTiles (200 dual-interface transforms, cached tiles)")]
        public TilePyramid VisibleTiles_DualInterface_CachedHits()
        {
            return _mapping.VisibleTiles(_visibleBounds, DownsampleLevel);
        }

        [Benchmark(Description = "TilePyramid.AddTile duplicate key (idempotent)")]
        public void TilePyramid_AddTile_DuplicateKey_Idempotent()
        {
            var pyramid = new TilePyramid(_visibleBounds);
            for (int i = 0; i < 1000; i++)
            {
                pyramid.AddTile(DownsampleLevel, _sampleTile);
            }
        }

        [Benchmark(Description = "TilePyramid.AddTile unique keys")]
        public void TilePyramid_AddTile_UniqueKeys()
        {
            var pyramid = new TilePyramid(_visibleBounds);
            for (int i = 0; i < 1000; i++)
            {
                var key = TileUniqueKey.Create(42, "bench", "bench", DownsampleLevel, $"unique_{i}.png");
                var tile = new TileViewModel(
                    key,
                    [
                        new(new GridVector3(i, 0, 0), GridVector3.UnitZ, new GridVector2(0, 0)),
                        new(new GridVector3(i + 64, 0, 0), GridVector3.UnitZ, new GridVector2(1, 0)),
                        new(new GridVector3(i, 64, 0), GridVector3.UnitZ, new GridVector2(0, 1)),
                    ],
                    [0, 1, 2],
                    $"bench/{key.TextureName}",
                    $"bench\\{key.TextureName}",
                    DownsampleLevel);
                pyramid.AddTile(DownsampleLevel, tile);
            }
        }

        private static Section CreateBenchSection(int sectionNumber)
        {
            var section = (Section)FormatterServices.GetUninitializedObject(typeof(Section));
            section.Number = sectionNumber;
            section.Name = sectionNumber.ToString("D4");
            section.volume = null;

            FieldInfo? pathField = typeof(Section).GetField(nameof(Section.Path), BindingFlags.Instance | BindingFlags.Public);
            pathField?.SetValue(section, "http://bench/");

            return section;
        }

        private static Pyramid CreateBenchPyramid(Section section)
        {
            var element = new XElement("Pyramid",
                new XAttribute("name", "bench"),
                new XAttribute("path", "bench"));
            foreach (int level in PyramidLevels)
            {
                element.Add(new XElement("Level",
                    new XAttribute("Downsample", level),
                    new XAttribute("path", $"{level:D3}")));
            }

            return Pyramid.CreateFromElement(element, section)
                ?? throw new InvalidOperationException("Failed to create bench pyramid.");
        }
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Google.Protobuf.WellKnownTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;

namespace AnnotationParity
{
    /// <summary>
    /// Client-side cost of merging a region load into the location cache: converting gRPC <see cref="Location"/> payloads
    /// to <see cref="LocationObj"/> and adding them (first visit), or applying them to cached objects (refresh).
    /// Payloads mimic a dense section: mostly polygons with a 40-vertex ring, some circles, two links each.
    /// No server is involved; the WCF baseline needs the old service host and is not runnable here.
    /// </summary>
    [MemoryDiagnoser]
    public class LocationIngestBenchmarks
    {
        [Params(1000, 5000)]
        public int Count;

        private Location[] _payload = Array.Empty<Location>();
        private ServerUpdate<long, ILocation[]> _update;
        private ServerUpdate<long, ILocation[]> _changedA;
        private ServerUpdate<long, ILocation[]> _changedB;
        private bool _flip;
        private LocationStore _warmStore = null!;
        private StoreServerQueryResultsHandler<long, LocationObj, ILocation> _warmHandler = null!;

        [GlobalSetup]
        public void Setup()
        {
            _payload = Enumerable.Range(1, Count).Select(MakeLocation).ToArray();
            _update = new ServerUpdate<long, ILocation[]>(DateTime.UtcNow, _payload.Cast<ILocation>().ToArray(), Array.Empty<long>());

            _changedA = Changed(_payload, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            _changedB = Changed(_payload, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

            (_warmStore, _warmHandler) = NewStore();
            _warmHandler.ProcessServerUpdate(_update).GetAwaiter().GetResult();
        }

        private static (LocationStore, StoreServerQueryResultsHandler<long, LocationObj, ILocation>) NewStore()
        {
            var converter = new LocationServerToClientConverter();
            var store = new LocationStore(null!, null!, null!, converter, null!, null!);
            var handler = new StoreServerQueryResultsHandler<long, LocationObj, ILocation>(store, converter, new LocationServerToClientUpdater());
            return (store, handler);
        }

        /// <summary>The same rows edited on the server at <paramref name="modified"/>.</summary>
        private static ServerUpdate<long, ILocation[]> Changed(Location[] payload, DateTime modified)
        {
            ILocation[] rows = payload.Select(l =>
            {
                Location copy = l.Clone();
                copy.LastModified = Timestamp.FromDateTime(modified);
                return (ILocation)copy;
            }).ToArray();
            return new ServerUpdate<long, ILocation[]>(DateTime.UtcNow, rows, Array.Empty<long>());
        }

        private static Location MakeLocation(int id)
        {
            var location = new Location
            {
                Id = id,
                ParentId = 1 + (id % 200),
                Section = 250,
                Terminal = false,
                OffEdge = false,
                Username = "bench",
                Attributes = string.Empty,
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            };
            location.Links.Add(id + 1);
            location.Links.Add(id + 2);

            double cx = (id % 100) * 400.0;
            double cy = (id / 100) * 400.0;
            if (id % 10 < 3)
            {
                location.TypeCode = AnnotationType.Circle;
                location.Radius = 120;
                location.MosaicPosition = new AnnotationPoint { X = cx, Y = cy, Z = 250 };
                location.VolumePosition = new AnnotationPoint { X = cx, Y = cy, Z = 250 };
            }
            else
            {
                location.TypeCode = AnnotationType.Polygon;
                string ring = Ring(cx, cy, 150, 40);
                location.MosaicShape = new ProtoGeometry { Text = ring };
                location.VolumeShape = new ProtoGeometry { Text = ring };
            }

            return location;
        }

        private static string Ring(double cx, double cy, double r, int vertices)
        {
            var sb = new StringBuilder("POLYGON ((");
            for (int i = 0; i <= vertices; i++)
            {
                double a = 2 * Math.PI * (i % vertices) / vertices;
                if (i > 0)
                    sb.Append(", ");
                sb.Append((cx + r * Math.Cos(a)).ToString("F3", CultureInfo.InvariantCulture)).Append(' ')
                  .Append((cy + r * Math.Sin(a)).ToString("F3", CultureInfo.InvariantCulture));
            }

            return sb.Append("))").ToString();
        }

        /// <summary>First visit: every payload is new to the cache.</summary>
        [Benchmark(Baseline = true)]
        public Task Ingest_New()
        {
            var (_, handler) = NewStore();
            return handler.ProcessServerUpdate(_update);
        }

        /// <summary>Refresh of unchanged rows (a revisited region cell): every payload matches a cached object.</summary>
        [Benchmark]
        public Task Ingest_RefreshUnchanged() => _warmHandler.ProcessServerUpdate(_update);

        /// <summary>Refresh where every row was edited on the server since the cached copy.</summary>
        [Benchmark]
        public Task Ingest_RefreshChanged()
        {
            _flip = !_flip;
            return _warmHandler.ProcessServerUpdate(_flip ? _changedA : _changedB);
        }

        /// <summary>Conversion alone, without the cache: payload to <see cref="LocationObj"/> on one thread.</summary>
        [Benchmark]
        public int Convert_Only()
        {
            var converter = new LocationServerToClientConverter();
            int n = 0;
            foreach (Location location in _payload)
                n += converter.Convert(location).ID > 0 ? 1 : 0;
            return n;
        }

        /// <summary>WKT parsing alone: the mosaic shape of every polygon payload.</summary>
        [Benchmark]
        public int ParseWkt_Only()
        {
            int n = 0;
            foreach (Location location in _payload)
            {
                if (location.MosaicShape is { } shape)
                    n += Geometry.FromWKT.ParseWKT(shape.Text) is null ? 0 : 1;
            }

            return n;
        }
    }
}

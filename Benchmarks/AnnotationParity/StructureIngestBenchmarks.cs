using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Google.Protobuf.WellKnownTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace AnnotationParity
{
    /// <summary>
    /// Client-side cost of merging structures into the cache, the companion of <see cref="LocationIngestBenchmarks"/>:
    /// converting gRPC <see cref="Structure"/> payloads (cells with child synapses and a few tags) and adding them, or
    /// applying an unchanged or changed refresh to cached structures. No server is involved.
    /// </summary>
    [MemoryDiagnoser]
    public class StructureIngestBenchmarks
    {
        [Params(1000, 5000)]
        public int Count;

        private ServerUpdate<long, IStructure[]> _update;
        private ServerUpdate<long, IStructure[]> _changedA;
        private ServerUpdate<long, IStructure[]> _changedB;
        private bool _flip;
        private Structure[] _payload = Array.Empty<Structure>();
        private StoreServerQueryResultsHandler<long, StructureObj, IStructure> _warmHandler = null!;

        [GlobalSetup]
        public void Setup()
        {
            Structure[] payload = Enumerable.Range(1, Count).Select(MakeStructure).ToArray();
            _payload = payload;
            _update = new ServerUpdate<long, IStructure[]>(DateTime.UtcNow, payload.Cast<IStructure>().ToArray(), Array.Empty<long>());
            _changedA = Changed(payload, "edited A", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            _changedB = Changed(payload, "edited B", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

            _warmHandler = NewHandler();
            _warmHandler.ProcessServerUpdate(_update).GetAwaiter().GetResult();
        }

        private static StoreServerQueryResultsHandler<long, StructureObj, IStructure> NewHandler()
        {
            var converter = new StructureServerToClientConverter();
            var updater = new StructureServerToClientUpdater();
            var store = new StructureStore(null!, null!, null!, converter, updater, null!, null!);
            return new StoreServerQueryResultsHandler<long, StructureObj, IStructure>(store, converter, updater);
        }

        /// <summary>The same rows edited on the server at <paramref name="modified"/>.</summary>
        private static ServerUpdate<long, IStructure[]> Changed(Structure[] payload, string notes, DateTime modified)
        {
            IStructure[] rows = payload.Select(s =>
            {
                Structure copy = s.Clone();
                copy.Notes = notes;
                copy.LastModified = Timestamp.FromDateTime(modified);
                return (IStructure)copy;
            }).ToArray();
            return new ServerUpdate<long, IStructure[]>(DateTime.UtcNow, rows, Array.Empty<long>());
        }

        /// <summary>Every tenth structure is a cell; the others are its children (synapses) with a tag or two.</summary>
        private static Structure MakeStructure(int id)
        {
            bool cell = id % 10 == 1;
            var structure = new Structure
            {
                Id = id,
                TypeId = cell ? 1 : 34,
                Notes = string.Empty,
                Verified = false,
                Confidence = 0.5,
                Label = cell ? $"Cell {id}" : string.Empty,
                Username = "bench",
                Attributes = cell ? string.Empty : "<Structure><Attrib Name=\"Conventional\" /><Attrib Name=\"Glutamate\" Value=\"0.5\" /></Structure>",
                Created = Timestamp.FromDateTime(new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc)),
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            };

            if (cell)
            {
                for (int child = id + 1; child < id + 10; child++)
                    structure.ChildIds.Add(child);
            }
            else
            {
                structure.ParentId = id - ((id - 1) % 10);
            }

            return structure;
        }

        /// <summary>The attributes XML parse alone, which every refresh repeats per structure.</summary>
        [Benchmark]
        public int ParseAttributes_Only()
        {
            int n = 0;
            foreach (Structure s in _payload)
                n += WebAnnotationModel.Objects.ObjAttributeParser.ParseAttributes(s.Attributes).Count;
            return n;
        }

        /// <summary>First visit: every structure is new to the cache.</summary>
        [Benchmark(Baseline = true)]
        public Task Ingest_New() => NewHandler().ProcessServerUpdate(_update);

        /// <summary>Refresh of unchanged rows: every payload matches a cached structure.</summary>
        [Benchmark]
        public Task Ingest_RefreshUnchanged() => _warmHandler.ProcessServerUpdate(_update);

        /// <summary>Refresh where every row changed on the server since the last one.</summary>
        [Benchmark]
        public Task Ingest_RefreshChanged()
        {
            _flip = !_flip;
            return _warmHandler.ProcessServerUpdate(_flip ? _changedA : _changedB);
        }
    }
}

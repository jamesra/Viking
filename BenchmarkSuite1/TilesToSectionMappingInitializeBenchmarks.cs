using BenchmarkDotNet.Attributes;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using Microsoft.VSDiagnostics;

namespace VolumeModel.Benchmarks
{
    [CPUUsageDiagnoser]
    public class TilesToSectionMappingInitializeBenchmarks
    {
        private const int Iterations = 1000;
        private MappingBase _mapping;
        private CancellationToken _token;
        [GlobalSetup]
        public void Setup()
        {
            Assembly volumeModelAssembly = typeof(MappingBase).Assembly;
            Type mappingType = volumeModelAssembly.GetType("Viking.VolumeModel.TilesToSectionMapping") ?? throw new InvalidOperationException("Could not locate Viking.VolumeModel.TilesToSectionMapping via reflection.");
            ConstructorInfo ctor = mappingType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: new[] { typeof(Section), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) }, modifiers: null) ?? throw new InvalidOperationException("Could not locate TilesToSectionMapping constructor via reflection.");
            // Section is intentionally null: none of Initialize/Initialized/FreeMemory dereference it,
            // and this avoids constructing an unrelated, heavyweight Volume/Section/XML object graph
            // purely to exercise the synchronization pattern under test.
            object instance = ctor.Invoke(new object[] { null, "bench", "http://localhost", "bench.mosaic", string.Empty, string.Empty });
            // Seed the private transform cache directly so Initialized reports true without any
            // network/disk I/O, putting the mapping in the steady-state condition hit on every
            // repeat scene draw.
            FieldInfo transformsField = mappingType.GetField("_TileTransforms", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Could not locate _TileTransforms field via reflection.");
            Array emptyTransforms = Array.CreateInstance(transformsField.FieldType.GetElementType(), 0);
            transformsField.SetValue(instance, emptyTransforms);
            _mapping = (MappingBase)instance;
            _token = CancellationToken.None;
        }

        [Benchmark(Baseline = true)]
        public async Task Unconditional_Initialize()
        {
            for (int i = 0; i < Iterations; i++)
            {
                await _mapping.Initialize(_token).ConfigureAwait(false);
            }
        }

        [Benchmark]
        public async Task Guarded_Initialize()
        {
            for (int i = 0; i < Iterations; i++)
            {
                if (!_mapping.Initialized)
                    await _mapping.Initialize(_token).ConfigureAwait(false);
            }
        }
    }
}
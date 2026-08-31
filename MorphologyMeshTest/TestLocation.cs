using System.Collections.Generic;
using Viking.AnnotationServiceTypes.Interfaces;

namespace MorphologyMeshTest
{
    /// <summary>
    /// A minimal annotation location for building synthetic morphology graphs in tests, so a fixture can be written
    /// as geometry and a section number without a database or service behind it.
    /// </summary>
    internal sealed class TestLocation : ILocationReadOnly
    {
        public ulong ID { get; init; }
        public ulong ParentID { get; init; }
        public bool Terminal { get; init; }
        public bool OffEdge { get; init; }
        public bool IsVericosityCap { get; init; }
        public bool IsUntraceable { get; init; }
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
        public long UnscaledZ { get; init; }
        public LocationType TypeCode { get; init; }
        public double Z { get; init; }
        public double? Width { get; init; }
        public string MosaicGeometryWKT { get; init; }
        public string VolumeGeometryWKT { get; init; }

        public bool Equals(ILocationReadOnly other) => other != null && ID == other.ID;
    }
}

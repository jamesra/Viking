using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using Microsoft.SqlServer.Types;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WebAnnotation.WPF.MockData
{
    public static class MockData
    {
        public static Dictionary<ulong, MockStructureType> StructureTypes = new() {
            { 0, new MockStructureType { ID = 0, Name = "Cell", Code = "C", Color = 0x800000FF } },
            { 1, new MockStructureType { ID = 1, Name="Ribbon", Code = "R", ParentID=0, Color = 0xFF00FF00 } },
            { 2, new MockStructureType { ID = 2, Name="Synapse", Code = "S", ParentID=0, Color = 0xFFFF0000 } },
            { 3, new MockStructureType { ID = 3, Name="Gap Junction", Code = "G", ParentID=0, Color = 0xFFFF8000 } },
            { 4, new MockStructureType { ID = 4, Name="Post Synapse", Code = "PSD", ParentID=0, Color = 0xFF808000 } },
        };

        public static Dictionary<ulong, MockStructure> Structures = new() {
            { 100, new MockStructure { ID = 100, Label="100", TypeID=0 } },
            { 101, new MockStructure { ID = 101, Label="101", TypeID=0 } },
            { 102, new MockStructure { ID = 102, Label="102", TypeID=0 } },
            { 200, new MockStructure { ID = 200, Label="200 Ribbon", TypeID=1, ParentID=100 } },
            { 201, new MockStructure { ID = 201, Label="201 PSD", TypeID=2, ParentID=101} },
            { 202, new MockStructure { ID = 202, Label="202 Conventional", TypeID=4, ParentID=101 } },
            { 300, new MockStructure { ID = 300, Label="300 Gap Junction", TypeID=3, ParentID=100 } },
            { 301, new MockStructure { ID = 301, Label="301 Gap Junction", TypeID=3, ParentID=101} },
        };

        public static List<MockPermittedStructureLink> PermittedStructureLinks = [
            new MockPermittedStructureLink { SourceTypeID = 1, TargetTypeID=4, Directional=false },
            new MockPermittedStructureLink { SourceTypeID = 3, TargetTypeID=3, Directional=true },
            new MockPermittedStructureLink { SourceTypeID = 2, TargetTypeID=4, Directional=false }
        ];

        public static List<MockStructureLink> StructureLinks = [
            new MockStructureLink { SourceID = 200, TargetID=201, Directional=true },
            new MockStructureLink { SourceID = 202, TargetID=201, Directional=true },
            new MockStructureLink { SourceID = 301, TargetID=300, Directional=false }
        ];

        static MockData()
        {
        }
    }

    public class MockPermittedStructureLinks : ObservableCollection<MockPermittedStructureLink>
    { }

    public class MockPermittedStructureLink : IPermittedStructureLink
    {
        public ulong SourceTypeID { get; set; }

        public ulong TargetTypeID { get; set; }

        public bool Directional { get; set; }

        PermittedStructureLinkKey IDataObjectWithKey<PermittedStructureLinkKey>.ID
        {
            get => new((long)SourceTypeID, (long)TargetTypeID, Directional == false);
            set { SourceTypeID = (ulong)value.SourceTypeID; TargetTypeID = (ulong)value.TargetTypeID; Directional = value.Bidirectional == false; }
        }

        IPermittedStructureLinkKey IDataObjectWithKey<IPermittedStructureLinkKey>.ID
        {
            get => new PermittedStructureLinkKey((long)SourceTypeID, (long)TargetTypeID, Directional == false);
            set { SourceTypeID = (ulong)value.SourceTypeID; TargetTypeID = (ulong)value.TargetTypeID; Directional = value.Directional; }
        }

        public bool Equals(IPermittedStructureLink other)
        {
            if (other is null)
                return false;

            return SourceTypeID == other.SourceTypeID &&
                   TargetTypeID == other.TargetTypeID &&
                   Directional == other.Directional;
        }

        public MockPermittedStructureLink() { }
    }

    public class MockStructureTypes : ObservableCollection<MockStructureType>
    { }


    public class MockStructureType : IStructureTypeReadOnly
    {
        private static ulong nextID = 0;

        public MockStructureType()
        {
            _ID = nextID;
            nextID = nextID++;
        }

        public uint Color { get; set; } = 0x80808080;

        private ulong _ID;
        public ulong ID
        {
            get => _ID;
            set
            {
                if (value == this._ID)
                    return;

                if (MockData.StructureTypes != null)
                {
                    if (MockData.StructureTypes.ContainsKey(this._ID))
                    {
                        MockData.StructureTypes.Remove(this._ID);
                    }

                    MockData.StructureTypes[value] = this;
                }

                this._ID = value;
            }
        }

        public ulong? ParentID { get; set; }

        public string Name { get; set; }

        public string[] Tags { get; set; }

        public bool Equals(IStructureTypeReadOnly other)
        {
            if (other is null)
                return false;

            return this.ID == other.ID;
        }

        public IStructureTypeReadOnly[] Children
        {
            get
            {
                return [.. MockData.StructureTypes.Values.Where(t => t.ParentID == this._ID)];
            }
            set
            {
                if (value is null)
                    return;

                foreach (var child in value)
                {
                    if (child is not MockStructureType obj)
                        continue;

                    obj.ParentID = this._ID;

                    if (MockData.StructureTypes.ContainsKey(obj.ID) == false)
                    {

                        MockData.StructureTypes.Add(obj.ID, obj);
                    }
                }

            }
        }

        public IPermittedStructureLink[] Permitted { get; internal set; }

        public ulong[] AllowedInputLinks => [.. MockData.PermittedStructureLinks.Where(t => t.TargetTypeID == this._ID && t.Directional).Select(t => t.SourceTypeID)];

        public ulong[] AllowedOutputLinks => [.. MockData.PermittedStructureLinks.Where(t => t.SourceTypeID == this._ID && t.Directional).Select(t => t.TargetTypeID)];

        public ulong[] AllowedBidirectionalLinks => [.. MockData.PermittedStructureLinks.Where(t => (t.TargetTypeID == this._ID || t.SourceTypeID == this._ID) && t.Directional == false).Select(t => t.SourceTypeID == this._ID ? t.TargetTypeID : t.SourceTypeID)];

        public string Code { get; set; }

        public string Notes { get; set; }

        public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public bool Abstract { get; set; }

        public int AllowedShapes { get; set; }
    }

    public class MockStructure : IStructureReadOnly
    {
        public ulong ID { get; internal set; }

        public ulong? ParentID { get; internal set; }

        public ulong TypeID { get; internal set; }

        public string Label { get; internal set; }

        public ICollection<IStructureLinkKey> Links { get; internal set; } = new List<IStructureLinkKey>();

        public IStructureTypeReadOnly Type { get; internal set; }

        public string TagsXML { get; internal set; }

        public IReadOnlyDictionary<string, string> Attributes { get; internal set; } = new Dictionary<string, string>();

        public double Confidence { get; internal set; }

        public string Notes { get; internal set; }

        public bool Equals(IStructureReadOnly other)
        {
            if (other is null)
                return false;

            return this.ID == other.ID;
        }
    }

    public class MockStructureLink : IStructureLink
    {
        public ulong SourceID { get; set; }

        public ulong TargetID { get; set; }

        public bool Directional { get; set; }

        StructureLinkKey IDataObjectWithKey<StructureLinkKey>.ID
        {
            get => new((long)SourceID, (long)TargetID, Directional == false);
            set { SourceID = (ulong)value.SourceID; TargetID = (ulong)value.TargetID; Directional = value.Bidirectional == false; }
        }

        IStructureLinkKey IDataObjectWithKey<IStructureLinkKey>.ID
        {
            get => new StructureLinkKey((long)SourceID, (long)TargetID, Directional == false);
            set { SourceID = (ulong)value.SourceID; TargetID = (ulong)value.TargetID; Directional = value.Directional; }
        }

        public bool Equals(IStructureLink other)
        {
            if (other is null)
                return false;

            return this.SourceID == other.SourceID && this.TargetID == other.TargetID && this.Directional == other.Directional;
        }

        public bool Equals(IStructureLinkKey other)
        {
            if (other is null)
                return false;

            return this.SourceID == other.SourceID && this.TargetID == other.TargetID && this.Directional == other.Directional;
        }
    }

    public class MockLocation : ILocationReadOnly
    {
        public ulong ID { get; internal set; }

        public ulong ParentID { get; internal set; }

        public bool Terminal { get; internal set; }

        public bool OffEdge { get; internal set; }

        public bool IsVericosityCap { get; internal set; }

        public bool IsUntraceable { get; internal set; }

        public IReadOnlyDictionary<string, string> Attributes { get; internal set; } = new Dictionary<string, string>();

        public long UnscaledZ { get; internal set; }

        public string TagsXml { get; internal set; }

        public LocationType TypeCode { get; internal set; }

        public double Z { get; internal set; }

        public double? Width { get; internal set; }

        public string MosaicGeometryWKT { get; internal set; }

        public string VolumeGeometryWKT { get; internal set; }

        public SqlGeometry Geometry { get; internal set; }

        public bool Equals(ILocationReadOnly other)
        {
            if (other is null)
                return false;

            return this.ID == other.ID;
        }
    }

    public class MockLocationLink : ILocationLink
    {
        public ulong A { get; internal set; }

        public ulong B { get; internal set; }

        public ulong OtherKey(ulong key)
        {
            if (A == key)
                return B;
            if (B == key)
                return A;

            throw new System.ArgumentException($"{key} is not part of location link {A}-{B}");
        }

        LocationLinkKey IDataObjectWithKey<LocationLinkKey>.ID
        {
            get => new((long)A, (long)B);
            set { A = (ulong)value.A; B = (ulong)value.B; }
        }

        ILocationLinkKey IDataObjectWithKey<ILocationLinkKey>.ID
        {
            get => new LocationLinkKey((long)A, (long)B);
            set { A = value.A; B = value.B; }
        }

        public bool Equals(ILocationLink other)
        {
            if (other is null)
                return false;

            return (this.A == other.A && this.B == other.B) || (this.B == other.A && this.A == other.B);
        }
    }



}

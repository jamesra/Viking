using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Annotation.Interfaces;
using Microsoft.SqlServer.Types;
using System.Collections.ObjectModel;

namespace WebAnnotation.WPF.MockData
{
    public static class MockData
    {
        public static Dictionary<ulong, MockStructureType> StructureTypes = new Dictionary<ulong, MockStructureType>() {
            { 0, new MockStructureType { ID = 0, Name = "Cell", Code = "C", Color = 0x800000FF } },
            { 1, new MockStructureType { ID = 1, Name="Ribbon", Code = "R", ParentID=0, Color = 0xFF00FF00 } },
            { 2, new MockStructureType { ID = 2, Name="Synapse", Code = "S", ParentID=0, Color = 0xFFFF0000 } },
            { 3, new MockStructureType { ID = 3, Name="Gap Junction", Code = "G", ParentID=0, Color = 0xFFFF8000 } },
            { 4, new MockStructureType { ID = 4, Name="Post Synapse", Code = "PSD", ParentID=0, Color = 0xFF808000 } },
        };

        public static Dictionary<ulong, MockStructure> Structures = new Dictionary<ulong, MockStructure>() {
            { 100, new MockStructure { ID = 100, Label="100", TypeID=0 } },
            { 101, new MockStructure { ID = 101, Label="101", TypeID=0 } }, 
            { 102, new MockStructure { ID = 102, Label="102", TypeID=0 } },
            { 200, new MockStructure { ID = 200, Label="200 Ribbon", TypeID=1, ParentID=100 } }, 
            { 201, new MockStructure { ID = 201, Label="201 PSD", TypeID=2, ParentID=101} },
            { 202, new MockStructure { ID = 202, Label="202 Conventional", TypeID=4, ParentID=101 } },
            { 300, new MockStructure { ID = 300, Label="300 Gap Junction", TypeID=3, ParentID=100 } },
            { 301, new MockStructure { ID = 301, Label="301 Gap Junction", TypeID=3, ParentID=101} },
        };

        public static List<MockPermittedStructureLink> PermittedStructureLinks = new List<MockPermittedStructureLink> {
            new MockPermittedStructureLink { SourceTypeID = 1, TargetTypeID=4, Directional=false },
            new MockPermittedStructureLink { SourceTypeID = 3, TargetTypeID=3, Directional=true },
            new MockPermittedStructureLink { SourceTypeID = 2, TargetTypeID=4, Directional=false }
        };

        public static List<MockStructureLink> StructureLinks = new List<MockStructureLink> {
            new MockStructureLink { SourceID = 200, TargetID=201, Directional=true },
            new MockStructureLink { SourceID = 202, TargetID=201, Directional=true },
            new MockStructureLink { SourceID = 301, TargetID=300, Directional=false }
        };

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

        public bool Equals(IPermittedStructureLink other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return SourceTypeID == other.SourceTypeID &&
                   TargetTypeID == other.TargetTypeID &&
                   Directional == other.Directional;
        }

        public MockPermittedStructureLink() { }
    }

    public class MockStructureTypes : ObservableCollection<MockStructureType>
    { }


    public class MockStructureType : IStructureType
    {
        private static ulong nextID = 0; 

        public MockStructureType()
        {
            _ID = nextID;
            nextID = nextID + 1;  
        }

        public uint Color { get; set; } = 0x80808080;

        private ulong _ID;
        public ulong ID
        {
            get { return _ID; }
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

        public bool Equals(IStructureType other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.ID == other.ID;
        }

        public IStructureType[] Children
        {
            get
            {
                return MockData.StructureTypes.Values.Where(t => t.ParentID == this._ID).ToArray();
            }
            set
            {
                if (value == null)
                    return; 

                foreach(var child in value)
                {
                    MockStructureType obj = child as MockStructureType;
                    if (obj == null)
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

        public ulong[] AllowedInputLinks
        {
            get {
                return MockData.PermittedStructureLinks.Where(t => t.TargetTypeID == this._ID && t.Directional).Select(t => t.SourceTypeID).ToArray();
            }
        }

        public ulong[] AllowedOutputLinks
        {
            get
            {
                return MockData.PermittedStructureLinks.Where(t => t.SourceTypeID == this._ID && t.Directional).Select(t => t.TargetTypeID).ToArray();
            }
        }

        public ulong[] AllowedBidirectionalLinks
        {
            get
            {
                return MockData.PermittedStructureLinks.Where(t => (t.TargetTypeID == this._ID || t.SourceTypeID == this._ID) && t.Directional==false)
                    .Select(t => t.SourceTypeID == this._ID ? t.TargetTypeID : t.SourceTypeID)
                    .ToArray();
            }
        }

        public string Code { get; set; }

        public string Notes { get; set; }
    }

    public class MockStructure : IStructure
    {
        public ulong ID {get; internal set;}

        public ulong? ParentID {get; internal set;}

        public ulong TypeID {get; internal set;}

        public string Label {get; internal set;}

        public ICollection<IStructureLink> Links {get; internal set;}

        public IStructureType Type {get; internal set;}

        public string TagsXML {get; internal set;}

        public bool Equals(IStructure other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.ID == other.ID;
        }
    }

    public class MockStructureLink : IStructureLink
    {
        public ulong SourceID { get; internal set; }

        public ulong TargetID { get; internal set; }

        public bool Directional { get; internal set; }

        public bool Equals(IStructureLink other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.SourceID == other.SourceID && this.TargetID == other.TargetID && this.Directional == other.Directional;
        }
    }

    public class MockLocation : ILocation
    {
        public ulong ID { get; internal set; }

        public ulong ParentID { get; internal set; }

        public bool Terminal { get; internal set; }

        public bool OffEdge { get; internal set; }

        public bool IsVericosityCap { get; internal set; }

        public bool IsUntraceable { get; internal set; }

        public IDictionary<string, string> Attributes { get; internal set; }

        public long UnscaledZ { get; internal set; }

        public string TagsXml { get; internal set; }

        public LocationType TypeCode { get; internal set; }

        public double Z { get; internal set; }

        public SqlGeometry Geometry { get; internal set; }

        public bool Equals(ILocation other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.ID == other.ID;
        }
    }

    public class MockLocationLink : ILocationLink
    {
        public ulong A { get; internal set; }

        public ulong B { get; internal set; }

        public bool Equals(ILocationLink other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return (this.A == other.A && this.B == other.B) || (this.B == other.A && this.A == other.B);
        }
    }



}

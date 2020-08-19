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
    public class MockData
    {
        public static ObservableCollection<IStructureType> RootStructureTypes;

        public ObservableCollection<IStructureType> RootStructureTypesInstance;

        public MockData()
        {
            RootStructureTypesInstance = RootStructureTypes;
        }

        static  MockData()
        {
            RootStructureTypes = new ObservableCollection<IStructureType>( new MockStructureType[] {
                                new MockStructureType {ID=0, ParentID=null, Name="Cell", Children = new MockStructureType[]{
                                               new MockStructureType {ID=1, Name="Synapse", ParentID=0},
                                               new MockStructureType {ID=2, Name="Gap Junction", ParentID=0}
                                        }
                                    }
                                });
        }
    }

    public class MockStructureTypes : ObservableCollection<MockStructureType>
    { }


    public class MockStructureType : IStructureType
    {
        public MockStructureType()
        {

        }

        public uint Color { get; set; } = 0x80808080;

        public ulong ID {get; internal set;}

        public ulong? ParentID { get; internal set; }

        public string Name { get; internal set; }

        public string[] Tags { get; internal set; }

        public bool Equals(IStructureType other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            return this.ID == other.ID;
        }

        public IStructureType[] Children { get; internal set; }

        public string Code { get; internal set; }
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

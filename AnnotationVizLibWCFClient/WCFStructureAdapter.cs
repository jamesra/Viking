using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureAdapter : IStructureReadOnly
    {
        private readonly Structure structure;

        public WCFStructureAdapter(Structure s)
        {
            this.structure = s;
        }

        public ulong ID => (ulong)structure.ID;

        public string Label => structure.Label;

        public ICollection<IStructureLink> Links
        {
            get
            {
                if (structure.Links is null)
                    return new IStructureLink[0];

                return structure.Links.Select(l => new WCFStructureLinkAdapter(l)).ToArray();
            }
        }

        public ulong? ParentID
        {
            get
            {
                if (structure.ParentID.HasValue)
                    return (ulong)structure.ParentID;
                else
                    return new ulong?();
            }
        }

        public string TagsXML => structure.AttributesXml;

        public IStructureTypeReadOnly Type => new WCFStructureTypeAdapter(Queries.IDToStructureType[this.structure.TypeID]);

        public ulong TypeID => (ulong)structure.TypeID;

        public bool Equals(IStructureReadOnly other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }
    }
}

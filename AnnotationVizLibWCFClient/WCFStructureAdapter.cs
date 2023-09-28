﻿using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureAdapter : IStructure
    {
        private readonly Structure structure;

        public WCFStructureAdapter(Structure s)
        {
            this.structure = s;
        }

        public ulong ID
        {
            get
            {
                return (ulong)structure.ID;
            }
        }

        public string Label
        {
            get
            {
                return structure.Label;
            }
        }

        public ICollection<IStructureLink> Links
        {
            get
            {
                if (structure.Links == null)
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

        public string TagsXML
        {
            get
            {
                return structure.AttributesXml;
            }
        }

        public IStructureType Type
        {
            get
            {
                return new WCFStructureTypeAdapter(Queries.IDToStructureType[this.structure.TypeID]);
            }
        }

        public ulong TypeID
        {
            get
            {
                return (ulong)structure.TypeID;
            }
        }

        public bool Equals(IStructure other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }
    }
}

﻿using Viking.AnnotationServiceTypes.Interfaces;
using ODataClient.ConnectomeDataModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.OData
{
    class ODataStructureAdapter : IStructure
    {
        private readonly Structure structure;

        public ODataStructureAdapter(Structure s)
        {
            this.structure = s ?? throw new ArgumentNullException();
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
                List<StructureLink> links = structure.SourceOfLinks.ToList();
                links.AddRange(structure.TargetOfLinks);

                return links.Select(l => new ODataStructureLinkAdapter(l)).ToArray();
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
                return structure.Tags;
            }
        }

        public IStructureType Type
        {
            get
            {
                return new ODataStructureTypeAdapter(structure.Type);
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

        public bool Equals(Structure other)
        {
            return this.Equals((IStructure)other);
        }
    }
}

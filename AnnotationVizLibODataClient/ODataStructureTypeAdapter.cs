﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODataClient.ConnectomeDataModel;
using Annotation.Interfaces;

namespace AnnotationVizLib.OData
{
    class ODataStructureTypeAdapter : IStructureType
    {
        private StructureType type;
        public ODataStructureTypeAdapter(StructureType t)
        {
            if (t == null)
                throw new ArgumentNullException();
            type = t;
        }

        public ulong ID
        {
            get
            {
                return (ulong)type.ID;
            }
        }

        public string Name
        {
            get
            {
                return type.Name;
            }
        }

        public ulong? ParentID
        {
            get
            {
                return (ulong?)type.ParentID;
            }
        }

        public string[] Tags
        {
            get
            {
                return ObjAttribute.Parse(type.Tags).Select(a => a.ToString()).ToArray();
            }
        }

        public bool Equals(IStructureType other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(StructureType other)
        {
            return this.Equals((IStructureType)other);
        }
    }
}

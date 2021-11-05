using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos 
{
    public static class StructureTypeExtensions
    {
        public static StructureType ToStructureType(this IStructureType src)
        {
            var output = new StructureType
            {
                Id = src.ID,
                Attributes = src.Attributes,
                Abstract = src.Abstract,
                Color = src.Color,
                Name = src.Name,
                Notes = src.Notes, 
                AllowedShapes = src.AllowedShapes,
                Code = src.Code
            };

            if (src.ParentID.HasValue)
                output.ParentId = src.ParentID.Value;

            output.PermittedStructureLinks.AddRange(src.PermittedLinks.Cast<PermittedStructureLink>());

            return output;
        }
    }

    public partial class StructureType : IStructureType, IChangeAction
    {
        
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }
        long IDataObjectWithKey<long>.ID { get => Id; set => Id = value; }
        long? IDataObjectWithParent<long>.ParentID
        {
            get => HasParentId ? this.ParentId : new long?();
            set
            {
                if (value.HasValue)
                {
                    ParentId = (long)value.Value;
                }
                else
                {
                    ClearParentId();
                }
            }
        }

        //string IStructureType.MarkupType { get => this.throw new NotImplementedException(); set => throw new NotImplementedException(); }
        //string[] IStructureType.Tags { get => this.Tags }
        string IStructureType.Attributes { get => this.Attributes; set => this.Attributes = value; }

        IPermittedStructureLink[] IStructureType.PermittedLinks
        {
            get => this.permittedStructureLinks_.Cast<IPermittedStructureLink>().ToArray();
            set
            {
                permittedStructureLinks_.Clear();
                permittedStructureLinks_.AddRange(value.Cast<PermittedStructureLink>());
            }
        }

        //string[] IStructureType.StructureTags { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        //IPermittedStructureLink[] IStructureType.PermittedLinks { get => this.P; set => throw new NotImplementedException(); }

        bool IEquatable<IStructureType>.Equals(IStructureType other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other is null)
                return false;

            return this.Id == (long)other.ID;
        }

        public static explicit operator StructureTypeChangeRequest(StructureType src)
        {
            var value = new StructureTypeChangeRequest();
            switch (src._DBAction)
            {
                case DBACTION.NONE:
                    return null;
                case DBACTION.INSERT:
                    value.Create = src;
                    break;
                case DBACTION.UPDATE:
                    value.Update = src;
                    break;
                case DBACTION.DELETE:
                    value.Delete = src.Id;
                    break;
            }
            return value;
        }
         
    }
}

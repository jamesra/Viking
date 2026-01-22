using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class Location : ILocationReadOnly, IChangeAction
    {
        // ILocationReadOnly interface implementation
        ulong ILocationReadOnly.ID => (ulong)this.Id;

        ulong ILocationReadOnly.ParentID => this.HasParentId ? (ulong)this.ParentId : 0;

        bool ILocationReadOnly.Terminal => this.Terminal;

        bool ILocationReadOnly.OffEdge => this.OffEdge;

        // These properties don't exist in the protobuf, so return default values
        bool ILocationReadOnly.IsVericosityCap => false;

        bool ILocationReadOnly.IsUntraceable => false;

        IDictionary<string, string> ILocationReadOnly.Attributes => 
            string.IsNullOrEmpty(this.Attributes) ? new Dictionary<string, string>() : 
            ParseAttributesFromString(this.Attributes);

        // This property doesn't exist in the protobuf, so calculate from section
        long ILocationReadOnly.UnscaledZ => this.Section;

        // This property doesn't exist in the protobuf, use attributes instead
        string ILocationReadOnly.TagsXml => this.Attributes ?? string.Empty;

        LocationType ILocationReadOnly.TypeCode => (LocationType)(int)this.TypeCode;

        // This property doesn't exist in the protobuf, use section as fallback
        double ILocationReadOnly.Z => this.Section;

        Microsoft.SqlServer.Types.SqlGeometry ILocationReadOnly.Geometry => 
            this.VolumeShape?.Text != null ? 
            Microsoft.SqlServer.Types.SqlGeometry.Parse(this.VolumeShape.Text) :
            Microsoft.SqlServer.Types.SqlGeometry.Null;

        // IChangeAction implementation
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        // IEquatable implementation
        bool IEquatable<ILocationReadOnly>.Equals(ILocationReadOnly other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (ReferenceEquals(other, null))
                return false;

            return this.Id == (long)other.ID;
        }

        // Helper method to parse attributes string to dictionary
        private static IDictionary<string, string> ParseAttributesFromString(string attributes)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(attributes))
                return result;

            try
            {
                // Try to parse as simple key=value pairs separated by semicolons
                var pairs = attributes.Split(';');
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        result[keyValue[0].Trim()] = keyValue[1].Trim();
                    }
                }
            }
            catch
            {
                // If parsing fails, return empty dictionary
            }

            return result;
        }
    }
}

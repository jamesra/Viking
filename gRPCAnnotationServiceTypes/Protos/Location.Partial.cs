using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;

namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class Location : ILocation, IChangeAction
    {
        // ILocation interface implementation
        ulong ILocation.ID => (ulong)this.Id;

        ulong ILocation.ParentID => this.HasParentId ? (ulong)this.ParentId : 0;

        bool ILocation.Terminal => this.Terminal;

        bool ILocation.OffEdge => this.OffEdge;

        // These properties don't exist in the protobuf, so return default values
        bool ILocation.IsVericosityCap => false;

        bool ILocation.IsUntraceable => false;

        IDictionary<string, string> ILocation.Attributes => 
            string.IsNullOrEmpty(this.Attributes) ? new Dictionary<string, string>() : 
            ParseAttributesFromString(this.Attributes);

        // This property doesn't exist in the protobuf, so calculate from section
        long ILocation.UnscaledZ => this.Section;

        // This property doesn't exist in the protobuf, use attributes instead
        string ILocation.TagsXml => this.Attributes ?? string.Empty;

        LocationType ILocation.TypeCode => (LocationType)(int)this.TypeCode;

        // This property doesn't exist in the protobuf, use section as fallback
        double ILocation.Z => this.Section;

        Microsoft.SqlServer.Types.SqlGeometry ILocation.Geometry => 
            this.VolumeShape?.Text != null ? 
            Microsoft.SqlServer.Types.SqlGeometry.Parse(this.VolumeShape.Text) :
            Microsoft.SqlServer.Types.SqlGeometry.Null;

        // IChangeAction implementation
        DBACTION _DBAction = DBACTION.NONE;
        DBACTION IChangeAction.DBAction { get => _DBAction; set => _DBAction = value; }

        // IEquatable implementation
        bool IEquatable<ILocation>.Equals(ILocation other)
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

using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using Microsoft.SqlServer.Types;
using ODataClient.ConnectomeDataModel;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib.OData
{

    public class ODataLocationAdapter(Location l, UnitsAndScale.IScale scale) : ILocationReadOnly
    {
        private readonly Location loc = l ?? throw new ArgumentNullException(nameof(l));
        public readonly UnitsAndScale.IScale scale = scale ?? throw new ArgumentNullException(nameof(scale));

        public IReadOnlyDictionary<string, string> Attributes => loc.Attributes().ToDictionary(a => a.Name, a => a.Value);

        public double? Width => loc.Radius;

        public string MosaicGeometryWKT => loc.MosaicShape?.Geometry?.WellKnownText;

        public string VolumeGeometryWKT => loc.VolumeShape?.Geometry?.WellKnownText;

        private SqlGeometry _VolumeShape = null;
        public SqlGeometry Geometry
        {
            get
            {
                if (_VolumeShape is null)
                {
                    _VolumeShape = loc.VolumeShape.Geometry.WellKnownBinary != null
                        ? Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(loc.VolumeShape.Geometry.WellKnownBinary), loc.VolumeShape.Geometry.CoordinateSystemId.Value)
                        : Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(loc.VolumeShape.Geometry.WellKnownText), loc.VolumeShape.Geometry.CoordinateSystemId.Value);

                    _VolumeShape = _VolumeShape.Scale(scale);
                }

                return _VolumeShape;
            }

            set => _VolumeShape = value;
        }

        public ulong ID => (ulong)loc.ID;

        public bool IsUntraceable => loc.IsUntraceable();

        public bool IsVericosityCap => loc.IsVericosityCap();

        public bool OffEdge => loc.OffEdge;

        public ulong ParentID => (ulong)loc.ParentID;

        public bool Terminal => loc.Terminal;

        public long UnscaledZ => loc.Z;

        public double Z => (double)loc.Z * scale.Z.Value;

        public string TagsXml => loc.Tags;

        public LocationType TypeCode => (LocationType)loc.TypeCode;

        Box _BoundingBox = default;
        public Box BoundingBox
        {
            get
            {
                if (_BoundingBox == default)
                {
                    Rectangle bound_rect = Geometry.BoundingBox();
                    _BoundingBox = new Box(bound_rect, Z - scale.Z.Value, Z + scale.Z.Value);
                }

                return _BoundingBox;
            }
        }

        public bool Equals(ILocationReadOnly other)
        {
            if (other is null)
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(Location other) => this.Equals((ILocationReadOnly)other);
    }
}

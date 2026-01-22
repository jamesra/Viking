using Viking.AnnotationServiceTypes.Interfaces;
using AnnotationService.Types;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using UnitsAndScale;

namespace AnnotationVizLib.WCFClient
{
    class WCFLocationAdapter(Location l, IScale scale) : ILocationReadOnly
    {
        private readonly Location loc = l;
        public readonly IScale scale = scale;

        public IDictionary<string, string> Attributes => null;

        private SqlGeometry _VolumeShape = null;
        public SqlGeometry Geometry
        {
            get
            {
                if (_VolumeShape is null)
                {
                    if (loc.VolumeShape.WellKnownValue.WellKnownBinary != null)
                        _VolumeShape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(loc.VolumeShape.WellKnownValue.WellKnownBinary), loc.VolumeShape.CoordinateSystemId);
                    else _VolumeShape = loc.VolumeShape.WellKnownValue.WellKnownText != null
                        ? Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(loc.VolumeShape.WellKnownValue.WellKnownText), loc.VolumeShape.CoordinateSystemId)
                        : throw new InvalidOperationException("No well known text or binary to create SQLGeometry object: Location ID = " + loc.ID.ToString());

                    _VolumeShape = _VolumeShape.Scale(scale);
                }

                return _VolumeShape;
            }

            set => _VolumeShape = value;
        }

        private SqlGeometry _MosaicShape = null;
        public SqlGeometry MosaicGeometry
        {
            get
            {
                if (_MosaicShape is null)
                {
                    if (loc.MosaicShape.WellKnownValue.WellKnownBinary != null)
                        _VolumeShape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(loc.MosaicShape.WellKnownValue.WellKnownBinary), loc.MosaicShape.CoordinateSystemId);
                    else _VolumeShape = loc.MosaicShape.WellKnownValue.WellKnownText != null
                        ? Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(loc.MosaicShape.WellKnownValue.WellKnownText), loc.MosaicShape.CoordinateSystemId)
                        : throw new InvalidOperationException("No well known text or binary to create SQLGeometry object: Location ID = " + loc.ID.ToString());

                    _MosaicShape = _MosaicShape.Scale(scale);
                }

                return _MosaicShape;
            }

            set => _MosaicShape = value;
        }

        public ulong ID => (ulong)loc.ID;

        public bool IsUntraceable => loc.IsUntraceable();

        public bool IsVericosityCap => loc.IsVericosityCap();

        public bool OffEdge => loc.OffEdge;

        public ulong ParentID => (ulong)loc.ParentID;

        public bool Terminal => loc.Terminal;

        public double Z => (double)loc.VolumePosition.Z * scale.Z.Value;

        public long UnscaledZ => (long)loc.VolumePosition.Z;

        public string TagsXml => loc.AttributesXml;

        public LocationType TypeCode => (LocationType)loc.TypeCode;

        GridBox? _BoundingBox = null;
        public GridBox BoundingBox
        {
            get
            {
                if (!_BoundingBox.HasValue)
                {
                    GridRectangle bound_rect = Geometry.BoundingBox();
                    _BoundingBox = new GridBox(bound_rect, Z - scale.Z.Value, Z + scale.Z.Value);
                }

                return _BoundingBox.Value;
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
    }
}

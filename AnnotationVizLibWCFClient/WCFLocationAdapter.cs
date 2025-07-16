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
    class WCFLocationAdapter : ILocationReadOnly
    {
        private readonly Location loc;
        public readonly IScale scale;

        public WCFLocationAdapter(Location l, IScale scale)
        {
            this.loc = l;
            this.scale = scale;
        }

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
                    else if (loc.VolumeShape.WellKnownValue.WellKnownText != null)
                        _VolumeShape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(loc.VolumeShape.WellKnownValue.WellKnownText), loc.VolumeShape.CoordinateSystemId);
                    else
                        throw new InvalidOperationException("No well known text or binary to create SQLGeometry object: Location ID = " + loc.ID.ToString());

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
                    else if (loc.MosaicShape.WellKnownValue.WellKnownText != null)
                        _VolumeShape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(loc.MosaicShape.WellKnownValue.WellKnownText), loc.MosaicShape.CoordinateSystemId);
                    else
                        throw new InvalidOperationException("No well known text or binary to create SQLGeometry object: Location ID = " + loc.ID.ToString());

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

        GridBox _BoundingBox = default;
        public GridBox BoundingBox
        {
            get
            {
                if (_BoundingBox == null)
                {
                    GridRectangle bound_rect = Geometry.BoundingBox();
                    _BoundingBox = new GridBox(bound_rect, Z - scale.Z.Value, Z + scale.Z.Value);
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
    }
}

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

        public IDictionary<string, string> Attributes
        {
            get
            {
                return null;
            }
        }

        private SqlGeometry _VolumeShape = null;
        public SqlGeometry VolumeGeometry
        {
            get
            {
                if (_VolumeShape == null)
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

            set
            {
                _VolumeShape = value;
            }
        }

        private SqlGeometry _MosaicShape = null;
        public SqlGeometry MosaicGeometry
        {
            get
            {
                if (_MosaicShape == null)
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

            set
            {
                _MosaicShape = value;
            }
        }

        public ulong ID
        {
            get
            {
                return (ulong)loc.ID;
            }
        }

        public bool IsUntraceable
        {
            get
            {
                return loc.IsUntraceable();
            }
        }

        public bool IsVericosityCap
        {
            get
            {
                return loc.IsVericosityCap();
            }
        }

        public bool OffEdge
        {
            get
            {
                return loc.OffEdge;
            }
        }

        public ulong ParentID
        {
            get
            {
                return (ulong)loc.ParentID;
            }
        }

        public bool Terminal
        {
            get
            {
                return loc.Terminal;
            }
        }

        public double Z
        {
            get
            {
                return (double)loc.VolumePosition.Z * scale.Z.Value;
            }
        }

        public long UnscaledZ
        {
            get
            {
                return (long)loc.VolumePosition.Z;
            }
        }

        public string TagsXml
        {
            get
            {
                return loc.AttributesXml;
            }
        }

        public LocationType TypeCode
        {
            get
            {
                return (LocationType)loc.TypeCode;
            }
        }

        GridBox _BoundingBox = null;
        public GridBox BoundingBox
        {
            get
            {
                if (_BoundingBox == null)
                {
                    GridRectangle bound_rect = VolumeGeometry.BoundingBox();
                    _BoundingBox = new GridBox(bound_rect, Z - scale.Z.Value, Z + scale.Z.Value);
                }

                return _BoundingBox;
            }
        }

        public bool Equals(ILocationReadOnly other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }
    }
}

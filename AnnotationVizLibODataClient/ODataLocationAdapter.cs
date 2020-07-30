using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Types;
using ODataClient.ConnectomeDataModel;
using SqlGeometryUtils;
using Geometry;

namespace AnnotationVizLib.OData
{

    public class ODataLocationAdapter : ILocation
    {
        private readonly Location loc;
        public readonly Geometry.Scale scale;

        public ODataLocationAdapter(Location l, Geometry.Scale scale)
        {
            if (l == null)
                throw new ArgumentNullException();

            if (scale == null)
                throw new ArgumentNullException();

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
        public SqlGeometry Geometry
        {
            get
            {
                if (_VolumeShape == null)
                {
                    if (loc.VolumeShape.Geometry.WellKnownBinary != null)
                        _VolumeShape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromWKB(new System.Data.SqlTypes.SqlBytes(loc.VolumeShape.Geometry.WellKnownBinary), loc.VolumeShape.Geometry.CoordinateSystemId.Value);
                    else
                        _VolumeShape = Microsoft.SqlServer.Types.SqlGeometry.STGeomFromText(new System.Data.SqlTypes.SqlChars(loc.VolumeShape.Geometry.WellKnownText), loc.VolumeShape.Geometry.CoordinateSystemId.Value);

                    _VolumeShape = _VolumeShape.Scale(scale);
                }

                return _VolumeShape;
            }

            set
            {
                _VolumeShape = value;
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

        public long UnscaledZ
        {
            get
            {
                return loc.Z;
            }
        }

        public double Z
        {
            get
            {
                return (double)loc.Z * scale.Z.Value;
            }
        }

        public string TagsXml
        {
            get
            {
                return loc.Tags;
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
                    GridRectangle bound_rect = Geometry.BoundingBox();
                    _BoundingBox = new GridBox(bound_rect, Z - scale.Z.Value, Z + scale.Z.Value);
                }

                return _BoundingBox;
            }
        }

        public bool Equals(ILocation other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(Location other)
        {
            return this.Equals((ILocation)other);
        }
    }
}

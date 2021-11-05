using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using UnitsAndScale;

namespace AnnotationVizLib.SimpleOData
{
    public class Location : ILocationReadOnly, IEquatable<Location>
    {
        public IScale scale { get; set; }

        public Location()
        {
        }

        public IDictionary<string, string> Attributes => null;

        public System.Data.Entity.Spatial.DbGeometry VolumeShape { get; internal set; }

        private IShape2D _VolumeShape = null;
        public IShape2D VolumeGeometry
        {
            get
            {
                if (_VolumeShape == null)
                {
                    _VolumeShape = this.VolumeShape.WellKnownValue.WellKnownText.ParseWKT();
                    throw new NotImplementedException("IShape2D must be scaled to match units");
                    //_VolumeShape = _VolumeShape.Scale(scale);
                }

                return _VolumeShape;
            }

            set => _VolumeShape = value;
        }

        public System.Data.Entity.Spatial.DbGeometry MosaicShape { get; internal set; }

        private IShape2D _MosaicShape = null;
        public IShape2D MosaicGeometry
        {
            get
            {
                if (_MosaicShape == null)
                {
                    _MosaicShape = this.MosaicShape.WellKnownValue.WellKnownText.ParseWKT();
                    throw new NotImplementedException("IShape2D must be scaled to match units");
                    //_MosaicShape = _MosaicShape.Scale(scale);
                }

                return _MosaicShape;
            }

            set => _MosaicShape = value;
        }


        public ulong ID
        {
            get; internal set;
        }

        public bool IsUntraceable
        {
            get; private set;
        }

        public bool IsVericosityCap
        {
            get; private set;
        }

        public bool OffEdge
        {
            get; internal set;
        }

        public ulong ParentID
        {
            get; internal set;
        }

        public bool Terminal
        {
            get; internal set;
        }

        public long UnscaledZ => (long)this.Z;

        public double Z
        {
            get; internal set;
        }

        double ILocationReadOnly.Z => (double)UnscaledZ * scale.Z.Value;

        public string TagsXml => this.Tags;

        public string Tags
        {
            get; internal set;
        }

        LocationType _TypeCode;
        public LocationType TypeCode
        {
            get
            {
                return (LocationType)this._TypeCode;
            }
            internal set
            {
                _TypeCode = value;
            }
        }

        GridBox _BoundingBox = null;
        public GridBox BoundingBox
        {
            get
            {

                if (VolumeShape == null)
                    return null;

                if (_BoundingBox == null)
                {
                    GridRectangle bound_rect = VolumeShape.BoundingBox();
                    _BoundingBox = new GridBox(bound_rect, Z - (scale.Z.Value / 2.0), Z + (scale.Z.Value / 2.0));
                }

                return _BoundingBox;
            }
        }

        string ILocationReadOnly.VolumeGeometryWKT => VolumeShape.WellKnownValue.WellKnownText;

        IReadOnlyDictionary<string, string> ILocationReadOnly.Attributes => throw new NotImplementedException();

        public double? Width
        {
            get; internal set;
        }

        public string MosaicGeometryWKT
        {
            get; internal set;
        }

        public override string ToString()
        {
            return ID.ToString();
        }

        public bool Equals(ILocationReadOnly other)
        {
            if (other is null)
                return false;

            return other.ID.Equals(this.ID);
        }

        public bool Equals(Location other)
        {
            if (other is null)
                return false;

            return other.ID.Equals(ID);
        }
    }
}

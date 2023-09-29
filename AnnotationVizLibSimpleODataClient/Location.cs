﻿using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using UnitsAndScale;

namespace AnnotationVizLib.SimpleOData
{
    public class Location : ILocation, IEquatable<Location>
    {
        public IScale scale { get; set; }

        public Location()
        {
        }

        public IDictionary<string, string> Attributes => null;

        public System.Data.Entity.Spatial.DbGeometry VolumeShape { get; internal set; }

        private SqlGeometry _VolumeShape = null;
        public SqlGeometry Geometry
        {
            get
            {
                if (_VolumeShape == null)
                {
                    _VolumeShape = this.VolumeShape.ToSqlGeometry();
                    _VolumeShape = _VolumeShape.Scale(scale);
                }

                return _VolumeShape;
            }

            set
            {
                _VolumeShape = value;
            }
        }

        public System.Data.Entity.Spatial.DbGeometry MosaicShape { get; internal set; }

        private SqlGeometry _MosaicShape = null;
        public SqlGeometry MosaicGeometry
        {
            get
            {
                if (_MosaicShape == null)
                {
                    _MosaicShape = this.MosaicShape.ToSqlGeometry();
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

        public long UnscaledZ
        {
            get
            {
                return (long)this.Z;
            }
        }

        public double Z
        {
            get; internal set;
        }

        double ILocation.Z
        {
            get { return (double)UnscaledZ * scale.Z.Value; }
        }

        public string TagsXml
        {
            get { return this.Tags; }
        }

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

        GridBox _BoundingBox = default;
        public GridBox BoundingBox
        {
            get
            {

                if (VolumeShape == null)
                    return default;

                if (_BoundingBox == default)
                {
                    GridRectangle bound_rect = VolumeShape.BoundingBox();
                    _BoundingBox = new GridBox(bound_rect, Z - (scale.Z.Value / 2.0), Z + (scale.Z.Value / 2.0));
                }

                return _BoundingBox;
            }
        }

        public override string ToString()
        {
            return ID.ToString();
        }

        public bool Equals(ILocation other)
        {
            if (other is null)
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

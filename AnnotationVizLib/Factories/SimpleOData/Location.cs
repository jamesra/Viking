using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Types;
using ODataClient.ConnectomeDataModel;
using SqlGeometryUtils;
using Geometry;

namespace AnnotationVizLib.SimpleOData
{
    public class Location : ILocation
    {
        public Geometry.Scale scale { get; set; }

        public Location()
        {
        }

        public IDictionary<string, string> Attributes
        {
            get
            {
                return null;
            }
        }

        public System.Data.Entity.Spatial.DbGeometry VolumeShape { get; set; }

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
        

        public ulong ID
        {
            get; private set;
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
            get; private set;
        }

        public ulong ParentID
        {
            get; private set;
        }

        public bool Terminal
        {
            get; private set;
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
            get; private set;
        }

        double IGeometry.Z
        {
            get { return (double)UnscaledZ * scale.Z.Value; }
        }

        public string TagsXml
        {
            get { return this.Tags; }
        }

        public string Tags
        {
            get; private set;
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

        public override string ToString()
        {
            return ID.ToString();
        }
    }
}

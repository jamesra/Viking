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

    public class ODataLocationAdapter : ILocationReadOnly
    {
        private readonly Location loc;
        public readonly UnitsAndScale.IScale scale;

        public ODataLocationAdapter(Location l, UnitsAndScale.IScale scale)
        {
            if (l == null)
                throw new ArgumentNullException();

            if (scale == null)
                throw new ArgumentNullException();

            this.loc = l;
            this.scale = scale;
        }

        public IDictionary<string, string> Attributes => null;

        private IShape2D _VolumeShape = null;
        public IShape2D VolumeGeometry
        {
            get
            {
                if (_VolumeShape == null)
                {
                    _VolumeShape = loc.VolumeShape.Geometry.WellKnownText.ParseWKT();
                    throw new NotImplementedException("Geometry must be scaled to units");
                    //_VolumeShape = _VolumeShape.Scale(scale);
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

        GridBox _BoundingBox = default;
        public GridBox BoundingBox
        {
            get
            {
                if (_BoundingBox == default)
                {
                    GridRectangle bound_rect = VolumeGeometry.BoundingBox;
                    _BoundingBox = new GridBox(bound_rect, Z - scale.Z.Value, Z + scale.Z.Value);
                }

                return _BoundingBox;
            }
        }

        string ILocationReadOnly.VolumeGeometryWKT => loc.VolumeShape.Geometry.WellKnownText;

        IReadOnlyDictionary<string, string> ILocationReadOnly.Attributes
        {
            get { return loc.Attributes().ToDictionary(a => a.Name, a=>a.Value); }
        }

        public double? Width => loc.Width;

        public string MosaicGeometryWKT => loc.MosaicShape.Geometry.WellKnownText;

        public bool Equals(ILocationReadOnly other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.ID == this.ID)
                return true;

            return false;
        }

        public bool Equals(Location other)
        {
            if (other is null)
                return false;
            
            return other.ID.Equals((long)ID);
        }
    }
}

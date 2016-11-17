using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphLib;
using SqlGeometryUtils;
using Microsoft.SqlServer.Types;
using AnnotationVizLib.AnnotationService;
using Geometry;


namespace AnnotationVizLib
{   
    public class MorphologyNode : Node<ulong, MorphologyEdge>, IGeometry
    { 
        public ILocation Location = null;

        public ulong ID { get { return this.Location.ID; } }

        //Structure this node represents 
        public MorphologyGraph Graph;
        

        public MorphologyNode(ulong key, ILocation Location, MorphologyGraph parent)
            : base(key)
        {
            this.Graph = parent;
            this.Location = Location;
        }

        public SqlGeometry VolumeShape {
            get
            { return Location.VolumeShape; }
            set { Location.VolumeShape = value; }
        }
        
        public double Z { get { return Location.Z; }}

        public double UnscaledZ { get { return Location.UnscaledZ; } }

        public override string ToString()
        {
            return this.Key.ToString();
        }

        public GridBox BoundingBox
        {
            get
            {
                GridRectangle rect = VolumeShape.BoundingBox();
                GridVector3 botleft = new GridVector3(rect.Left, rect.Bottom, Z - Graph.SectionThickness / 2.0);
                GridVector3 topright = new GridVector3(rect.Right, rect.Top, Z + Graph.SectionThickness / 2.0);

                GridBox bbox = new GridBox(botleft, topright);
                return bbox;
            }
        }

        public GridVector3 Center
        {
            get
            {
                GridVector2 c = VolumeShape.Centroid();
                return new GridVector3(c.X, c.Y, Z);
            }
        }
    }
}

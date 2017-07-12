using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry.Meshing;
using Geometry;


namespace MorphologyMesh
{
    public enum ConnectionPortType
    {
        OPEN, //The port verticies are not connected in a circle.  This is used for 1D geometries (lines)
        CLOSED //The port verticies are connected in a circle.  This is used for 2D geometries (circles and polygons)
    };

    /// <summary>
    /// Describes the verticies available to connect two meshes together.
    /// Verticies should be listed in Counter-clockwise order
    /// </summary>
    public class ConnectionVerticies
    {
        public ConnectionPortType Type;

        /// <summary>
        /// Points on the external border
        /// </summary>
        public IIndexSet ExternalBorder;

        /// <summary>
        /// Verticies known to be internal to the annotation. Not on any internal or external border
        /// </summary>
        public IIndexSet InternalVerticies;

        /// <summary>
        /// Points on an internal border
        /// </summary>
        public IIndexSet[] InternalBorders;

        public ConnectionVerticies(long[] exteriorRing, long[] internalVerticies, ICollection<long[]> interiorRings)
        {
            this.Type = ConnectionPortType.CLOSED; //Cannot have internal verticies in an open port
            ExternalBorder = new IndexSet(exteriorRing);

            if (internalVerticies != null)
                InternalVerticies = new IndexSet(internalVerticies);
            else
                InternalVerticies = new IndexSet(new long[0]);

            if (InternalBorders != null)
                InternalBorders = interiorRings.Select(ir => new IndexSet(ir)).ToArray();
            else
                InternalBorders = new IIndexSet[0];
        }

        public ConnectionVerticies(IIndexSet exteriorRing, IIndexSet internalVerticies, IIndexSet[] interiorRings)
        {
            this.Type = ConnectionPortType.CLOSED; //Cannot have internal verticies in an open port
            ExternalBorder = exteriorRing;
            InternalVerticies = internalVerticies;

            if (internalVerticies != null)
                InternalVerticies = internalVerticies;
            else
                InternalVerticies = new IndexSet(new long[0]);

            if (interiorRings != null)
                InternalBorders = interiorRings;
            else
                InternalBorders = new IIndexSet[0];
        }

        public ConnectionVerticies(IIndexSet lineVerticies)
        {
            this.Type = ConnectionPortType.OPEN; //Cannot have internal verticies in an open port
            ExternalBorder = lineVerticies;
            InternalVerticies = new IndexSet(new long[0]);
            InternalBorders = new IIndexSet[0];
        }

        /// <summary>
        /// Add a constant to all index values
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ConnectionVerticies IncrementStartingIndex(int value)
        {
            IIndexSet external = ExternalBorder.IncrementStartingIndex(value);
            IIndexSet internalVerts = InternalVerticies.IncrementStartingIndex(value);
            IIndexSet[] internalSets = InternalBorders.Select(ib => ib.IncrementStartingIndex(value)).ToArray();

            ConnectionVerticies port = new ConnectionVerticies(external, internalVerts, internalSets);
            port.Type = this.Type;
            return port;
        }

        public int TotalVerticies
        {
            get
            {
                return ExternalBorder.Count + InternalBorders.Sum(ib => ib.Count) + InternalVerticies.Count;
            }
        }

        public Geometry.GridPolygon ToPolygon(DynamicRenderMesh mesh)
        {
            GridVector2[] externalBorder = this.ExternalBorder.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray();
            List<GridVector2[]> internalBorders = this.InternalBorders.Select(ib => ib.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray()).ToList();
            GridPolygon polygon = new GridPolygon(externalBorder, internalBorders);
            return polygon;
        }
    }

}

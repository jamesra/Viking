using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Linq;


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
        public Geometry.IIndexSet ExternalBorder;

        /// <summary>
        /// Verticies known to be internal to the annotation. Not on any internal or external border
        /// </summary>
        public Geometry.IIndexSet InternalVerticies;

        /// <summary>
        /// Points on an internal border
        /// </summary>
        public Geometry.IIndexSet[] InternalBorders;

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

            ConnectionVerticies port = new ConnectionVerticies(external, internalVerts, internalSets)
            {
                Type = this.Type
            };
            return port;
        }

        public int TotalVerticies
        {
            get
            {
                return ExternalBorder.Count + InternalBorders.Sum(ib => ib.Count) + InternalVerticies.Count;
            }
        }
        public Geometry.GridPolygon ToPolygon(IReadOnlyList<IVertex3D> Verticies)
        {
            System.Diagnostics.Debug.Assert(ExternalBorder.Max() < Verticies.Count);
#if DEBUG
            if (InternalBorders.Length > 0)
                System.Diagnostics.Debug.Assert(InternalBorders.Max(ib => ib.Max()) < Verticies.Count);
#endif

            GridVector2[] externalBorder = this.ExternalBorder.Select(i => Verticies[(int)i].Position.XY()).ToArray();
            externalBorder = externalBorder.EnsureClosedRing();
            List<GridVector2[]> internalBorders = this.InternalBorders.Select(ib => ib.Select(i => Verticies[(int)i].Position.XY()).ToArray().EnsureClosedRing()).ToList();
            GridPolygon polygon = new GridPolygon(externalBorder, internalBorders);
            return polygon;
        }

        public Geometry.GridPolygon ToPolygon(IMesh<Vertex3D> mesh)
        {
            System.Diagnostics.Debug.Assert(ExternalBorder.Max() < mesh.Verticies.Count);
#if DEBUG
            if(InternalBorders.Length > 0)
                System.Diagnostics.Debug.Assert(InternalBorders.Max(ib => ib.Max()) < mesh.Verticies.Count);
#endif

            GridVector2[] externalBorder = mesh[this.ExternalBorder].Select(v => v.Position.XY()).ToArray();
            externalBorder = externalBorder.EnsureClosedRing();
            List<GridVector2[]> internalBorders = this.InternalBorders.Select(ib => ib.Select(i => mesh.Verticies[(int)i].Position.XY()).ToArray().EnsureClosedRing()).ToList();
            GridPolygon polygon = new GridPolygon(externalBorder, internalBorders);
            return polygon;
        }

        public Geometry.GridPolygon ToPolygon(IMesh<IVertex2D> mesh)
        {
            System.Diagnostics.Debug.Assert(ExternalBorder.Max() < mesh.Verticies.Count);
#if DEBUG
            if (InternalBorders.Length > 0)
                System.Diagnostics.Debug.Assert(InternalBorders.Max(ib => ib.Max()) < mesh.Verticies.Count);
#endif

            GridVector2[] externalBorder = mesh[this.ExternalBorder].Select(v => v.Position).ToArray();
            externalBorder = externalBorder.EnsureClosedRing();
            List<GridVector2[]> internalBorders = this.InternalBorders.Select(ib => ib.Select(i => mesh.Verticies[(int)i].Position).ToArray().EnsureClosedRing()).ToList();
            GridPolygon polygon = new GridPolygon(externalBorder, internalBorders);
            return polygon;
        }

        public static ConnectionVerticies CreatePort(ICircle2D shape, long NumPointsAroundCircle)
        {
            ContinuousIndexSet ExternalBorder = new ContinuousIndexSet(0, NumPointsAroundCircle);
            //Add one internal point for the vertex at the center of the circle
            ContinuousIndexSet InternalPoints = new ContinuousIndexSet(NumPointsAroundCircle, 1);
            return new ConnectionVerticies(ExternalBorder, InternalPoints, null);
        }

        public static ConnectionVerticies CreatePort(IPolygon2D shape)
        {
            ContinuousIndexSet ExternalBorder = new ContinuousIndexSet(0, shape.ExteriorRing.Count - 1);

            ContinuousIndexSet[] InternalBorders = new ContinuousIndexSet[shape.InteriorRings.Count];

            int iStartVertex = shape.ExteriorRing.Count-1;
            for (int i = 0; i < shape.InteriorRings.Count; i++)
            {
                ICollection<IPoint2D> interiorRing = shape.InteriorRings.ElementAt(i);
                InternalBorders[i] = new ContinuousIndexSet(iStartVertex, interiorRing.Count - 1);
                iStartVertex += interiorRing.Count - 1; 
            }

            return new ConnectionVerticies(ExternalBorder, null, InternalBorders);
        }

        public static ConnectionVerticies CreatePort(IPolyLine2D shape)
        {
            ContinuousIndexSet ExternalBorder = new ContinuousIndexSet(0, shape.Points.Count);
            return new ConnectionVerticies(ExternalBorder);
        }

        public static ConnectionVerticies CreatePort(IPoint2D shape)
        {
            ContinuousIndexSet ExternalBorder = new ContinuousIndexSet(0, 1);
            return new ConnectionVerticies(ExternalBorder);
        }
    }

}

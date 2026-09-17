using Geometry;
using Geometry.Meshing;
using System;
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
    /// Vertices should be listed in Counter-clockwise order
    /// </summary>
    public class ConnectionVertices
    {
        public ConnectionPortType Type;

        /// <summary>
        /// Points on the external border
        /// </summary>
        public Geometry.IIndexSet ExternalBorder;

        /// <summary>
        /// Vertices known to be internal to the annotation. Not on any internal or external border
        /// </summary>
        public Geometry.IIndexSet InternalVerticies;

        /// <summary>
        /// Points on an internal border
        /// </summary>
        public Geometry.IIndexSet[] InternalBorders;

        public ConnectionVertices(long[] exteriorRing, long[] internalVerticies, ICollection<long[]> interiorRings)
        {
            this.Type = ConnectionPortType.CLOSED; //Cannot have internal verticies in an open port
            ExternalBorder = new IndexSet(exteriorRing);

            InternalVerticies = internalVerticies != null ? new IndexSet(internalVerticies) : new IndexSet([]);

            InternalBorders = InternalBorders != null ? [.. interiorRings.Select(ir => new IndexSet(ir))] : [];
        }

        public ConnectionVertices(IIndexSet exteriorRing, IIndexSet internalVerticies, IIndexSet[] interiorRings)
        {
            this.Type = ConnectionPortType.CLOSED; //Cannot have internal verticies in an open port
            ExternalBorder = exteriorRing;
            InternalVerticies = internalVerticies;

            InternalVerticies = internalVerticies != null ? internalVerticies : new IndexSet([]);

            InternalBorders = interiorRings != null ? interiorRings : [];
        }

        public ConnectionVertices(IIndexSet lineVerticies)
        {
            this.Type = ConnectionPortType.OPEN; //Cannot have internal verticies in an open port
            ExternalBorder = lineVerticies;
            InternalVerticies = new IndexSet([]);
            InternalBorders = [];
        }

        /// <summary>
        /// Add a constant to all index values
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ConnectionVertices IncrementStartingIndex(int value)
        {
            IIndexSet external = ExternalBorder.IncrementStartingIndex(value);
            IIndexSet internalVerts = InternalVerticies.IncrementStartingIndex(value);
            IIndexSet[] internalSets = [.. InternalBorders.Select(ib => ib.IncrementStartingIndex(value))];

            ConnectionVertices port = new(external, internalVerts, internalSets)
            {
                Type = this.Type
            };
            return port;
        }

        public int TotalVertices => ExternalBorder.Count + InternalBorders.Sum(ib => ib.Count) + InternalVerticies.Count;
        public Geometry.Polygon ToPolygon(IReadOnlyList<IVertex3D> Vertices)
        {
            System.Diagnostics.Debug.Assert(ExternalBorder.Max() < Vertices.Count);
#if DEBUG
            if (InternalBorders.Length > 0)
                System.Diagnostics.Debug.Assert(InternalBorders.Max(ib => ib.Max()) < Vertices.Count);
#endif

            Vector2[] externalBorder = [.. this.ExternalBorder.Select(i => Vertices[(int)i].Position.XY())];
            externalBorder = externalBorder.EnsureClosedRing();
            List<Vector2[]> internalBorders = [.. this.InternalBorders.Select(ib => ib.Select(i => Vertices[(int)i].Position.XY()).ToArray().EnsureClosedRing())];
            Polygon polygon = new(externalBorder, internalBorders);
            return polygon;
        }

        public Geometry.Polygon ToPolygon(IMesh<Vertex3D> mesh)
        {
            System.Diagnostics.Debug.Assert(ExternalBorder.Max() < mesh.Vertices.Count);
#if DEBUG
            if(InternalBorders.Length > 0)
                System.Diagnostics.Debug.Assert(InternalBorders.Max(ib => ib.Max()) < mesh.Vertices.Count);
#endif

            Vector2[] externalBorder = [.. mesh[this.ExternalBorder].Select(v => v.Position.XY())];
            externalBorder = externalBorder.EnsureClosedRing();
            List<Vector2[]> internalBorders = [.. this.InternalBorders.Select(ib => ib.Select(i => mesh.Vertices[(int)i].Position.XY()).ToArray().EnsureClosedRing())];
            Polygon polygon = new(externalBorder, internalBorders);
            return polygon;
        }

        public Geometry.Polygon ToPolygon(IMesh<IVertex2D> mesh)
        {
            System.Diagnostics.Debug.Assert(ExternalBorder.Max() < mesh.Vertices.Count);
#if DEBUG
            if (InternalBorders.Length > 0)
                System.Diagnostics.Debug.Assert(InternalBorders.Max(ib => ib.Max()) < mesh.Vertices.Count);
#endif

            Vector2[] externalBorder = [.. mesh[this.ExternalBorder].Select(v => v.Position)];
            externalBorder = externalBorder.EnsureClosedRing();
            List<Vector2[]> internalBorders = [.. this.InternalBorders.Select(ib => ib.Select(i => mesh.Vertices[(int)i].Position).ToArray().EnsureClosedRing())];
            Polygon polygon = new(externalBorder, internalBorders);
            return polygon;
        }

        public static ConnectionVertices CreatePort(ICircle2D shape, long NumPointsAroundCircle)
        {
            ContinuousIndexSet ExternalBorder = new(0, NumPointsAroundCircle);
            //Add one internal point for the vertex at the center of the circle
            ContinuousIndexSet InternalPoints = new(NumPointsAroundCircle, 1);
            return new ConnectionVertices(ExternalBorder, InternalPoints, null);
        }

        public static ConnectionVertices CreatePort(IPolygon2D shape)
        {
            ContinuousIndexSet ExternalBorder = new(0, shape.ExteriorRing.Count - 1);

            ContinuousIndexSet[] InternalBorders = new ContinuousIndexSet[shape.InteriorRings.Count];

            int iStartVertex = shape.ExteriorRing.Count - 1;
            for (int i = 0; i < shape.InteriorRings.Count; i++)
            {
                ICollection<IPoint2D> interiorRing = shape.InteriorRings.ElementAt(i);
                InternalBorders[i] = new ContinuousIndexSet(iStartVertex, interiorRing.Count - 1);
                iStartVertex += interiorRing.Count - 1;
            }

            return new ConnectionVertices(ExternalBorder, null, InternalBorders);
        }

        public static ConnectionVertices CreatePort(IPolyLine2D shape)
        {
            ContinuousIndexSet ExternalBorder = new(0, shape.Points.Count);
            return new ConnectionVertices(ExternalBorder);
        }

        public static ConnectionVertices CreatePort(IPoint2D shape)
        {
            ContinuousIndexSet ExternalBorder = new(0, 1);
            return new ConnectionVertices(ExternalBorder);
        }
    }

}

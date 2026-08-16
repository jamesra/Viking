using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphologyMesh
{
    public enum RegionType
    {
        EXPOSED,
        HOLE,
        INVAGINATION,
        UNTILED, //A Region that covers an untiled area of a polygon
    }

    public static class RegionTypeExtensions
    {
        private static readonly Dictionary<RegionType, SortedSet<RegionType>> ValidRegionPairings = new()
        {
            { RegionType.EXPOSED, new SortedSet<RegionType>{RegionType.EXPOSED} },
            { RegionType.HOLE, new SortedSet<RegionType> {RegionType.HOLE, RegionType.INVAGINATION } },
            { RegionType.INVAGINATION, new SortedSet<RegionType> {RegionType.HOLE, RegionType.INVAGINATION } },
            { RegionType.UNTILED, new SortedSet<RegionType> {} },
        };

        /// <summary>
        /// Return true if these region types could be connected
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool IsValidPair(this RegionType r, RegionType other) => ValidRegionPairings[r].Contains(other);
    }

    [Flags]
    public enum EdgeType
    {
        /// <summary>
        /// An edge of unknown type
        /// </summary>
        UNKNOWN = 0x00,
        //VALID = 0x01,   //An edge that could be a valid slice chord

        /// <summary>
        /// An edge that cannot be part of the final surface
        /// </summary>
        INVALID = 1 << 31,

        /// <summary>
        /// An edge that would be valid, but the orientation is wrong.  For example, the line has solid material to the left on one vertex and the right on another 
        /// </summary>
        FLIPPED_DIRECTION = 1 << 30,

        //INVALID Types:
        /// <summary>
        /// An edge that connects two verticies on the same shape
        /// </summary>
        FLAT = 1 << 29,
        /// <summary>
        /// An edge that crosses empty space, not a valid surface edge                
        /// </summary>
        FLYING = 1 << 28,
        /// <summary>
        /// An edge that runs between two sections but is known to be inside the mesh
        /// </summary>
        INTERNAL = 1 << 27,
        /// <summary>
        /// An edge that spans between the same shape outside of that shape, but passes over a shape on an adjacent section
        /// </summary>
        INVAGINATION = 1 << 26,
        /// <summary>
        /// An edge that spans a hole in a shape
        /// </summary>
        HOLE = 1 << 25,
        /// <summary>
        /// An edge that crosses an untiled region of a polygon on an adjacent section
        /// </summary>
        UNTILED = 1 << 24,

        //VALID Types
        /// <summary>
        /// An edge along the contour, part of either the exterior or inner ring of the input shapes
        /// </summary>
        CONTOUR = 1 << 2,
        /// <summary>
        /// An edge that crosses from one Z-LEVEL to another and is part of the surface
        /// </summary>
        SURFACE = 1 << 3,
        /// <summary>
        /// An edge that is connected to a non-polygon vertex that we added to the mesh
        /// </summary>
        ARTIFICIAL = 1 << 4,
        /// <summary>
        /// An edge that shares XY coordinates with a vertex on a shape on an adjacent section
        /// </summary>
        CORRESPONDING = 1 << 5,
        /// <summary>
        /// An edge that was added as part of an untiled regions medial axis and is known to be part of the final mesh
        /// </summary>
        MEDIALAXIS = 1 << 6,
        /// <summary>
        /// An edge that was added as part of an untiled region that runs from a contour boundary to the medial axis.
        /// This implies it is on the surface and is part of the final mesh
        /// </summary>
        CONTOUR_TO_MEDIALAXIS = 1 << 7,

        /// <summary>
        /// An edge that runs from the countour edge to a polyline that may be external or internal to the countour
        /// </summary>
        COUNTOUR_TO_POLYLINE = 1 << 8,
    }

    public static class EdgeTypeExtensions
    {
        public static bool IsValid(this EdgeType edge)
        {
            const EdgeType ValidMask = EdgeType.CONTOUR | EdgeType.SURFACE | EdgeType.ARTIFICIAL | EdgeType.CORRESPONDING | EdgeType.MEDIALAXIS | EdgeType.CONTOUR_TO_MEDIALAXIS;
            return (edge & ValidMask) > 0;
        }

        public static bool CouldBeSliceChord(this EdgeType edge) => edge.IsValid() || edge == EdgeType.FLYING;

        public static EdgeType GetEdgeType(this Vector2 midpoint, IShape2D A, IShape2D B)
        {
            if (A is Polygon apoly && B is Polygon bpoly)
                return GetEdgeType(midpoint, apoly, bpoly);

            if (A is Polyline aline && B is Polyline bline)
                return EdgeType.FLYING; //Line covers empty space, could be on surface 

            if (A is Polygon && B is Polyline)
                return EdgeType.FLYING; //Line covers empty space, could be on surface

            if (A is Polyline && B is Polygon)
                return EdgeType.FLYING; //Line covers empty space, could be on surface

            throw new ArgumentException("Unhandled case in GetEdgeType");
        }

        public static EdgeType GetEdgeType(this Vector2 midpoint, Polygon A, Polygon B)
        {
            bool midInA = A.Contains(midpoint);
            bool midInB = B.Contains(midpoint);

            if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
            {
                if (midInA && midInB)
                    return EdgeType.INTERNAL; //Line is inside the final mesh. Cannot be on surface.
                else
                {
                    return EdgeType.FLYING; //Line covers empty space, could be on surface
                }
            }
            else //Midpoint in one or the other polygon, but not both
            {
                return EdgeType.SURFACE;
            }
        }

        public static EdgeType GetEdgeType(this LineSegment line, Polygon A, Polygon B)
        {
            Vector2 midpoint = line.PointAlongLine(0.5);
            // bool midInA = A.Contains(midpoint);
            // bool midInB = B.Contains(midpoint);
            bool lineCrossesA = line.Crosses(A);
            bool lineCrossesB = line.Crosses(B);
            // bool lineInA = A.Contains(line);
            // bool lineInB = B.Contains(line);

            if (!(lineCrossesA ^ lineCrossesB)) //Midpoint in both or neither polygon. Line may be on exterior surface
            {
                if (lineCrossesA && lineCrossesB)
                    return EdgeType.INTERNAL; //Line is inside the final mesh. Cannot be on surface.
                else
                {
                    return EdgeType.FLYING; //Line covers empty space, could be on surface
                }
            }
            else //Midpoint in one or the other polygon, but not both
            {
                return EdgeType.SURFACE;
            }
        }

        public static EdgeType GetEdgeType(this LineSegment line, IShape2D A, IShape2D B)
        {
            Vector2 midpoint = line.PointAlongLine(0.5);
            return GetEdgeType(midpoint, A, B);
        }


        /// <summary>
        /// Determine the edge type when comparing polyline to polyline chords
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="Polylines"></param>
        /// <param name="midpoint"></param>
        /// <returns></returns>
        public static EdgeType GetEdgeType(PolylineIndex APoly, PolylineIndex BPoly, IReadOnlyList<Polyline> Polylines, Vector2 midpoint)
        {
            Polyline A = Polylines[APoly.ShapeIndex];
            Polyline B = Polylines[BPoly.ShapeIndex];

            LineSegment chord = new(A[APoly], B[BPoly]);

            if (APoly.ShapeIndex != BPoly.ShapeIndex)
            {
                var results = chord.Intersections([.. Polylines.SelectMany(p => p.LineSegments)], EndpointsOnLineDoNotIntersect: true, out var Intersections);
                if (results.Any())
                {
                    return EdgeType.INVALID;
                }
                else
                {
                    return EdgeType.SURFACE;
                }
            }
            else
            {
                if (APoly.AreAdjacent(BPoly))
                {
                    return EdgeType.CONTOUR;
                }
                else
                {
                    return EdgeType.INVALID;
                }
            }
        }

        public static EdgeType GetEdgeType(IShapeIndex A, IShapeIndex B, IReadOnlyList<IShape2D> shapes, Vector2 midpoint)
        {
            if (A is PolygonIndex iPolyA && B is PolygonIndex iPolyB)
                return GetEdgeType(iPolyA, iPolyB, shapes, midpoint);
            if (A is PolylineIndex iLineA && B is PolylineIndex iLineB)
                return GetEdgeType(iLineA, iLineB, shapes, midpoint);

            throw new ArgumentException("Unhandled case in GetEdgeType");
        }

        /// <summary>
        /// Determines the type of edge.
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="Polygons"></param>
        /// <param name="midpoint"></param>
        /// <returns></returns>
        public static EdgeType GetEdgeType(PolygonIndex APoly, PolygonIndex BPoly, IReadOnlyList<IShape2D> Shapes, Vector2 midpoint)
        {
            if (Shapes[APoly.ShapeIndex] is not Polygon A)
                throw new ArgumentException($"Shape #{APoly.ShapeIndex} must be a polygon", nameof(APoly));
            if (Shapes[BPoly.ShapeIndex] is not Polygon B)
                throw new ArgumentException($"Shape #{BPoly.ShapeIndex} must be a polygon", nameof(BPoly));

            if (APoly.ShapeIndex != BPoly.ShapeIndex)
            {
                ShapeRelation midInA = A.GetRelation(midpoint);
                ShapeRelation midInB = B.GetRelation(midpoint);

                if (!(midInA == ShapeRelation.None ^ midInB == ShapeRelation.None)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    if (midInA == ShapeRelation.Contained && midInB == ShapeRelation.Contained)
                        return EdgeType.INTERNAL; //Line is inside the final mesh. Cannot be on surface.
                    else if (midInA == ShapeRelation.Touching && midInB == ShapeRelation.Touching)
                    {
                        return EdgeType.CONTOUR;
                    }
                    else
                    {
                        //return EdgeType.FLYING; //Line covers empty space, could be on surface
                        LineSegment segment = new(APoly.Point(A), BPoly.Point(B));
                        bool LineIntersectsAnyOtherPoly = Shapes.Where((p, iP) => iP != APoly.ShapeIndex && iP != BPoly.ShapeIndex).Any(p => p.GetRelation(segment) != ShapeRelation.None);
                        if (!LineIntersectsAnyOtherPoly)
                            return EdgeType.FLYING;
                        else
                        {
                            return EdgeType.UNTILED;
                        }
                    }
                }
                else //Midpoint in one or the other polygon, but not both
                {
                    /*var APoint = APoly.Point(Shapes);
                    var BPoint = BPoly.Point(Shapes);

                    bool A_Is_Corresponding = A.IsVertex(APoint) && B.IsVertex(APoint);
                    bool B_Is_Corresponding = A.IsVertex(BPoint) && B.IsVertex(BPoint);
                    */

                    if (APoly.IsInner ^ BPoly.IsInner) //One or the other is an interior polygon, but not both
                    {
                        if (A.InteriorPolygonContains(midpoint) ^ B.InteriorPolygonContains(midpoint))
                        {
                            //Verify the line is not exactly over the contour line of a corresponding edge
                            /************
                             Not considering Corresponding verticies always flying when drawing an edge from an exterior to interior polygon was a change that
                             unexpectedly fixed creating clean meshes for that same test case of an interior hole overlapping an adjacent exterior segment.
                             
                            if(A.IsVertex(BPoly.Point(Shapes)) || B.IsVertex(APoly.Point(Shapes)))
                            {
                                //This means we are connecting to a corresponding vertex/edge.  
                                //return EdgeType.FLYING;
                            }
                            */

                            //Include in port.
                            //Line runs from exterior ring to the near side of an overlapping interior hole
                            return EdgeType.SURFACE;
                        }
                        else //Find out if the midpoint is contained by the same polygon with the inner polygon
                        {
                            if ((!(midInA == ShapeRelation.None) && APoly.IsInner) || (!(midInB == ShapeRelation.None) && BPoly.IsInner))
                            {
                                return EdgeType.SURFACE;// lineViews[i].Color = Color.Gold;
                            }
                            else
                            {
                                return EdgeType.INVALID; //Not sure if this is correct.  Never saw it in testing. //lineViews[i].Color = Color.Pink;
                            }
                        }
                    }
                    else
                    {
                        return EdgeType.SURFACE;
                    }
                }
            }
            else if (APoly.ShapeIndex == BPoly.ShapeIndex)
            {
                if (PolygonIndex.IsBorderLine(APoly, BPoly, A))
                {
                    //Line is part of the border, either internal or external
                    return EdgeType.CONTOUR;
                }

                if (APoly.IsInner ^ BPoly.IsInner) //Spans from inner to outer ring
                {
                    bool LineIntersectsAnyOtherPoly = Shapes.Where((p, iP) => iP != APoly.ShapeIndex).Any(p => p.Contains(midpoint));
                    bool midInA = A.Contains(midpoint);
                    if (LineIntersectsAnyOtherPoly)
                    {
                        //Line passes over the other cell.
                        return EdgeType.INVALID;

                    }
                    else
                    {
                        //Line does not pass through solid space
                        return EdgeType.FLAT;
                    }

                }
                else if (APoly.IsInner && BPoly.IsInner)
                {
                    if (APoly.InnerShapeIndex == BPoly.InnerShapeIndex)
                    {
                        return EdgeType.HOLE;
                    }
                    else //Edge spans from one inner polygon to another
                    {
                        bool LineIntersectsAnyOtherPoly = Shapes.Where((p, iP) => iP != APoly.ShapeIndex).Any(p => p.Contains(midpoint));
                        if (LineIntersectsAnyOtherPoly)
                        {
                            return EdgeType.INVALID;
                        }
                        else
                        {
                            return EdgeType.FLAT;
                        }
                    }
                }
                else //Both points are on outer ring of one polygon
                {
                    bool LineIntersectsAnyOtherPoly = Shapes.Where((p, iP) => iP != APoly.ShapeIndex).Any(p => p.Contains(midpoint));
                    bool midInA = A.Contains(midpoint);

                    if (midInA)
                    {
                        if (LineIntersectsAnyOtherPoly)
                        {
                            return EdgeType.INVALID;
                        }
                        else
                        {
                            return EdgeType.FLAT;
                        }
                    }

                    else
                    {
                        return EdgeType.INVAGINATION;
                    }
                }
            }

            throw new ArgumentException("Unhandled case in IsLineOnSurface");
        }

        /// <summary>
        /// Determines the type of edge.
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="Polygons"></param>
        /// <param name="midpoint"></param>
        /// <returns></returns>
        public static EdgeType GetEdgeType(PolylineIndex ALine, PolylineIndex BLine, IReadOnlyList<IShape2D> Shapes, Vector2 midpoint)
        {
            if (Shapes[ALine.ShapeIndex] is not Polyline A)
                throw new ArgumentException($"Shape #{ALine.ShapeIndex} must be a polyline", nameof(Shapes));
            if (Shapes[BLine.ShapeIndex] is not Polyline B)
                throw new ArgumentException($"Shape #{BLine.ShapeIndex} must be a polyline", nameof(Shapes));

            if (ALine.ShapeIndex != BLine.ShapeIndex)
            {
                //return EdgeType.FLYING; //Line covers empty space, could be on surface
                LineSegment segment = new(ALine.Point(A), BLine.Point(B));
                bool LineIntersectsAnyOtherPoly = Shapes.Where((p, iP) => iP != ALine.ShapeIndex && iP != BLine.ShapeIndex).Any(p => p.GetRelation(segment) != ShapeRelation.None);
                if (!LineIntersectsAnyOtherPoly)
                    return EdgeType.INVALID;
                else
                {
                    return EdgeType.SURFACE;
                }
            }
            else if (ALine.ShapeIndex == BLine.ShapeIndex)
            {
                return EdgeType.INVALID;
            }

            throw new ArgumentException("Unhandled case in IsLineOnSurface");
        }

        /// <summary>
        /// Measure the difference in angle between the normals of two verticies 
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static double Orientation(this IShapeIndex A, IShapeIndex B, IReadOnlyList<IShape2D> Shapes)
        {
            Vector2 AO = A.GetOrientation(Shapes);
            Vector2 BO = B.GetOrientation(Shapes);

            //If the normals are more than 90 degrees apart then we consider them to have different orientations
            double arcAngle = Vector2.ArcAngle(Vector2.Zero, AO, BO);
            if (A.IsInner ^ B.IsInner)
            {
                if (arcAngle < 0)
                    arcAngle += Math.PI;
                else
                    arcAngle -= Math.PI;
            }

            return arcAngle;
        }


        public static bool OrientationsAreMatched(IShapeIndex A, IShapeIndex B, IReadOnlyList<IShape2D> Shapes)
        {
            double arcAngle = Orientation(A, B, Shapes);
            return Math.Abs(arcAngle) < Math.PI / 2.0;

            /*
            Vector2 p1 = APoly.Point(Shapes);
            Vector2 p2 = BPoly.Point(Shapes);

            Vector2[] adjacent1 = APoly.ConnectedVertices(Shapes);
            LineSegment ALine = new LineSegment(adjacent1[0], adjacent1[1]);

            Vector2[] adjacent2 = BPoly.ConnectedVertices(Shapes);
            LineSegment BLine = new LineSegment(adjacent2[0], adjacent2[1]);

            //If the normals are more than 90 degrees apart then we consider them to have different orientations
            double arcAngle = Vector2.ArcAngle(Vector2.Zero, ALine.Normal, BLine.Normal);
            bool AngleMatched = Math.Abs(arcAngle) < Math.PI / 2.0;
            if (APoly.IsInner ^ BPoly.IsInner)
            {
                AngleMatched = !AngleMatched;
            }

            return AngleMatched;
            */
        }

        /// <summary>
        /// Determines the edge type for two verticies that are both on a contour
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="midpoint"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static EdgeType GetContourEdgeTypeWithOrientation(this MorphRenderMesh mesh, PolygonIndex A, PolygonIndex B, Vector2? midpoint = new Vector2?())
        {
            if (!midpoint.HasValue)
            {
                midpoint = ((mesh[A].Position + mesh[B].Position) / 2.0).XY();
            }

            EdgeType type = GetContourEdgeTypeWithOrientation(A, B, mesh.Shapes, midpoint.Value);
            return type;
        }

        /// <summary>
        /// Determines the edge type for two verticies that are both on a contour
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="midpoint"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static EdgeType GetContourEdgeTypeWithOrientation(PolygonIndex APoly, PolygonIndex BPoly, IReadOnlyList<IShape2D> Shapes, Vector2 midpoint)
        {
            EdgeType type = GetEdgeType(APoly, BPoly, Shapes, midpoint);
            if ((type.IsValid() &&
               type != EdgeType.CONTOUR))
            {
                bool OrientationsMatch = OrientationsAreMatched(APoly, BPoly, Shapes);

                if (!OrientationsMatch)
                {
                    type = EdgeType.FLIPPED_DIRECTION;
                }
            }

            return type;
        }

        /// <summary>
        /// Determines the edge type for any two verticies in the mesh
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="Shapes"></param>
        /// <param name="midpoint"></param>
        /// <returns></returns>
        public static EdgeType GetEdgeTypeWithOrientation(MorphMeshVertex A, MorphMeshVertex B, IReadOnlyList<IShape2D> Shapes, Vector2? midpoint = new Vector2?())
        {
            if (A.Type == VertexOrigin.CONTOUR && B.Type == VertexOrigin.CONTOUR)
            {
                if (!midpoint.HasValue)
                {
                    midpoint = ((A.Position + B.Position) / 2.0).XY();
                }

                return GetContourEdgeTypeWithOrientation((PolygonIndex)A.ShapeIndex, (PolygonIndex)B.ShapeIndex, Shapes, midpoint.Value);
            }
            else if ((A.Type == VertexOrigin.CONTOUR && B.Type == VertexOrigin.MEDIALAXIS) ||
                    (B.Type == VertexOrigin.CONTOUR && A.Type == VertexOrigin.MEDIALAXIS))
            {
                return EdgeType.CONTOUR_TO_MEDIALAXIS;
            }
            else
            {
                return EdgeType.MEDIALAXIS;
            }
        }

        public static EdgeType GetEdgeTypeWithOrientation(this MorphRenderMesh mesh, MorphMeshVertex A, MorphMeshVertex B, Vector2? midpoint = new Vector2?()) => GetEdgeTypeWithOrientation(A, B, mesh.Shapes, midpoint);

        public static EdgeType GetEdgeTypeWithOrientation(this MorphRenderMesh mesh, int iA, int iB, Vector2? midpoint = new Vector2?()) => GetEdgeTypeWithOrientation(mesh[iA], mesh[iB], mesh.Shapes, midpoint);

    }
}

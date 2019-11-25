using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

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
        private static Dictionary<RegionType, RegionType[]> ValidRegionPairings = new Dictionary<RegionType, RegionType[]>()
        {
            { RegionType.EXPOSED, new RegionType[]{RegionType.EXPOSED} },
            { RegionType.HOLE, new RegionType[] {RegionType.HOLE, RegionType.INVAGINATION } },
            { RegionType.INVAGINATION, new RegionType[] {RegionType.HOLE, RegionType.INVAGINATION } },
            { RegionType.UNTILED, new RegionType[] {} },
        };

        /// <summary>
        /// Return true if these region types could be connected
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool IsValidPair(this RegionType r, RegionType other)
        {
            return ValidRegionPairings[r].Contains(other);
        }
    }

    [Flags]
    public enum EdgeType
    {
        UNKNOWN = 0x00,
        //VALID = 0x01,   //An edge that could be a valid slice chord

        INVALID = 1 << 31, //An edge that cannot be part of the final surface
        FLIPPED_DIRECTION = 1 << 30, //An edge that would be valid, but the orientation is wrong.  For example, the line has solid material to the left on one vertex and the right on another 

        //INVALID Types:
        FLAT = 1 << 29, //An edge that connects two verticies on the same shape
        FLYING = 1 << 28, //An edge that crosses empty space, not a valid surface edge                
        INTERNAL = 1 << 27, //An edge that runs between two sections but is known to be inside the mesh
        INVAGINATION = 1 << 26, //An edge that spans between the same shape outside of that shape, but passes over a shape on an adjacent section
        HOLE = 1 << 25, //An edge that spans a hole in a shape
        UNTILED = 1 << 24, //An edge that crosses an untiled region of a polygon on an adjacent section

        //VALID Types
        CONTOUR = 1 << 2, //An edge along the contour, part of either the exterior or inner ring
        SURFACE = 1 << 3, //An edge that crosses from one Z-LEVEL to another and is part of the surface
        ARTIFICIAL = 1 << 4, //An edge that is connected to a non-polygon vertex that we added to the mesh
        CORRESPONDING = 1 << 5,  //An edge that shares XY coordinates with a vertex on a shape on an adjacent section
        MEDIALAXIS = 1 << 6, //An edge that was added as part of an untiled regions medial axis and is known to be part of the final mesh
        CONTOUR_TO_MEDIALAXIS = 1 << 7, //An edge that was added as part of an untiled region that runs from a contour boundary to the medial axis and is known to be part of the final mesh
    }

    public static class EdgeTypeExtensions
    {
        public static bool IsValid(this EdgeType edge)
        {
            const EdgeType ValidMask = EdgeType.CONTOUR | EdgeType.SURFACE | EdgeType.ARTIFICIAL | EdgeType.CORRESPONDING | EdgeType.MEDIALAXIS | EdgeType.CONTOUR_TO_MEDIALAXIS;
            return (edge & ValidMask) > 0;
        }

        public static bool CouldBeSliceChord(this EdgeType edge)
        {
            return edge.IsValid() || edge == EdgeType.FLYING;
        }

        public static EdgeType GetEdgeType(this GridVector2 midpoint, GridPolygon A, GridPolygon B)
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

        public static EdgeType GetEdgeType(this GridLineSegment line, GridPolygon A, GridPolygon B)
        {
            GridVector2 midpoint = line.PointAlongLine(0.5);
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
        
        /// <summary>
        /// Determines the type of edge.
        /// </summary>
        /// <param name="APoly"></param>
        /// <param name="BPoly"></param>
        /// <param name="Polygons"></param>
        /// <param name="midpoint"></param>
        /// <returns></returns>
        public static EdgeType GetEdgeType(PointIndex APoly, PointIndex BPoly, IReadOnlyList<GridPolygon> Polygons, GridVector2 midpoint)
        {
            GridPolygon A = Polygons[APoly.iPoly];
            GridPolygon B = Polygons[BPoly.iPoly];

            if (APoly.iPoly != BPoly.iPoly)
            {
                bool midInA = A.Contains(midpoint);
                bool midInB = B.Contains(midpoint);

                if (!(midInA ^ midInB)) //Midpoint in both or neither polygon. Line may be on exterior surface
                {
                    if (midInA && midInB)
                        return EdgeType.INTERNAL; //Line is inside the final mesh. Cannot be on surface.
                    else
                    {
                        //return EdgeType.FLYING; //Line covers empty space, could be on surface
                        bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly && iP != BPoly.iPoly).Any(p => p.Contains(midpoint));
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
                    /*var APoint = APoly.Point(Polygons);
                    var BPoint = BPoly.Point(Polygons);

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
                             
                            if(A.IsVertex(BPoly.Point(Polygons)) || B.IsVertex(APoly.Point(Polygons)))
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
                            if ((midInA && APoly.IsInner) || (midInB && BPoly.IsInner))
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
            else if (APoly.iPoly == BPoly.iPoly)
            {


                if (PointIndex.IsBorderLine(APoly, BPoly, Polygons[APoly.iPoly]))
                {
                    //Line is part of the border, either internal or external
                    return EdgeType.CONTOUR;
                }

                if (APoly.IsInner ^ BPoly.IsInner) //Spans from inner to outer ring
                {
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
                    bool midInA = A.Contains(midpoint);
                    if (LineIntersectsAnyOtherPoly)
                    {
                        //Line passes over the other cell.  So
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
                    if (APoly.iInnerPoly == BPoly.iInnerPoly)
                    {
                        return EdgeType.HOLE;
                    }
                    else //Edge spans from one inner polygon to another
                    {
                        bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
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
                    bool LineIntersectsAnyOtherPoly = Polygons.Where((p, iP) => iP != APoly.iPoly).Any(p => p.Contains(midpoint));
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


        public static double Orientation(this PointIndex APoly, PointIndex BPoly, IReadOnlyList<GridPolygon> Polygons)
        {
            GridVector2 p1 = APoly.Point(Polygons);
            GridVector2 p2 = BPoly.Point(Polygons);

            GridVector2[] adjacent1 = APoly.ConnectedVerticies(Polygons);
            GridLineSegment ALine = new GridLineSegment(adjacent1[0], adjacent1[1]);

            GridVector2[] adjacent2 = BPoly.ConnectedVerticies(Polygons);
            GridLineSegment BLine = new GridLineSegment(adjacent2[0], adjacent2[1]);

            //If the normals are more than 90 degrees apart then we consider them to have different orientations
            double arcAngle = GridVector2.ArcAngle(GridVector2.Zero, ALine.Normal, BLine.Normal);
            if (APoly.IsInner ^ BPoly.IsInner)
            {
                if(arcAngle < 0)
                    arcAngle += Math.PI;
                else
                    arcAngle -= Math.PI;
            }

            return arcAngle;
        }

        public static bool OrientationsAreMatched(PointIndex APoly, PointIndex BPoly, IReadOnlyList<GridPolygon> Polygons)
        {
            double arcAngle = Orientation(APoly, BPoly, Polygons);
            return Math.Abs(arcAngle) < Math.PI / 2.0;

            /*
            GridVector2 p1 = APoly.Point(Polygons);
            GridVector2 p2 = BPoly.Point(Polygons);

            GridVector2[] adjacent1 = APoly.ConnectedVerticies(Polygons);
            GridLineSegment ALine = new GridLineSegment(adjacent1[0], adjacent1[1]);

            GridVector2[] adjacent2 = BPoly.ConnectedVerticies(Polygons);
            GridLineSegment BLine = new GridLineSegment(adjacent2[0], adjacent2[1]);

            //If the normals are more than 90 degrees apart then we consider them to have different orientations
            double arcAngle = GridVector2.ArcAngle(GridVector2.Zero, ALine.Normal, BLine.Normal);
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
        public static EdgeType GetContourEdgeTypeWithOrientation(PointIndex APoly, PointIndex BPoly, IReadOnlyList<GridPolygon> Polygons, GridVector2 midpoint)
        {
            EdgeType type = GetEdgeType(APoly, BPoly, Polygons, midpoint); 
            if((type.IsValid() &&
               type != EdgeType.CONTOUR))
            {
                bool OrientationsMatch = OrientationsAreMatched(APoly, BPoly, Polygons);
                

                if(!OrientationsMatch)
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
        /// <param name="Polygons"></param>
        /// <param name="midpoint"></param>
        /// <returns></returns>
        public static EdgeType GetEdgeTypeWithOrientation(MorphMeshVertex A, MorphMeshVertex B, IReadOnlyList<GridPolygon> Polygons, GridVector2? midpoint=new GridVector2?())
        {
            if (A.Type == VertexOrigin.CONTOUR && B.Type == VertexOrigin.CONTOUR)
            {
                if(!midpoint.HasValue)
                {
                    midpoint = ((A.Position + B.Position) / 2.0).XY();
                }

                return GetContourEdgeTypeWithOrientation(A.PolyIndex.Value, B.PolyIndex.Value, Polygons, midpoint.Value);
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

        public static EdgeType GetEdgeTypeWithOrientation(this MorphRenderMesh mesh, MorphMeshVertex A, MorphMeshVertex B, GridVector2? midpoint = new GridVector2?())
        {
            return  GetEdgeTypeWithOrientation(A, B, mesh.Polygons, midpoint); 
        }

        public static EdgeType GetEdgeTypeWithOrientation(this MorphRenderMesh mesh, int iA, int iB, GridVector2? midpoint = new GridVector2?())
        {
            return GetEdgeTypeWithOrientation(mesh[iA], mesh[iB], mesh.Polygons, midpoint);
        }

    }
}

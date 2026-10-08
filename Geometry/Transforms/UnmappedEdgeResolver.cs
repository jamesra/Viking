using System.Collections.Generic;
using System.Diagnostics;

namespace Geometry.Transforms
{
    /// <summary>
    /// Recovers mappable area when composing transforms. When a control point of the warping transform (A to B) falls
    /// outside the fixed discrete transform (B to C) it is dropped. Each edge from that point to a neighbour that does map is
    /// cut where it crosses B to C's convex hull, and the crossing becomes a new point, so the composed transform still
    /// reaches the edge of B to C.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="TriangulationTransform.Transform"/> once per unmapped point. Thread safe as long as the two
    /// transforms are only read; <paramref name="output"/> in <see cref="AddHullCrossings"/> must not be shared between threads.
    /// </remarks>
    internal static class UnmappedEdgeResolver
    {
        /// <summary>
        /// Adds a point to <paramref name="output"/> for every edge from <paramref name="iPoint"/> (a point the fixed
        /// transform could not map) to a mappable neighbour that crosses the fixed transform's convex hull.
        /// </summary>
        public static void AddHullCrossings(int iPoint, IControlPointTriangulation warpingTransform, IDiscreteTransform fixedTransform, List<MappingVector2> output)
        {
            //If we could not map a point we need to test each edge connecting this point to other points to see if the edge intersects the fixed transform boundaries
            MappingVector2 UnmappedPoint = warpingTransform.MapPoints[iPoint];

            List<int> MovingEdgeIndicies = warpingTransform.Edges[iPoint];

            //Find out which of these edge points intersect triangles in the fixed warp.  If they are inside the control warp
            //triangle mesh we find the point where the edge intersects the fixed warp mesh.
            for (int iEdge = 0; iEdge < MovingEdgeIndicies.Count; iEdge++)
            {
                int iEdgePoint = MovingEdgeIndicies[iEdge];

                LineSegment ctrlLine = new(UnmappedPoint.ControlPoint, warpingTransform.MapPoints[iEdgePoint].ControlPoint);
                LineSegment mapLine = new(UnmappedPoint.MappedPoint, warpingTransform.MapPoints[iEdgePoint].MappedPoint);

                //Control line found in nearest line call
                //Corresponding map line found in nearest line call

                //Find out if there is a line in the fixed transform we intersect with. 
                double distance = fixedTransform.ConvexHullIntersection(ctrlLine, UnmappedPoint.ControlPoint, out LineSegment foundCtrlLine, out LineSegment foundMapLine, out Vector2 intersect);
                if (distance == double.MaxValue)
                    continue;

                if (false == fixedTransform.CanTransform(warpingTransform.MapPoints[iEdgePoint].ControlPoint))
                    continue;

                //Translate from the fixed transform map space into control space. 
                Vector2 newCtrlPoint;
                {

                    //Determine how far along the mapping line on the fixed transfrom is the intersect point.
                    double mapLineDistance = Vector2.Distance(in foundMapLine.A, in intersect);
                    double mapLineFraction = mapLineDistance / foundMapLine.Length;

                    //How far along the corresponding control line are we?
                    double ctrlLineDistance = foundCtrlLine.Length * mapLineFraction;

                    newCtrlPoint = foundCtrlLine.Direction; //Get unit vector describing direction and scale it
                    newCtrlPoint *= ctrlLineDistance;
                    newCtrlPoint += foundCtrlLine.A;
                }

                //Now we must find out where the point on the warping transform is by checking how far along the mapping line on the warping transform we were.
                Vector2 newMapPoint;
                {
                    //Figure out where the transformed point lies in the moving transform mapped space. 
                    //Make sure we measure from the same origin on both mapped and control lines
                    double CtrlLineDistance = Vector2.Distance(in ctrlLine.A, in intersect);
                    double fraction = CtrlLineDistance / ctrlLine.Length;

                    Debug.Assert(fraction <= 1.0 && fraction >= 0.0);
                    if (fraction > 1f)
                        fraction = 1f;
                    else if (fraction < 0f)
                        fraction = 0f;

                    double mappedDistance = mapLine.Length * fraction;

                    newMapPoint = mapLine.Direction;
                    newMapPoint *= mappedDistance;
                    newMapPoint += mapLine.A;
                }

                output.Add(new MappingVector2(newCtrlPoint, newMapPoint));
            }
        }
    }
}

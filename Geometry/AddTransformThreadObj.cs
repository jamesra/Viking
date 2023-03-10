using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;


namespace Geometry.Transforms
{
    class AddTransformThreadObj : IDisposable
    {
        public ManualResetEvent DoneEvent = new ManualResetEvent(false);

        /// <summary>
        /// The output returned after processing
        /// </summary>
        public MappingGridVector2[] newPoints;

        readonly int[] iPoints; 
        readonly IControlPointTriangulation warpingTransform;
        readonly ITransform fixedTransform;

        /// <summary>
        /// This is set to true if every original point was transformed successfully
        /// </summary>
        public bool AllPointsTransformed = true;

        public AddTransformThreadObj(int[] iMapPoints, IControlPointTriangulation warpingT, ITransform fixedT)
        {
            this.iPoints = iMapPoints; 
            this.warpingTransform = warpingT;
            this.fixedTransform = fixedT; 
        }

        //TODO: Convert this entire class to an async method 
        public void ThreadPoolCallback(Object threadContext)
        {
            bool[] mapped = fixedTransform.TryTransform(warpingTransform.MapPoints.Select(p => p.ControlPoint).ToArray(), out var MappedControlPoints);
            
            //Create new MappingGridVector2s for all the points we could cleanly transform
            List<MappingGridVector2> newPointsList = MappedControlPoints.Select((cp, i) => mapped[i] ? 
                new MappingGridVector2(cp, warpingTransform.MapPoints[i].MappedPoint) :
                default).Where((_,i) => mapped[i]).ToList();
             
            if (!mapped.All(m => m))
            {
                //Prepare to remove unmappable points
                IDiscreteTransform discreteFixedTransform = fixedTransform as IDiscreteTransform;

                //OK, cleanup all of the points that could not be mapped
                foreach (int iPoint in iPoints)
                {
                    if (mapped[iPoint])
                        continue;

                    //If we could not map a point we need to test each edge connecting this point to other points to see if the edge intersects the fixed transform boundaries
                    MappingGridVector2 UnmappedPoint = warpingTransform.MapPoints[iPoint];

                    this.AllPointsTransformed = false;

                    List<int> MovingEdgeIndicies = warpingTransform.Edges[iPoint];

                    //Find out which of these edge points intersect triangles in the fixed warp.  If they are inside the control warp
                    //triangle mesh we find the point where the edge intersects the fixed warp mesh.
                    for (int iEdge = 0; iEdge < MovingEdgeIndicies.Count; iEdge++)
                    {
                        int iEdgePoint = MovingEdgeIndicies[iEdge];

                        GridLineSegment ctrlLine = new GridLineSegment(UnmappedPoint.ControlPoint, this.warpingTransform.MapPoints[iEdgePoint].ControlPoint);
                        GridLineSegment mapLine = new GridLineSegment(UnmappedPoint.MappedPoint, this.warpingTransform.MapPoints[iEdgePoint].MappedPoint);

                        //Control line found in nearest line call
                        //Corresponding map line found in nearest line call

                        //Find out if there is a line in the fixed transform we intersect with. 
                        double distance = discreteFixedTransform.ConvexHullIntersection(ctrlLine, UnmappedPoint.ControlPoint, out GridLineSegment foundCtrlLine, out GridLineSegment foundMapLine, out GridVector2 intersect);
                        if (distance == double.MaxValue)
                            continue;

                        if (false == discreteFixedTransform.CanTransform(this.warpingTransform.MapPoints[iEdgePoint].ControlPoint))
                            continue;

                        //Translate from the fixed transform map space into control space. 
                        GridVector2 newCtrlPoint;
                        {

                            //Determine how far along the mapping line on the fixed transfrom is the intersect point.
                            double mapLineDistance = GridVector2.Distance(in foundMapLine.A, in intersect);
                            double mapLineFraction = mapLineDistance / foundMapLine.Length;

                            //How far along the corresponding control line are we?
                            double ctrlLineDistance = foundCtrlLine.Length * mapLineFraction;

                            newCtrlPoint = foundCtrlLine.Direction; //Get unit vector describing direction and scale it
                            newCtrlPoint *= ctrlLineDistance;
                            newCtrlPoint += foundCtrlLine.A;
                        }

                        //Now we must find out where the point on the warping transform is by checking how far along the mapping line on the warping transform we were.
                        GridVector2 newMapPoint;
                        {
                            //Figure out where the transformed point lies in the moving transform mapped space. 
                            //Make sure we measure from the same origin on both mapped and control lines
                            double CtrlLineDistance = GridVector2.Distance(in ctrlLine.A, in intersect);
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

                        newPointsList.Add(new MappingGridVector2(newCtrlPoint, newMapPoint));
                    }
                }
            }

            MappingGridVector2.RemoveControlSpaceDuplicates(newPointsList);
            MappingGridVector2.RemoveMappedSpaceDuplicates(newPointsList);
            newPoints = newPointsList.ToArray();
            DoneEvent.Set(); 
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (this.DoneEvent != null)
            {
                this.DoneEvent.Close();
                this.DoneEvent = null;
            }
        }

        #endregion
    }
}

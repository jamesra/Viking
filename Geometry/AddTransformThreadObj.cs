using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading; 
using System.Diagnostics; 


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
        readonly TriangulationTransform warpingTransform;
        readonly TriangulationTransform fixedTransform;

        /// <summary>
        /// This is set to true if every original point was transformed successfully
        /// </summary>
        public bool AllPointsTransformed = true;

        public AddTransformThreadObj(int[] iMapPoints, TriangulationTransform warpingT, TriangulationTransform fixedT)
        {
            this.iPoints = iMapPoints; 
            this.warpingTransform = warpingT;
            this.fixedTransform = fixedT; 
        }

        public void ThreadPoolCallback(Object threadContext)
        {
            List<MappingGridVector2> newPointsList = new List<MappingGridVector2>(iPoints.Length); 

            foreach(int iPoint in iPoints)
            {
                MappingGridVector2 UnmappedPoint = warpingTransform.MapPoints[iPoint]; 
                GridVector2 newControl;
                bool TransformSuccess = fixedTransform.TryTransform(UnmappedPoint.ControlPoint, out newControl);
                if (TransformSuccess)
                {
                    newPointsList.Add( new MappingGridVector2(newControl, UnmappedPoint.MappedPoint) );
                }
                else
                {
                    this.AllPointsTransformed = false; 
                    //In this case we need to test each edge connecting this point to other points.
                    List<int> MovingEdgeIndicies = warpingTransform.Edges[iPoint];
                    
                    //Find out which of these edge points intersect triangles in the fixed warp.  If they are inside the control warp
                    //triangle mesh we find the point where the edge intersects the fixed warp mesh.
                    for (int iEdge = 0; iEdge < MovingEdgeIndicies.Count; iEdge++)
                    {
                        int iEdgePoint = MovingEdgeIndicies[iEdge];

                        GridLineSegment ctrlLine = new GridLineSegment(UnmappedPoint.ControlPoint, this.warpingTransform.MapPoints[iEdgePoint].ControlPoint);
                        GridLineSegment mapLine = new GridLineSegment(UnmappedPoint.MappedPoint, this.warpingTransform.MapPoints[iEdgePoint].MappedPoint);

                        GridLineSegment foundCtrlLine; //Control line found in nearest line call
                        GridLineSegment foundMapLine; //Corresponding map line found in nearest line call
                        GridVector2 intersect;

                        //Find out if there is a line in the fixed transform we intersect with. 
                        double distance = fixedTransform.ConvexHullIntersection(ctrlLine, UnmappedPoint.ControlPoint, out foundCtrlLine, out foundMapLine, out intersect);
                        if (distance == double.MaxValue)
                            continue;

                        if (fixedTransform.GetTransform(this.warpingTransform.MapPoints[iEdgePoint].ControlPoint) == null)
                            continue;


                        //Translate from the fixed transform map space into control space. 
                        GridVector2 newCtrlPoint;
                        {

                            //Determine how far along the mapping line on the fixed transfrom is the intersect point.
                            double mapLineDistance = GridVector2.Distance(foundMapLine.A, intersect);
                            double mapLineFraction = mapLineDistance / foundMapLine.Length;

                            //How far along the corresponding control line are we?
                            double ctrlLineDistance = foundCtrlLine.Length * mapLineFraction;

                            newCtrlPoint = foundCtrlLine.Direction; //Get unit vector describing direction and scale it
                            newCtrlPoint.Scale(ctrlLineDistance);
                            newCtrlPoint = newCtrlPoint + foundCtrlLine.A;
                        }

                        //Now we must find out where the point on the warping transform is by checking how far along the mapping line on the warping transform we were.
                        GridVector2 newMapPoint;
                        {
                            //Figure out where the transformed point lies in the moving transform mapped space. 
                            //Make sure we measure from the same origin on both mapped and control lines
                            double CtrlLineDistance = GridVector2.Distance(ctrlLine.A, intersect);
                            double fraction = CtrlLineDistance / ctrlLine.Length;

                            Debug.Assert(fraction <= 1.0 && fraction >= 0.0);
                            if (fraction > 1f)
                                fraction = 1f;
                            else if (fraction < 0f)
                                fraction = 0f;

                            double mappedDistance = mapLine.Length * fraction;

                            newMapPoint = mapLine.Direction;
                            newMapPoint.Scale(mappedDistance);
                            newMapPoint = newMapPoint + mapLine.A;
                        }

                        newPointsList.Add(new MappingGridVector2(newCtrlPoint, newMapPoint));
                    }
                     
                }
            }

            MappingGridVector2.RemoveDuplicates(newPointsList);
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

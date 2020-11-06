using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.VolumeModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Represents a possible outcome of cutting a given a polygon with a given path 
    /// </summary>
    internal static class PolygonCut
    {
        public static List<Change2DContourAction> GetRetraceActionForPath(long locID, GridPolygon OriginalVolumePolygon, IReadOnlyList<GridVector2> path, InteractionLogEvent StartEntry, InteractionLogEvent NextEntry)
        {
            /// <summary>
            /// Which polygon is being cut? This can be either the exterior ring or an interior ring
            /// </summary>
            PointIndex? PolyBeingCut;

            List<Change2DContourAction> output = new List<Change2DContourAction>();

            if (StartEntry.Index == 0)
                return output;

            GridVector2[] subpath = path.PathBetween(StartEntry.Index - 2 < 0 ? 0 : StartEntry.Index - 2, NextEntry.Index + 1 < path.Count ? NextEntry.Index + 1 : NextEntry.Index); //path.PathBetween(StartEntry.Index - 1 < 0 ? 0 : StartEntry.Index - 1, path.Count-1); //path.PathBetween(StartEntry.Index - 1 < 0 ? 0 : StartEntry.Index - 1, NextEntry.Index);

            if (subpath.Length <= 1)
            {
                return output;
            }

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //This can probably be simplified to checking the first and last segment in the path against the smoothed shape displayed in UI since we are using events

            SortedDictionary<double, PointIndex> intersectedSegments = OriginalVolumePolygon.IntersectingSegments(subpath.ToLineSegments());

            if (intersectedSegments.Count < 2)
            {
                //TODO: This is a hack, but I want this to work for a beta.  We shouldn't have to give up and search the entire path like this
                subpath = path.ToArray();
                intersectedSegments = OriginalVolumePolygon.IntersectingSegments(subpath.ToLineSegments());

                if (intersectedSegments.Count < 2)
                {
                    return output;
                }
            }

            double[] firstTwoIntersections = intersectedSegments.Keys.Take(2).ToArray();

            PointIndex FirstIntersection = intersectedSegments[firstTwoIntersections[0]];
            PointIndex SecondIntersection = intersectedSegments[firstTwoIntersections[1]];

            //We cannot cut across rings, so check that
            if (FirstIntersection.AreOnSameRing(SecondIntersection) == false)
            {
                return output;
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            GridPolygon PolyToCut = OriginalVolumePolygon;
            if (FirstIntersection.IsInner)
            {
                PolyToCut = OriginalVolumePolygon.InteriorPolygons[FirstIntersection.iInnerPoly.Value];
            }

            //Condition Check to make sure pen path exists and is valid
            if (subpath == null || subpath.Length < 2 || OriginalVolumePolygon.TotalVerticies <= 3)
            {
                return output;
            }

            PolyBeingCut = FirstIntersection;
            GridPolygon clockwise_poly = null;
            GridPolygon counter_clockwise_poly = null;

            try
            {
                clockwise_poly = GridPolygon.WalkPolygonCut(PolyToCut, RotationDirection.CLOCKWISE, subpath);
                clockwise_poly.ExteriorRing = CatmullRomControlPointSimplification.IdentifyControlPoints(clockwise_poly.ExteriorRing, AnnotationOverlay.CurrentOverlay.Parent.Downsample < 2 ? 4 : AnnotationOverlay.CurrentOverlay.Parent.Downsample * 4, true).ToArray();
                counter_clockwise_poly = GridPolygon.WalkPolygonCut(PolyToCut, RotationDirection.COUNTERCLOCKWISE, subpath);
                counter_clockwise_poly.ExteriorRing = CatmullRomControlPointSimplification.IdentifyControlPoints(counter_clockwise_poly.ExteriorRing, AnnotationOverlay.CurrentOverlay.Parent.Downsample < 2 ? 4 : AnnotationOverlay.CurrentOverlay.Parent.Downsample * 4, true).ToArray();
            }
            catch (ArgumentException e)
            {
                //Thrown when the polygon cannot be cut using the path
                return output;
            }

            RetraceCommandAction cutType = GetRetraceCommandAction(FirstIntersection.IsInner, StartEntry.Interaction == AnnotationRegionInteraction.ENTER);

            var Transform = AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

            var mosaic_clockwise_poly = Transform.TryMapShapeVolumeToSection(clockwise_poly);
            var mosaic_counter_clockwise_poly = Transform.TryMapShapeVolumeToSection(counter_clockwise_poly);


            LocationObj locObj = Store.Locations[locID];

            {
                Change2DContourAction grow_action;
                Change2DContourAction counter_clockwise_action;
                Change2DContourAction clockwise_action;
                GridPolygon input_poly_clone;
                GridPolygon inner_ring_replacement;

                bool UseCCW;

                switch (cutType)
                {
                    case RetraceCommandAction.NONE:
                        return output;
                    case RetraceCommandAction.GROW_EXTERIOR_RING:
                        UseCCW = counter_clockwise_poly.Area > clockwise_poly.Area;
                        GridPolygon outputVolumePoly = UseCCW ? counter_clockwise_poly : clockwise_poly;
                        GridPolygon outputMosaicPoly = UseCCW ? mosaic_counter_clockwise_poly : mosaic_clockwise_poly;
                        grow_action = new Change2DContourAction(locObj, cutType, outputMosaicPoly, outputVolumePoly);
                        output.Add(grow_action);
                        break;
                    case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                        counter_clockwise_action = new Change2DContourAction(locObj, cutType, mosaic_counter_clockwise_poly, counter_clockwise_poly);
                        clockwise_action = new Change2DContourAction(locObj, cutType, mosaic_clockwise_poly, clockwise_poly, true);
                        output.Add(counter_clockwise_action);
                        output.Add(clockwise_action);
                        break;
                    case RetraceCommandAction.GROW_INTERNAL_RING:
                        UseCCW = counter_clockwise_poly.Area > clockwise_poly.Area;
                        var mosaic_shape_clone = locObj.MosaicShape.ToPolygon();
                        inner_ring_replacement = UseCCW ? mosaic_counter_clockwise_poly : mosaic_clockwise_poly;
                        mosaic_shape_clone.ReplaceInteriorRing(PolyBeingCut.Value.iInnerPoly.Value, inner_ring_replacement);
                        grow_action = new Change2DContourAction(locObj, cutType, mosaic_shape_clone);
                        output.Add(grow_action);
                        break;
                    case RetraceCommandAction.SHRINK_INTERNAL_RING:
                        //Counterclockwise action
                        input_poly_clone = locObj.MosaicShape.ToPolygon(); //(GridPolygon)OriginalVolumePolygon.Clone();
                        input_poly_clone.ReplaceInteriorRing(PolyBeingCut.Value.iInnerPoly.Value, mosaic_counter_clockwise_poly);
                        counter_clockwise_action = new Change2DContourAction(locObj, cutType, input_poly_clone);

                        //Clockwise action
                        input_poly_clone = locObj.MosaicShape.ToPolygon(); //(GridPolygon)OriginalVolumePolygon.Clone();
                        input_poly_clone.ReplaceInteriorRing(PolyBeingCut.Value.iInnerPoly.Value, mosaic_clockwise_poly);
                        clockwise_action = new Change2DContourAction(locObj, cutType, input_poly_clone, ClockwiseContour: true);

                        output.Add(counter_clockwise_action);
                        output.Add(clockwise_action);
                        return output;
                }
            }

            return output;
        }

        public static RetraceCommandAction GetRetraceCommandAction(bool IsInnerPoly, bool EnterIsFirstInteraction)
        {
            if (IsInnerPoly)
            {
                return EnterIsFirstInteraction ? RetraceCommandAction.GROW_INTERNAL_RING : RetraceCommandAction.SHRINK_INTERNAL_RING;
            }
            else
            {
                return EnterIsFirstInteraction ? RetraceCommandAction.SHRINK_EXTERIOR_RING : RetraceCommandAction.GROW_EXTERIOR_RING;
            }
        }
    }

    /// <summary>
    /// A helper class that determines which actions can be performed on a generic 2D shape with a given path
    /// </summary>
    static public class Shared2DShapeActionsForPath
    {
        /// <summary>
        /// Returns a list of actions that should be offered for the path drawn over the provided smooth volume shape.
        /// </summary>
        /// <param name="shapeView"></param>
        /// <param name="smooth_volume_shape"></param>
        /// <param name="path"></param>
        /// <param name="interaction_log"></param>
        /// <param name="VisibleSectionNumber"></param>
        /// <returns></returns>
        public static List<IAction> GetPenActionsForShapeAnnotation(ICanvasView shapeView, IShape2D smooth_volume_shape, Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> actions = new List<IAction>();
            IViewLocation viewLocation = shapeView as IViewLocation;
            if (viewLocation != null)
            {
                if (smooth_volume_shape is GridPolygon)
                {
                    actions.AddRange(GetPenActionsForLocationShape2DAnnotation(viewLocation.ID, shapeView, smooth_volume_shape as GridPolygon, path, interaction_log, VisibleSectionNumber));
                }
                else
                {
                    actions.AddRange(IdentifyPossibleReshapeAction(viewLocation.ID, smooth_volume_shape, path));
                }

                return actions;
            }
            else
            {
                throw new NotImplementedException("Expected a shape that represented a location annotation");
            }
        }

        /// <summary>
        /// This function identifies actions that occur entirely within the interior of an existing shape
        ///   Cutting a new interior hole
        ///   Replacing the entire contour of an interior hole
        /// </summary>
        /// <param name="origin_ID"></param>
        /// <param name="smooth_volume_shape"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<IAction> IdentifyPossibleInteriorActions(long origin_ID, IShape2D volume_shape, IShape2D smooth_volume_shape, Path path)
        {
            List<IAction> actions = new List<IAction>();

            if (path.HasSelfIntersection == false)
                return actions;

            if (smooth_volume_shape is GridPolygon)
            {
                GridPolygon smooth_volume_polygon = smooth_volume_shape as GridPolygon;
                GridPolygon smooth_exterior_polygon = new GridPolygon(smooth_volume_polygon.ExteriorRing);
                GridPolygon closedpath = new GridPolygon(path.SimplifiedFirstLoop);
                if (smooth_exterior_polygon.Contains(closedpath))
                {
                    //Check if we draw a closed circle around existing holes (that should enlarge & merge if needed) or
                    //if we are cutting a new hole
                    bool PathContainsInteriorPoly = false;
                    bool InteriorPolyContainsPath = false;
                    List<int> IntersectedInteriorPolygons = new List<int>();
                    for (int iPoly = 0; iPoly < smooth_volume_polygon.InteriorPolygons.Count; iPoly++)
                    {
                        GridPolygon interiorPoly = smooth_volume_polygon.InteriorPolygons[iPoly];
                        OverlapType interiorRelationship = closedpath.ContainsExt(interiorPoly);
                        if (interiorRelationship == OverlapType.INTERSECTING || interiorRelationship == OverlapType.CONTAINED)
                        {
                            IntersectedInteriorPolygons.Add(iPoly);
                            PathContainsInteriorPoly = true;
                        }

                        if (interiorPoly.Contains(closedpath))
                        {
                            IntersectedInteriorPolygons.Clear();
                            IntersectedInteriorPolygons.Add(iPoly);
                            InteriorPolyContainsPath = true;
                            break;
                        }
                    }

                    LocationObj origin_loc = Store.Locations.GetObjectByID(origin_ID);

                    //Remove the intersected interior polygons, add the new interior polygon
                    if (IntersectedInteriorPolygons.Count > 0)
                    {
                        GridPolygon volume_shape_copy = ((GridPolygon)volume_shape).Clone() as GridPolygon;

                        IntersectedInteriorPolygons.Reverse(); //Have to delete high to low to remove the correct polygons
                        foreach (int iInnerPoly in IntersectedInteriorPolygons)
                        {
                            volume_shape_copy.RemoveInteriorRing(iInnerPoly);
                        }

                        volume_shape_copy.AddInteriorRing(closedpath);

                        var transform = WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;

                        Change2DContourAction action = new Change2DContourAction(origin_ID, RetraceCommandAction.REPLACE_INTERIOR_RING, volume_shape_copy, transform: transform);
                        actions.Add(action);
                    }
                    else
                    {
                        CutHoleAction cutHoleAction = new CutHoleAction(origin_loc, closedpath);
                        actions.Add(cutHoleAction);
                    }
                }
            }

            return actions;
        }

        /// <summary>
        /// Check if we should replace the entire shape or cut a new hole into an existing shape
        /// </summary>
        /// <param name="origin_ID"></param>
        /// <param name="smooth_volume_shape"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<IAction> IdentifyPossibleReshapeAction(long origin_ID, IShape2D smooth_volume_shape, Path path)
        {
            List<IAction> actions = new List<IAction>();
            LocationObj origin_loc = Store.Locations.GetObjectByID(origin_ID);

            //2D Case: If we draw a loop around an annotation we should offer to replace that annotations contour's with the closed loop
            if (origin_loc.TypeCode.AllowsClosed2DShape() && path.HasSelfIntersection)
            {
                GridPolygon newShape = new GridPolygon(path.SimplifiedFirstLoop);

                //Check to see if we should migrate interior holes that are inside the new shape from the old shape.
                if (origin_loc.TypeCode.AllowsInteriorHoles())
                {
                    if (smooth_volume_shape is GridPolygon)
                    {
                        GridPolygon old_volume_poly = (GridPolygon)smooth_volume_shape;

                        foreach (GridPolygon interiorPoly in old_volume_poly.InteriorPolygons)
                        {
                            try
                            {
                                newShape.AddInteriorRing(interiorPoly);
                            }
                            catch (ArgumentException e)
                            {
                                Trace.WriteLine("Could not add old interior ring to reshaped polygon {0}", string.Format(e.Message));
                                continue;
                            }
                        }
                    }
                }

                var Transform = WebAnnotation.AnnotationOverlay.CurrentOverlay.Parent.Section.ActiveSectionToVolumeTransform;
                var mosaic_shape = Transform.TryMapShapeVolumeToSection(newShape);

                Change2DContourAction action = new Change2DContourAction(origin_loc, RetraceCommandAction.REPLACE_EXTERIOR_RING, mosaic_shape, newShape);
                actions.Add(action);
            }
            else if (origin_loc.TypeCode.AllowsOpen2DShape() && path.HasSelfIntersection == false)//1-D Case
            {
                Change1DContourAction action = new Change1DContourAction(origin_loc, path.SimplifiedPath.ToPolyline());
                actions.Add(action);

                //TODO: Make two more options, where we use the intersection point and append on the original line.

            }

            return actions;
        }

        /// <summary>
        /// Return true if the path crosses the border of the shape N times  
        /// </summary>
        /// <returns></returns>
        public static bool IsBorderScribble(long locID, ICanvasView shapeView, IReadOnlyList<InteractionLogEvent> interaction_log, int nCrossings)
        {
            var shapeEntries = interaction_log.Where(entry => entry.Annotation == shapeView).ToArray();
            var shapeEntryExitEntries = shapeEntries.Where(entry => entry.Interaction == AnnotationRegionInteraction.ENTER || entry.Interaction == AnnotationRegionInteraction.EXIT).ToArray();

            throw new NotImplementedException();
        }

        public static List<IAction> GetPenActionsForLocationShape2DAnnotation(long locID, ICanvasView shapeView, GridPolygon smooth_volume_polygon, Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            var output = new List<IAction>();

            //if (interaction_log.Last().Annotation != shapeView)
            //    return new List<IAction>();

            //Determine which entries apply to our shape
            var shapeEntries = interaction_log.Where(entry => entry.Annotation == shapeView).ToArray();
            var shapeEntryExitEntries = shapeEntries.Where(entry => entry.Interaction == AnnotationRegionInteraction.ENTER || entry.Interaction == AnnotationRegionInteraction.EXIT).ToArray();

            //Check for retrace and replace contour actions
            //We retrace if we enter/exit the shape and there is not a loop
            if (path.HasSelfIntersection == false)
            {
                if (shapeEntryExitEntries.Length >= 2)
                {
                    InteractionLogEvent StartEntry = shapeEntries[0];

                    var locModel = Store.Locations[locID];
                    //Viking.VolumeModel.IVolumeToSectionTransform transform = Viking.UI.State.volume.GetSectionToVolumeTransform((int)locModel.Z);

                    //GridPolygon VolumePolygon = transform.TryMapShapeSectionToVolume(locModel.MosaicShape).ToPolygon();
                    //GridPolygon SmoothedVolumePolygon = VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

                    //We need to enter/exit the same ring of the polygon to change the contour of the annotation
                    /*
                    for (int i = 1; i < shapeEntryExitEntries.Length; i++)
                    {
                        InteractionLogEvent NextEntry = shapeEntryExitEntries[i];

                        output.AddRange(PolygonCut.GetRetraceActionForPath(locModel.ID, VolumePolygon, path.Points, StartEntry, NextEntry));

                        StartEntry = NextEntry;
                    }
                    */
                    output.AddRange(PolygonCut.GetRetraceActionForPath(locModel.ID, smooth_volume_polygon, path.Points, shapeEntryExitEntries[shapeEntryExitEntries.Length - 2], shapeEntryExitEntries[shapeEntryExitEntries.Length - 1]));
                }
            }
            else if (path.HasSelfIntersection)
            {
                output.AddRange(IdentifyPossibleReshapeAction(locID, smooth_volume_polygon, path));
            }

            //Check for crossing the border N times (was going to be a delete gesture but I've shelved it
            /*
            if (shapeEntryExitEntries.Count(e => e.Interaction == AnnotationRegionInteraction.ENTER) >= 3 &&
                   shapeEntryExitEntries.Count(e => e.Interaction == AnnotationRegionInteraction.EXIT) >= 3)
            {
                foreach (var e in shapeEntryExitEntries)
                {
                    if (e.Index == 0)
                        continue;

                    GridLineSegment s = new GridLineSegment(path.Points[e.Index], path.Points[e.Index - 1]);

                }
            }
            */



            return output;
        }
    }
}

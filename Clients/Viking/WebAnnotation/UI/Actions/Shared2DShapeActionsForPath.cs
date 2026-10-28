using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// Represents a possible outcome of cutting a given a polygon with a given path 
    /// </summary>
    internal static class PolygonCut
    {
        /// <param name="OriginalVolumePolygon">The location's outline as drawn (smoothed), which the pen crossed.</param>
        /// <param name="tolerance">Largest distance, in volume pixels, between the saved curve and the pen stroke.</param>
        public static List<Change2DContourAction> GetRetraceActionForPath(long locID, Polygon OriginalVolumePolygon, IReadOnlyList<Vector2> path, InteractionLogEvent StartEntry, InteractionLogEvent NextEntry, double tolerance)
        {
            /// <summary>
            /// Which polygon is being cut? This can be either the exterior ring or an interior ring
            /// </summary>
            PolygonIndex? PolyBeingCut;

            List<Change2DContourAction> output = [];

            if (StartEntry.Index == 0)
            {
                return Rejected(output, locID, "the start event is at path index 0");
            }

            Vector2[] subpath = path.PathBetween(StartEntry.Index - 2 < 0 ? 0 : StartEntry.Index - 2, NextEntry.Index + 1 < path.Count ? NextEntry.Index + 1 : NextEntry.Index); //path.PathBetween(StartEntry.Index - 1 < 0 ? 0 : StartEntry.Index - 1, path.Count-1); //path.PathBetween(StartEntry.Index - 1 < 0 ? 0 : StartEntry.Index - 1, NextEntry.Index);

            if (subpath.Length <= 1)
            {
                return Rejected(output, locID, $"sub-path has {subpath.Length} point(s)");
            }

            /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //This can probably be simplified to checking the first and last segment in the path against the smoothed shape displayed in UI since we are using events

            SortedDictionary<double, PolygonIndex> intersectedSegments = OriginalVolumePolygon.IntersectingSegmentIndices(subpath.ToLineSegments());

            if (intersectedSegments.Count < 2)
            {
                //TODO: This is a hack, but I want this to work for a beta.  We shouldn't have to give up and search the entire path like this
                subpath = [.. path];
                intersectedSegments = OriginalVolumePolygon.IntersectingSegmentIndices(subpath.ToLineSegments());

                if (intersectedSegments.Count < 2)
                {
                    return Rejected(output, locID, $"path crosses the polygon outline {intersectedSegments.Count} time(s), need 2 (events {StartEntry.Interaction}@{StartEntry.Index} to {NextEntry.Interaction}@{NextEntry.Index})");
                }
            }

            double[] firstTwoIntersections = [.. intersectedSegments.Keys.Take(2)];

            PolygonIndex FirstIntersection = intersectedSegments[firstTwoIntersections[0]];
            PolygonIndex SecondIntersection = intersectedSegments[firstTwoIntersections[1]];

            //We cannot cut across rings, so check that
            if (FirstIntersection.AreOnSameRing(SecondIntersection) == false)
            {
                return Rejected(output, locID, "the first two crossings are on different rings");
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //The view can outlive its store entry (deleted or replaced by a poll), so a missing location rejects the cut instead of throwing out of the mouse-up handler.
            if (!Store.Locations.TryGetObjectByID(locID, out LocationObj locObj) || locObj is null)
            {
                return Rejected(output, locID, "the location is no longer in the store");
            }

            // Cuts near or past the edge of the registration grid have vertices a discrete grid cannot map; the RBF fallback extrapolates them.
            IVolumeToSectionTransform Transform = AnnotationOverlay.ActiveSectionToVolumeTransform.WithContinuousFallback();

            Polygon PolyToCut = OriginalVolumePolygon.Clone() as Polygon;
            if (FirstIntersection.IsInner)
            {
                PolyToCut = OriginalVolumePolygon.InteriorPolygons[FirstIntersection.InnerShapeIndex.Value];
            }
            else if (PolyToCut.HasInteriorRings)
            {
                //If we have a polygon with multiple rings, then all rings have been smoothed by Viking and only the exterior ring of the new polygon will be smoothed by the code below.
                //We don't want to change the interior rings, so replace the smooth versions with the originals from the LocationObj
                Polygon unsmooth_mosaic_poly = locObj.MosaicShape.ToPolygon();

                for (int i = 0; i < PolyToCut.InteriorRings.Count && i < unsmooth_mosaic_poly.InteriorPolygons.Count; i++)
                {
                    Polygon unsmooth_volume_inner_poly = Transform.TryMapShapeSectionToVolume(unsmooth_mosaic_poly.InteriorPolygons[i]);
                    if (unsmooth_volume_inner_poly is null)
                        continue;

                    try
                    {
                        PolyToCut.ReplaceInteriorRing(i, unsmooth_volume_inner_poly);
                    }
                    catch (ArgumentException e)
                    {
                        // The original hole can touch the smoothed outline once mapped; the smoothed hole stays and is not cut.
                        Trace.WriteLine($"Pen retrace of location {locID}: hole {i} keeps its smoothed ring: {e.Message}");
                    }
                }
            }


            //Condition Check to make sure pen path exists and is valid
            if (subpath is null || subpath.Length < 2 || OriginalVolumePolygon.TotalVertices <= 3)
            {
                return Rejected(output, locID, "the polygon has 3 or fewer vertices");
            }

            PolyBeingCut = FirstIntersection;

            // The walk follows the smoothed display ring, so it carries every interpolated sample. These pieces decide the
            // choices and draw their previews; drop samples that sit on the chord between neighbors so they mesh quickly.
            // The choice fill still draws from the cached cell, not from this reduced ring.
            double downsample = AnnotationOverlay.ActiveDownsample;
            double reduceTolerance = downsample < 2 ? 4 : downsample * 4;

            // Each side is cut on its own: a self-intersecting source ring only spoils the side that carries the fold,
            // and the broken side is never offered or rendered.
            Polygon clockwise_poly = TryCutSide(PolyToCut, RotationDirection.Clockwise, subpath, reduceTolerance, out string clockwiseProblem);
            Polygon counter_clockwise_poly = TryCutSide(PolyToCut, RotationDirection.Counterclockwise, subpath, reduceTolerance, out string counterClockwiseProblem);

            if (clockwise_poly is null && counter_clockwise_poly is null)
            {
                string sourceProblem = PolyToCut.DescribeInvalidity();
                string sourceNote = sourceProblem is null ? string.Empty : $" (the location's own outline is invalid: {sourceProblem})";
                return Rejected(output, locID, $"the polygon could not be cut along the path: {clockwiseProblem}{sourceNote}");
            }

            if (clockwise_poly is null || counter_clockwise_poly is null)
            {
                Trace.WriteLine($"Pen retrace of location {locID}: one side of the cut is invalid and is not offered ({(clockwise_poly is null ? "clockwise" : "counterclockwise")}: {(clockwise_poly is null ? clockwiseProblem : counterClockwiseProblem)}).");
            }

            RetraceCommandAction cutType = GetRetraceCommandAction(FirstIntersection.IsInner, StartEntry.Interaction == AnnotationRegionInteraction.ENTER);



            // What is saved keeps the location's stored control points and adds only the pen stretch. Saving the walked piece
            // instead turned the drawn curve into control points and curved it again, moving the whole outline at every edit.
            Polygon saved_clockwise_poly = clockwise_poly;
            Polygon saved_counter_clockwise_poly = counter_clockwise_poly;
            Polygon mosaic_clockwise_poly = null;
            Polygon mosaic_counter_clockwise_poly = null;
            if (ControlPointCut.TrySplice(locObj.MosaicShape.ToPolygon(), Transform, FirstIntersection, subpath, tolerance, out ControlPointCut.Piece afterFirst, out ControlPointCut.Piece afterSecond))
            {
                ControlPointCut.PairWithWalk(afterFirst, afterSecond, clockwise_poly, counter_clockwise_poly, out ControlPointCut.Piece splicedClockwise, out ControlPointCut.Piece splicedCounterClockwise);
                if (clockwise_poly is not null && splicedClockwise is not null)
                {
                    saved_clockwise_poly = splicedClockwise.Volume;
                    mosaic_clockwise_poly = splicedClockwise.Mosaic;
                }

                if (counter_clockwise_poly is not null && splicedCounterClockwise is not null)
                {
                    saved_counter_clockwise_poly = splicedCounterClockwise.Volume;
                    mosaic_counter_clockwise_poly = splicedCounterClockwise.Mosaic;
                }
            }
            else
            {
                Trace.WriteLine($"Pen retrace of location {locID}: the stroke could not be spliced into the stored control points; saving the walked outline.");
            }

            // A side the splice could not build falls back to the walked piece
            if (mosaic_clockwise_poly is null && clockwise_poly is not null)
                mosaic_clockwise_poly = Transform.TryMapShapeVolumeToSection(clockwise_poly);
            if (mosaic_counter_clockwise_poly is null && counter_clockwise_poly is not null)
                mosaic_counter_clockwise_poly = Transform.TryMapShapeVolumeToSection(counter_clockwise_poly);



            //Create actions for the path
            {
                Change2DContourAction grow_action;
                Change2DContourAction counter_clockwise_action;
                Change2DContourAction clockwise_action;
                Polygon input_poly_clone;
                Polygon inner_ring_replacement;

                bool UseCCW;

                switch (cutType)
                {
                    case RetraceCommandAction.NONE:
                        return Rejected(output, locID, "no retrace action applies");
                    case RetraceCommandAction.GROW_EXTERIOR_RING:
                        if (TryChooseGrownSide(PolyToCut, clockwise_poly, counter_clockwise_poly, out UseCCW) == false)
                            return Rejected(output, locID, "the only valid side of the cut does not enlarge the outline");
                        Polygon outputVolumePoly = UseCCW ? saved_counter_clockwise_poly : saved_clockwise_poly;
                        Polygon outputMosaicPoly = UseCCW ? mosaic_counter_clockwise_poly : mosaic_clockwise_poly;
                        Polygon growthLobe = UseCCW ? clockwise_poly : counter_clockwise_poly;
                        // Only the kept piece is saved, so an unmappable discarded lobe must not block the cut.
                        if (outputMosaicPoly is null)
                            return Rejected(output, locID, "the grown outline has a vertex outside the section's mapped area");
                        grow_action = new Change2DContourAction(locObj, cutType, outputMosaicPoly, outputVolumePoly, transform: Transform);
                        AttachCachedPreview(grow_action, OriginalVolumePolygon, subpath, addedPatch: growthLobe, excludedPatch: null);
                        output.Add(grow_action);
                        break;
                    case RetraceCommandAction.SHRINK_EXTERIOR_RING:
                        // TryMapShapeVolumeToSection returns null when any vertex is outside the section's mapped area.
                        // Both pieces are offered as choices, so one that cannot be saved means no valid pair.
                        // A side that is invalid (null) is simply not offered.
                        if ((counter_clockwise_poly is not null && mosaic_counter_clockwise_poly is null) || (clockwise_poly is not null && mosaic_clockwise_poly is null))
                            return Rejected(output, locID, "a cut piece has a vertex outside the section's mapped area");
                        counter_clockwise_action = counter_clockwise_poly is null ? null : new Change2DContourAction(locObj, cutType, mosaic_counter_clockwise_poly, saved_counter_clockwise_poly, transform: Transform);
                        clockwise_action = clockwise_poly is null ? null : new Change2DContourAction(locObj, cutType, mosaic_clockwise_poly, saved_clockwise_poly, true, Transform);
                        if (counter_clockwise_action is not null && clockwise_action is not null)
                        {
                            AttachShrinkPreview(counter_clockwise_action, clockwise_action, OriginalVolumePolygon, subpath, counter_clockwise_poly, clockwise_poly);
                        }

                        // A lone piece has no cached preview, so the choice view draws that piece itself and never the broken side.
                        if (counter_clockwise_action is not null)
                            output.Add(counter_clockwise_action);
                        if (clockwise_action is not null)
                            output.Add(clockwise_action);
                        break;
                    case RetraceCommandAction.GROW_INTERNAL_RING:
                        if (TryChooseGrownSide(PolyToCut, clockwise_poly, counter_clockwise_poly, out UseCCW) == false)
                            return Rejected(output, locID, "the only valid side of the cut does not enlarge the hole");
                        inner_ring_replacement = UseCCW ? mosaic_counter_clockwise_poly : mosaic_clockwise_poly;
                        if (inner_ring_replacement is null)
                            return output;
                        Polygon mosaic_shape_clone = WithReplacedHole(locObj.MosaicShape.ToPolygon(), PolyBeingCut.Value.InnerShapeIndex.Value, inner_ring_replacement, locID);
                        if (mosaic_shape_clone is null)
                            return output;
                        grow_action = new Change2DContourAction(locObj, cutType, mosaic_shape_clone, transform: Transform);
                        output.Add(grow_action);
                        break;
                    case RetraceCommandAction.SHRINK_INTERNAL_RING:
                        if ((counter_clockwise_poly is not null && mosaic_counter_clockwise_poly is null) || (clockwise_poly is not null && mosaic_clockwise_poly is null))
                            return output;

                        if (mosaic_counter_clockwise_poly is not null)
                        {
                            //Counterclockwise action
                            input_poly_clone = WithReplacedHole(locObj.MosaicShape.ToPolygon(), PolyBeingCut.Value.InnerShapeIndex.Value, mosaic_counter_clockwise_poly, locID);
                            if (input_poly_clone is not null)
                                output.Add(new Change2DContourAction(locObj, cutType, input_poly_clone, transform: Transform));
                        }

                        if (mosaic_clockwise_poly is not null)
                        {
                            //Clockwise action
                            input_poly_clone = WithReplacedHole(locObj.MosaicShape.ToPolygon(), PolyBeingCut.Value.InnerShapeIndex.Value, mosaic_clockwise_poly, locID);
                            if (input_poly_clone is not null)
                                output.Add(new Change2DContourAction(locObj, cutType, input_poly_clone, ClockwiseContour: true, transform: Transform));
                        }

                        return output;
                }
            }

            return output;
        }

        /// <summary>
        /// <paramref name="shape"/> with hole <paramref name="hole"/> replaced by <paramref name="replacement"/>, or null when
        /// the new hole would cross the outline or another hole (the stroke left the hole's surroundings). That choice is then
        /// not offered; with none left, the stroke goes to the region cut, which can merge the hole with what it reached.
        /// </summary>
        internal static Polygon WithReplacedHole(Polygon shape, int hole, Polygon replacement, long locID)
        {
            try
            {
                shape.ReplaceInteriorRing(hole, replacement);
                return shape;
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine($"Pen retrace of location {locID}: the new hole {hole} is not valid inside the polygon ({e.Message}); that choice is not offered.");
                return null;
            }
        }

        /// <summary>
        /// Cuts one side of the polygon along the path and reduces its exterior ring.
        /// </summary>
        /// <returns>The piece, or null with <paramref name="problem"/> set when that side is not a valid polygon
        /// (for example because the source ring crosses itself on that side).</returns>
        internal static Polygon TryCutSide(Polygon polyToCut, RotationDirection direction, Vector2[] subpath, double reduceTolerance, out string problem)
        {
            try
            {
                problem = null;
                return Polygon.WalkPolygonCut(polyToCut, direction, subpath).ReduceExteriorRing(reduceTolerance);
            }
            catch (ArgumentException ex)
            {
                problem = $"{direction}: {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Grow cuts keep the larger piece. A side that failed validation has no area to compare, so a lone valid
        /// piece is only taken when it is larger than the polygon being grown (the discarded lobe never is).
        /// </summary>
        internal static bool TryChooseGrownSide(Polygon source, Polygon clockwise, Polygon counterClockwise, out bool useCounterClockwise)
        {
            useCounterClockwise = false;
            if (clockwise is not null && counterClockwise is not null)
            {
                useCounterClockwise = counterClockwise.Area > clockwise.Area;
                return true;
            }

            Polygon only = clockwise ?? counterClockwise;
            useCounterClockwise = counterClockwise is not null;
            return only.Area > source.Area;
        }

        /// <summary>
        /// Logs why a pen path produced no retrace choice and returns the (empty) result, so a stroke that offers no
        /// polygon change can be explained from the log.
        /// </summary>
        private static List<Change2DContourAction> Rejected(List<Change2DContourAction> output, long locID, string reason)
        {
            Trace.WriteLine($"Pen retrace of location {locID} offered no change: {reason}.");
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

        /// <summary>
        /// The larger shrink piece reuses the cached cell. The smaller piece is meshed on its own by the choice view.
        /// </summary>
        static void AttachShrinkPreview(Change2DContourAction counterClockwise, Change2DContourAction clockwise, Polygon original, IReadOnlyList<Vector2> path, Polygon counterClockwisePoly, Polygon clockwisePoly)
        {
            bool counterClockwiseIsCap = counterClockwisePoly.Area <= clockwisePoly.Area;
            Change2DContourAction large = counterClockwiseIsCap ? clockwise : counterClockwise;
            Polygon cap = counterClockwiseIsCap ? counterClockwisePoly : clockwisePoly;
            AttachCachedPreview(large, original, path, addedPatch: null, excludedPatch: cap);
        }

        /// <summary>
        /// Points the choice fill at the polygon that was triangulated when the cell was shown.
        /// </summary>
        static void AttachCachedPreview(Change2DContourAction action, Polygon original, IReadOnlyList<Vector2> path, Polygon addedPatch, Polygon excludedPatch)
        {
            action.PreviewOriginal = original;
            action.PreviewPath = path;
            action.PreviewAddedPatch = addedPatch;
            action.PreviewExcludedPatch = excludedPatch;
        }
    }

    /// <summary>
    /// A helper class that determines which actions can be performed on a generic 2D shape with a given path
    /// </summary>
    public static class Shared2DShapeActionsForPath
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
            List<IAction> actions = [];
            if (shapeView is IViewLocation viewLocation)
            {
                if (smooth_volume_shape is Polygon)
                {
                    actions.AddRange(GetPenActionsForLocationShape2DAnnotation(viewLocation.ID, shapeView, smooth_volume_shape as Polygon, path, interaction_log, VisibleSectionNumber));
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
            List<IAction> actions = [];

            if (path.HasSelfIntersection == false)
            {
                return actions;
            }

            if (smooth_volume_shape is Polygon)
            {
                Polygon smooth_volume_polygon = smooth_volume_shape as Polygon;
                Polygon smooth_exterior_polygon = new(smooth_volume_polygon.ExteriorRing);
                Polygon closedpath = new(path.SimplifiedFirstLoop);
                if (smooth_exterior_polygon.Covers(closedpath))
                {
                    List<int> IntersectedInteriorPolygons = [];
                    for (int iPoly = 0; iPoly < smooth_volume_polygon.InteriorPolygons.Count; iPoly++)
                    {
                        Polygon interiorPoly = smooth_volume_polygon.InteriorPolygons[iPoly];
                        ShapeRelation interiorRelationship = closedpath.GetRelation(interiorPoly);
                        if (interiorRelationship == ShapeRelation.Intersecting || interiorRelationship == ShapeRelation.Contained)
                        {
                            IntersectedInteriorPolygons.Add(iPoly);
                        }

                        if (interiorPoly.Covers(closedpath))
                        {
                            IntersectedInteriorPolygons.Clear();
                            IntersectedInteriorPolygons.Add(iPoly);
                            break;
                        }
                    }

                    LocationObj origin_loc;
                    if (!Store.Locations.TryGetObjectByID(origin_ID, out origin_loc) || origin_loc == null)
                        return actions;

                    //Remove the intersected interior polygons, add the new interior polygon
                    if (IntersectedInteriorPolygons.Count > 0)
                    {
                        Polygon volume_shape_copy = ((Polygon)volume_shape).Clone() as Polygon;

                        IntersectedInteriorPolygons.Reverse(); //Have to delete high to low to remove the correct polygons
                        foreach (int iInnerPoly in IntersectedInteriorPolygons)
                        {
                            volume_shape_copy.RemoveInteriorRing(iInnerPoly);
                        }

                        volume_shape_copy.AddInteriorRing(closedpath);

                        IVolumeToSectionTransform transform = WebAnnotation.AnnotationOverlay.ActiveSectionToVolumeTransform;

                        // The stored shape keeps its own points; only the new hole is mapped to the section
                        Polygon mosaic_shape_copy = origin_loc.MosaicShape.ToPolygon();
                        foreach (int iInnerPoly in IntersectedInteriorPolygons)
                        {
                            mosaic_shape_copy.RemoveInteriorRing(iInnerPoly);
                        }

                        Polygon mosaic_hole = transform.TryMapShapeVolumeToSection(closedpath);
                        if (mosaic_hole is null)
                            return actions;

                        mosaic_shape_copy.AddInteriorRing(mosaic_hole);

                        Change2DContourAction action = new(origin_ID, RetraceCommandAction.REPLACE_INTERIOR_RING, mosaic_shape_copy, volume_shape_copy, transform: transform);
                        actions.Add(action);
                    }
                    else
                    {
                        CutHoleAction cutHoleAction = new(origin_loc, closedpath);
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
            List<IAction> actions = [];
            if (!Store.Locations.TryGetObjectByID(origin_ID, out LocationObj origin_loc) || origin_loc == null)
                return actions;

            //2D Case: If we draw a loop around an annotation we should offer to replace that annotations contour's with the closed loop
            if (origin_loc.TypeCode.AllowsClosed2DShape() && path.HasSelfIntersection)
            {
                Polygon newShape = new(path.SimplifiedFirstLoop);

                IVolumeToSectionTransform Transform = WebAnnotation.AnnotationOverlay.ActiveSectionToVolumeTransform;
                Polygon mosaic_shape = Transform.TryMapShapeVolumeToSection(newShape);
                if (mosaic_shape is null)
                    return actions;

                //Check to see if we should migrate interior holes that are inside the new shape from the old shape.
                //The stored holes are moved as they are; the drawn (smoothed) holes would be curved again once saved.
                if (origin_loc.TypeCode.AllowsInteriorHoles() && smooth_volume_shape is Polygon)
                {
                    foreach (Vector2[] mosaicHole in origin_loc.MosaicShape.ToPolygon().InteriorRings)
                    {
                        if (Transform.TrySectionToVolume(mosaicHole, out Vector2[] volumeHole).Any(mapped => !mapped))
                            continue;

                        try
                        {
                            mosaic_shape.AddInteriorRing(mosaicHole);
                        }
                        catch (ArgumentException e)
                        {
                            Trace.WriteLine("Could not add old interior ring to reshaped polygon {0}", string.Format(e.Message));
                            continue;
                        }

                        try
                        {
                            newShape.AddInteriorRing(volumeHole);
                        }
                        catch (ArgumentException e)
                        {
                            // Keep the two spaces in step: the hole goes from both or neither
                            mosaic_shape.RemoveInteriorRing(mosaic_shape.InteriorRings.Count - 1);
                            Trace.WriteLine("Could not add old interior ring to reshaped polygon {0}", string.Format(e.Message));
                        }
                    }
                }

                Change2DContourAction action = new(origin_loc, RetraceCommandAction.REPLACE_EXTERIOR_RING, mosaic_shape, newShape, transform: Transform);
                actions.Add(action);
            }
            else if (origin_loc.TypeCode.AllowsOpen2DShape() && path.HasSelfIntersection == false)//1-D Case
            {
                Change1DContourAction action = new(origin_loc, path.SimplifiedPath.ToPolyline());
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
            InteractionLogEvent[] shapeEntries = [.. interaction_log.Where(entry => entry.Annotation == shapeView)];
            InteractionLogEvent[] shapeEntryExitEntries = [.. shapeEntries.Where(entry => entry.Interaction == AnnotationRegionInteraction.ENTER || entry.Interaction == AnnotationRegionInteraction.EXIT)];

            throw new NotImplementedException();
        }

        /// <summary>The region cut choices (<see cref="PenRegionCut"/>) for a stroke over the location.</summary>
        private static List<Change2DContourAction> RegionCutActions(long locID, Path path)
        {
            // The view can outlive its store entry (deleted or replaced by a poll)
            if (!Store.Locations.TryGetObjectByID(locID, out LocationObj location) || location is null)
                return [];

            IVolumeToSectionTransform transform = AnnotationOverlay.ActiveSectionToVolumeTransform.WithContinuousFallback();
            return PenRegionCut.GetActions(location, transform, path.Points, path.SimplifiedPathTolerance);
        }

        public static List<IAction> GetPenActionsForLocationShape2DAnnotation(long locID, ICanvasView shapeView, Polygon smooth_volume_polygon, Path path, IReadOnlyList<InteractionLogEvent> interaction_log, int VisibleSectionNumber)
        {
            List<IAction> output = [];

            //if (interaction_log.Last().Annotation != shapeView)
            //    return new List<IAction>();

            //Determine which entries apply to our shape
            InteractionLogEvent[] shapeEntries = [.. interaction_log.Where(entry => entry.Annotation == shapeView)];
            InteractionLogEvent[] shapeEntryExitEntries = [.. shapeEntries.Where(entry => entry.Interaction == AnnotationRegionInteraction.ENTER || entry.Interaction == AnnotationRegionInteraction.EXIT)];

            //Check for retrace and replace contour actions
            //We retrace if we enter/exit the shape and there is not a loop
            Trace.WriteLine($"Pen path over location {locID}: {shapeEntryExitEntries.Length} enter/exit event(s), self-intersecting={path.HasSelfIntersection}.");
            if (path.HasSelfIntersection == false)
            {
                if (shapeEntryExitEntries.Length >= 2)
                {
                    InteractionLogEvent StartEntry = shapeEntries[0];

                    //No Store.Locations lookup: the view can outlive its store entry (deleted or replaced by a poll), and only the ID is needed.
                    //Viking.VolumeModel.IVolumeToSectionTransform transform = Viking.UI.State.volume.GetSectionToVolumeTransform((int)locModel.Z);

                    //Polygon VolumePolygon = transform.TryMapShapeSectionToVolume(locModel.MosaicShape).ToPolygon();
                    //Polygon SmoothedVolumePolygon = VolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

                    //We need to enter/exit the same ring of the polygon to change the contour of the annotation
                    /*
                    for (int i = 1; i < shapeEntryExitEntries.Length; i++)
                    {
                        InteractionLogEvent NextEntry = shapeEntryExitEntries[i];

                        output.AddRange(PolygonCut.GetRetraceActionForPath(locModel.ID, VolumePolygon, path.Points, StartEntry, NextEntry));

                        StartEntry = NextEntry;
                    }
                    */
                    // A stroke that crosses the outline more than twice, such as one cutting a bridge out of a cell, encloses a
                    // region to remove or add. Two crossings are a retrace of the outline between them.
                    int crossings = shapeEntryExitEntries.Count(e => e.Index > 0);
                    if (crossings > 2)
                        output.AddRange(RegionCutActions(locID, path));

                    if (output.Count == 0)
                        output.AddRange(PolygonCut.GetRetraceActionForPath(locID, smooth_volume_polygon, path.Points, shapeEntryExitEntries[shapeEntryExitEntries.Length - 2], shapeEntryExitEntries[shapeEntryExitEntries.Length - 1], path.SimplifiedPathTolerance));

                    // Two crossings on different rings (from the outline into a hole) are not a retrace, but they do enclose a region
                    if (output.Count == 0 && crossings == 2)
                        output.AddRange(RegionCutActions(locID, path));
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

                    LineSegment s = new LineSegment(path.Points[e.Index], path.Points[e.Index - 1]);

                }
            }
            */



            return output;
        }
    }
}

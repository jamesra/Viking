using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using WebAnnotation.UI.Commands;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation
{
    /// <summary>
    /// Describes the action/command that will occur if the user clicks the mouse at a given position on the screen.
    /// </summary>
    public enum LocationAction
    {
        /// <summary>
        /// No action
        /// </summary>
        NONE,
        /// <summary>
        /// Move the annotation
        /// </summary>
        TRANSLATE,
        /// <summary>
        /// Scale the size of the annotation
        /// </summary>
        SCALE,
        /// <summary>
        /// Scale and translate the annotation
        /// </summary>
        SCALETRANSLATE,
        /// <summary>
        /// Adjust a specific control point
        /// </summary>
        ADJUST,
        /// <summary>
        /// Add a control point
        /// </summary>
        ADDCONTROLPOINT,
        /// <summary>
        /// Remove a control point
        /// </summary>
        REMOVECONTROLPOINT,
        /// <summary>
        /// Create a structure and add the first location for that structure
        /// </summary>
        CREATESTRUCTURE,
        /// <summary>
        /// Create a link to an adjacent location or structure,
        /// </summary>
        CREATELINK,
        /// <summary>
        /// Create a new location and link to it
        /// </summary>
        CREATELINKEDLOCATION,
        /// <summary>
        /// Cut a hole from the interior of an annotation
        /// </summary>
        CUTHOLE,
        /// <summary>
        /// Remove a hole from the interior of an annotation
        /// </summary>
        REMOVEHOLE,
        /// <summary>
        /// Trace a new path from the perimeter of an annotation to another point on the perimeter and replace points in between with the new path
        /// </summary>
        CHANGEBOUNDARY,
        /// <summary>
        /// Change the type of shape used to represent the location
        /// </summary>
        CHANGETYPE
    }



    /// <summary>
    /// Manage actions available to locations
    /// </summary>
    internal static class LocationActions
    {
        public static Cursor GetCursor(this LocationAction action)
        {
            return action switch
            {
                LocationAction.NONE => Cursors.Default,
                LocationAction.SCALETRANSLATE or LocationAction.TRANSLATE => Cursors.Hand,
                LocationAction.SCALE => Cursors.SizeAll,
                LocationAction.ADJUST => Cursors.SizeAll,
                LocationAction.ADDCONTROLPOINT => new Cursor(Viking.Properties.Resources.Create.Handle),
                LocationAction.REMOVECONTROLPOINT => Cursors.No,
                LocationAction.CREATELINK => new Cursor(Viking.Properties.Resources.Link.Handle),
                LocationAction.CREATELINKEDLOCATION => Cursors.Cross,
                LocationAction.CUTHOLE => new Cursor(Viking.Properties.Resources.Scissors2.Handle),
                LocationAction.REMOVEHOLE => new Cursor(Viking.Properties.Resources.PaintBucketFill.Handle),
                LocationAction.CHANGEBOUNDARY => Cursors.Cross,
                _ => Cursors.Default,
            };
        }

        /// <summary>
        /// Create a command for this action.
        /// </summary>
        /// <param name="action">The action</param>
        /// <param name="Parent">The control hosting the command we are creating</param>
        /// <param name="loc">The annotation we are creating the command for</param>
        /// <param name="volumePosition">The position in volume space the command should instantiate at.  For example a new annotation command would create the annotation at this point</param>
        /// <returns></returns>
        public static Viking.UI.Commands.Command CreateCommand(this LocationAction action,
                                                               Viking.UI.Controls.SectionViewerControl Parent,
                                                               LocationObj loc,
                                                               Geometry.Vector2 volumePosition)
        {
            Viking.UI.State.SelectedObject = null;
            //            CreateNewLinkedLocationCommand.LastEditedLocation = null; 

            return loc.TypeCode switch
            {
                LocationType.CIRCLE => CreateCommandForCircles(action, Parent, loc, volumePosition),
                LocationType.POLYLINE => CreateCommandForlineOrOpenCurve(action, Parent, loc, volumePosition),
                LocationType.OPENCURVE => CreateCommandForlineOrOpenCurve(action, Parent, loc, volumePosition),
                LocationType.CLOSEDCURVE => CreateCommandForClosedCurve(action, Parent, loc, volumePosition),
                LocationType.POLYGON or LocationType.CURVEPOLYGON => CreateCommandForPolygon(action, Parent, loc, volumePosition),
                LocationType.POINT => throw new NotImplementedException("No commands available for polygons"),
                _ => throw new NotImplementedException("Unexpected location type"),
            };
        }


        public static Viking.UI.Commands.Command CreateCommandForCircles(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc,
                                                                         Geometry.Vector2 volumePosition)
        {
            //I had to calculate this on the fly because if the databases VolumeShape was out of date it could cause large movements of the annotation during the command.
            IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number);
            Geometry.Vector2 VolumeCircleCenter;

            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.SCALETRANSLATE:
                case LocationAction.TRANSLATE:
                    // The command works in volume space, so it gets the circle's volume radius (mapped, not the mosaic Radius).
                    if (TryGetVolumeCircle(loc, section_mapper) is not Circle volumeCircle)
                        return null;

                    return new TranslateCircleLocationCommand(Parent,
                                                              volumeCircle,
                                                              volumePosition,
                                                              loc.Parent.Type.Color.ToXNAColor(1f),
                                                              (NewVolumePosition, NewMosaicPosition, NewRadius) => UpdateCircleLocationCallback(loc, section_mapper, NewVolumePosition, NewRadius));
                case LocationAction.SCALE:
                    VolumeCircleCenter = section_mapper.SectionToVolume(loc.Position);
                    return new ResizeCircleCommand(Parent,
                            System.Drawing.Color.FromArgb((int)loc.Parent.Type.Color).SetAlpha(0.5f),
                            VolumeCircleCenter,
                            (radius) => WebAnnotation.LocationActions.UpdateCircleLocationCallback(loc, section_mapper, VolumeCircleCenter, radius));
                case LocationAction.ADJUST:
                    return null;
                case LocationAction.CREATESTRUCTURE:

                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.CREATELINKEDLOCATION:
                    if (loc.Parent?.Type is null)
                        return null;

                    // The source circle is on another section: its volume radius comes from that section's own transform.
                    if (TryGetVolumeCircle(loc, Parent.Volume.GetSectionToVolumeTransform((int)loc.Z)) is not Circle sourceVolumeCircle)
                        return null;

                    return new TranslateCircleLocationCommand(Parent,
                                                                new Circle(volumePosition, sourceVolumeCircle.Radius),
                                                                loc.Parent.Type.Color.ToXNAColor(1f),
                                                                new TranslateCircleLocationCommand.OnCommandSuccess((NewVolumePosition, NewMosaicPosition, NewRadius) =>
                                                                    QueueLinkedCircle(Parent, loc, NewVolumePosition, NewRadius)));
                default:
                    return null;
            }
        }

        /// <summary>
        /// Commands for lines or open curves
        /// </summary>
        /// <param name="action"></param>
        /// <param name="Parent"></param>
        /// <param name="loc"></param>
        /// <param name="volumePosition"></param>
        /// <returns></returns>
        public static Viking.UI.Commands.Command CreateCommandForlineOrOpenCurve(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc,
                                                                         Geometry.Vector2 volumePosition)
        {
            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.TRANSLATE:
                    return new TranslateOpenCurveCommand(Parent,
                                                             volumePosition,
                                                             loc.MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             loc.Width ?? 16.0,
                                                             (VolumeControlPoints, MosaicControlPoints, LineWidth) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints, LineWidth));
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc.MosaicShape.ToPoints(),
                                                                      loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                      loc.Width ?? 16.0,
                                                                      IsClosedCurve(loc),
                                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.ADDCONTROLPOINT:
                    return new AddLineControlPointCommand(Parent,
                                                      loc.MosaicShape.ToPoints(),
                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.REMOVECONTROLPOINT:
                    return new RemoveLineControlPointCommand(Parent,
                                                      loc.MosaicShape.ToPoints(),
                                                      IsClosedCurve(loc),
                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.CREATELINKEDLOCATION:

                    //The section we are linking from is on another section, so we have to:
                    // 0. Position the mosaic shape where we want the command to begin
                    // 1. Warp the mosaic using the correct transform for the source section
                    // 2. Warp the volume shape back to our section using the current transform

                    IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                    Geometry.Vector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                    SqlGeometry MosaicShape = TryTransformMosaicShapeToSection(mapper, Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number), loc.MosaicShape.MoveTo(MosaicPosition), out SqlGeometry VolumeShape);
                    if (MosaicShape is null)
                        return null;

                    return new TranslateOpenCurveCommand(Parent,
                                                             volumePosition,
                                                             MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             loc.Width ?? 16.0,
                                                             (NewVolumeControlPoints, NewMosaicControlPoints, NewWidth) =>
                                                                {
                                                                    LocationObj newLoc = new(loc.Parent,
                                                                       MosaicShape,
                                                                       VolumeShape,
                                                                       Parent.Section.Number,
                                                                       loc.TypeCode);

                                                                    IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number);
                                                                    NewMosaicControlPoints = section_mapper.VolumeToSection(NewVolumeControlPoints);

                                                                    UpdateLineLocationNoSaveCallback(newLoc, NewVolumeControlPoints, NewMosaicControlPoints, NewWidth);
                                                                    Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [Parent, loc, newLoc]);
                                                                }
                                                             );
                default:
                    return null;
            }
        }

        /// <summary>
        /// Commands for closed curves
        /// </summary>
        /// <param name="action"></param>
        /// <param name="Parent"></param>
        /// <param name="loc"></param>
        /// <param name="volumePosition"></param>
        /// <returns></returns>
        public static Viking.UI.Commands.Command CreateCommandForClosedCurve(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc,
                                                                         Geometry.Vector2 volumePosition)
        {
            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.TRANSLATE:
                    return new TranslateClosedCurveCommand(Parent,
                                                             loc.MosaicShape.Centroid(),
                                                             loc.MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor().SetAlpha(0.5f),
                                                             loc.Width ?? 16.0,
                                                             (VolumeControlPoints, MosaicControlPoints, LineWidth) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints, LineWidth));
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc.MosaicShape.ToPoints(),
                                                                      loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                      Global.DefaultClosedLineWidth,
                                                                      IsClosedCurve(loc),
                                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.ADDCONTROLPOINT:
                    return new AddLineControlPointCommand(Parent,
                                                      loc.MosaicShape.ToPoints(),
                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.REMOVECONTROLPOINT:
                    return new RemoveLineControlPointCommand(Parent,
                                                      loc.MosaicShape.ToPoints(),
                                                      IsClosedCurve(loc),
                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.CREATELINKEDLOCATION:

                    //The section we are linking from is on another section, so we have to:
                    // 0. Position the mosaic shape where we want the command to begin
                    // 1. Warp the mosaic using the correct transform for the source section
                    // 2. Warp the volume shape back to our section using the current transform

                    IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                    Geometry.Vector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                    SqlGeometry MosaicShape = TryTransformMosaicShapeToSection(mapper, Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number), loc.MosaicShape.MoveTo(MosaicPosition), out SqlGeometry VolumeShape);
                    if (MosaicShape is null)
                        return null;

                    return new TranslateClosedCurveCommand(Parent,
                                                             MosaicShape.Centroid(),
                                                             MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             loc.Width ?? 16.0,
                                                             (NewVolumeControlPoints, NewMosaicControlPoints, NewWidth) =>
                                                             {
                                                                 LocationObj newLoc = new(loc.Parent,
                                                                    MosaicShape,
                                                                    VolumeShape,
                                                                    Parent.Section.Number,
                                                                    loc.TypeCode);

                                                                 IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number);
                                                                 NewMosaicControlPoints = section_mapper.VolumeToSection(NewVolumeControlPoints);

                                                                 UpdateLineLocationNoSaveCallback(newLoc, NewVolumeControlPoints, NewMosaicControlPoints, NewWidth);

                                                                 Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [Parent, loc, newLoc]);
                                                             }
                                                             );
                default:
                    return null;
            }
        }

        /// <summary>
        /// Commands for Polygons or closed curves
        /// </summary>
        /// <param name="action"></param>
        /// <param name="Parent"></param>
        /// <param name="loc"></param>
        /// <param name="volumePosition"></param>
        /// <returns></returns>
        public static Viking.UI.Commands.Command CreateCommandForPolygon(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc,
                                                                         Geometry.Vector2 volumePosition)
        {
            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.TRANSLATE:
                    return new TranslatePolygonCommand(Parent,
                                                               loc.MosaicShape.ToPolygon(),
                                                               volumePosition,
                                                               loc.Parent.Type.Color.ToXNAColor().SetAlpha(0.5f),
                                                               (mosaicPolygon, points) =>
                                                                    {
                                                                        SqlGeometry undoShape = loc.MosaicShape;
                                                                        loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, mosaicPolygon.ToSqlGeometry());
                                                                        if (AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                                                                            UndoManager.PushLocationShape("Move", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                                                                    });
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                    return new AdjustPolygonVertexCommand(Parent, loc.MosaicShape.ToPolygon(),
                                                                      loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                       (MosaicPolygon, VolumePolygon) =>
                                                                       {
                                                                           try
                                                                           {
                                                                               SqlGeometry undoShape = loc.MosaicShape;
                                                                               loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                                               Store.Locations.Save();
                                                                               UndoManager.PushLocationShape("Edit Shape", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                                                                           }
                                                                           catch (ArgumentException e)
                                                                           {
                                                                               MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                           }
                                                                           catch (System.ServiceModel.FaultException e)
                                                                           {
                                                                               AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                                                                           }
                                                                       });
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.ADDCONTROLPOINT:
                    return new AddPolygonVertexCommand(Parent,
                                                      loc.MosaicShape.ToPolygon(),
                                                      (MosaicPolygon, VolumePolygon) =>
                                                      {
                                                          try
                                                          {
                                                              SqlGeometry undoShape = loc.MosaicShape;
                                                              loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                              Store.Locations.Save();
                                                              UndoManager.PushLocationShape("Edit Shape", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                                                          }
                                                          catch (ArgumentException e)
                                                          {
                                                              MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                          }
                                                          catch (System.ServiceModel.FaultException e)
                                                          {
                                                              AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                                                          }
                                                      });
                case LocationAction.REMOVECONTROLPOINT:
                    return new RemovePolygonVertexCommand(Parent,
                                                      loc.MosaicShape.ToPolygon(),
                                                      (MosaicPolygon, VolumePolygon) =>
                                                      {
                                                          try
                                                          {
                                                              SqlGeometry undoShape = loc.MosaicShape;
                                                              loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                              Store.Locations.Save();
                                                              UndoManager.PushLocationShape("Edit Shape", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                                                          }
                                                          catch (ArgumentException e)
                                                          {
                                                              MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                          }
                                                          catch (System.ServiceModel.FaultException e)
                                                          {
                                                              AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                                                          }
                                                      });
                case LocationAction.CUTHOLE:
                    {
                        if (Global.PenMode)
                        {

                            return new CutHoleWithPenCommand(Parent,
                                                                loc.MosaicShape.ToPolygon(),
                                                                Microsoft.Xna.Framework.Color.White.SetAlpha(0.5f),//loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                volumePosition,
                                                                loc.Width ?? Global.DefaultClosedLineWidth,
                                                                (sender, volume_points) =>
                                                                {
                                                                    Geometry.Vector2[] mosaic_points = Parent.Section.ActiveSectionToVolumeTransform.VolumeToSection(volume_points);
                                                                    SqlGeometry updatedMosaicShape = loc.MosaicShape.AddInteriorPolygon(mosaic_points);
                                                                    SqlGeometry undoShape = loc.MosaicShape;

                                                                    try
                                                                    {
                                                                        loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, updatedMosaicShape);
                                                                    }
                                                                    catch (ArgumentException e)
                                                                    {
                                                                        MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                    }

                                                                    if (AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                                                                        UndoManager.PushLocationShape("Cut Hole", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                                                                }
                                                                );

                        }
                        else
                        {
                            return new PlaceClosedCurveCommand(Parent, Microsoft.Xna.Framework.Color.White, volumePosition, Global.DefaultClosedLineWidth, (sender, volume_points) =>
                            {
                                Geometry.Vector2[] mosaic_points = Parent.Section.ActiveSectionToVolumeTransform.VolumeToSection(volume_points);
                                SqlGeometry updatedMosaicShape = loc.MosaicShape.AddInteriorPolygon(mosaic_points);
                                SqlGeometry undoShape = loc.MosaicShape;

                                try
                                {
                                    loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, updatedMosaicShape);
                                }
                                catch (ArgumentException e)
                                {
                                    MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }

                                if (AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                                    UndoManager.PushLocationShape("Cut Hole", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                            }
                            );
                        }
                    }
                case LocationAction.REMOVEHOLE:
                    return new RemovePolygonHoleCommand(Parent,
                                                        loc.MosaicShape.ToPolygon(),
                                                        Parent.Section.ActiveSectionToVolumeTransform.VolumeToSection(volumePosition),
                                                        (MosaicPolygon, VolumePolygon) =>
                                                        {
                                                            SqlGeometry undoShape = loc.MosaicShape;
                                                            loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                            Store.Locations.Save();
                                                            UndoManager.PushLocationShape("Edit Shape", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode);
                                                        },
                                                        loc.VolumeShape?.ToPolygon(),
                                                        volumePosition);
                case LocationAction.CREATELINKEDLOCATION:
                    {
                        //The section we are linking from is on another section, so we have to:
                        // 0. Position the mosaic shape where we want the command to begin
                        // 1. Warp the mosaic using the correct transform for the source section
                        // 2. Warp the volume shape back to our section using the current transform

                        IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                        Geometry.Vector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                        SqlGeometry MosaicShape = TryTransformMosaicShapeToSection(mapper, Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number), loc.MosaicShape.MoveTo(MosaicPosition), out _);
                        if (MosaicShape is null)
                            return null;

                        Polygon mosaicPolygon = MosaicShape.ToPolygon();
                        Microsoft.Xna.Framework.Color color = WebAnnotation.View.LocationCanvasView.ColorForStructure(loc.Parent, 0.25f);

                        void PlaceLinkedMosaicPolygon(Polygon placedMosaicPolygon)
                        {
                            LocationObj newLoc = new(loc.Parent,
                                Parent.Section.Number,
                                loc.TypeCode);
                            try
                            {
                                newLoc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, placedMosaicPolygon.ToSqlGeometry());
                                Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [Parent, loc, newLoc]);
                            }
                            catch (ArgumentException e)
                            {
                                MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        if (!Global.AnnotationSettings.SegmentLinkedAnnotations || !Global.IsSegmentationServiceAvailable)
                        {
                            return new TranslatePolygonCommand(Parent,
                                mosaicPolygon,
                                volumePosition,
                                color,
                                (placedMosaicPolygon, _) => PlaceLinkedMosaicPolygon(placedMosaicPolygon));
                        }

                        Geometry.Vector2[] previewCentroids = [.. PolygonSegmentationPrompts.CreateMosaicForegroundPoints(mosaicPolygon)];
                        return new TranslatePolygonCommand(Parent,
                            mosaicPolygon,
                            volumePosition,
                            previewCentroids,
                            color,
                            (polygon, _) =>
                            {
                                Polygon volumeFallback = Parent.Section.ActiveSectionToVolumeTransform.TryMapShapeSectionToVolume(polygon);
                                if (volumeFallback is null || volumeFallback.ExteriorRing.Length < 3)
                                {
                                    PlaceLinkedMosaicPolygon(polygon);
                                    return;
                                }

                                // The dropped polygon is painted as foreground and segmented at once; Escape or the red X places it as dropped.
                                List<Geometry.Vector2[]> seedRings = [volumeFallback.ExteriorRing, .. volumeFallback.InteriorRings];
                                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                                Parent.CommandQueue.EnqueueCommand(new SegmentationPaintCommand(
                                    Parent,
                                    segmentedVolumePolygon =>
                                    {
                                        LocationObj newLoc = new(loc.Parent,
                                            Parent.Section.Number,
                                            loc.TypeCode);
                                        try
                                        {
                                            LocationShapeUpdate.AssignSimplifiedVolumePolygon(
                                                newLoc,
                                                segmentedVolumePolygon,
                                                Parent.Section.ActiveSectionToVolumeTransform,
                                                Parent.Downsample);
                                            Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [Parent, loc, newLoc]);
                                        }
                                        catch (ArgumentException e)
                                        {
                                            MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                    },
                                    channelManager,
                                    seedRings,
                                    onAbandoned: () => PlaceLinkedMosaicPolygon(polygon))
                                {
                                    ShowPenButtons = true
                                });
                            });
                    }
                case LocationAction.CHANGEBOUNDARY:
                    {
                        IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                        Geometry.Vector2 MosaicPosition = mapper.VolumeToSection(volumePosition);
                        //SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);

                        RetraceAndReplacePathCommand retracecmd = new(Parent,
                                                                 loc.MosaicShape.ToPolygon(),
                                                                 loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                 loc.Width ?? Global.DefaultClosedLineWidth,
                                                                 (sender, MosaicPolygon) =>
                                                                 {
                                                                     //Drawing from inside to outside:
                                                                     RetraceAndReplacePathCommand cmd = (RetraceAndReplacePathCommand)sender;

                                                                     Action? recordUndo = null;
                                                                     try
                                                                     {
                                                                         recordUndo = cmd.ApplyCut(loc);
                                                                     }
                                                                     catch (ArgumentException e)
                                                                     {
                                                                         MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                     }


                                                                     if (AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                                                                         recordUndo?.Invoke();

                                                                 }
                                                                 );
                        retracecmd.InitPath(new Geometry.Vector2[] { volumePosition });
                        return retracecmd;
                    }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Queues a circle on the open section, linked to <paramref name="source"/>.
        /// Called when the user releases a continue-last-trace or adjacent-disc drag.
        /// The click is mapped with the open section's transform only. A bad or missing section between <paramref name="source"/> and the open section does not have to inverse-map the point.
        /// </summary>
        private static void QueueLinkedCircle(Viking.UI.Controls.SectionViewerControl parent, LocationObj source, Geometry.Vector2 newVolumePosition, double newRadius)
        {
            if (source?.Parent is null || parent?.Section is null)
                return;

            IVolumeToSectionTransform destination = parent.Volume.GetSectionToVolumeTransform(parent.Section.Number);
            if (!destination.TryVolumeToSection(newVolumePosition, out Geometry.Vector2 mosaic))
            {
                MessageBox.Show(parent, "The chosen point is outside mappable volume space, location not created", "Recoverable Error");
                return;
            }

            // newRadius is a volume radius (the drag happened in volume space); the open section's mosaic radius is mapped from it.
            LocationObj newLoc = new(source.Parent, parent.Section.Number, source.TypeCode);
            if (!UpdateCircleLocationNoSaveCallback(newLoc, destination, newVolumePosition, newRadius))
            {
                MessageBox.Show(parent, "The circle is outside mappable volume space, location not created", "Recoverable Error");
                return;
            }

            parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [parent, source, newLoc]);
        }

        /// <summary>
        /// Warps a shape from the source section's mosaic space to the destination section's mosaic space, through volume space.
        /// Returns null, with a trace line, when any vertex of the shape lies outside either mapping, so a linked-location command is not created for a shape it cannot place.
        /// </summary>
        /// <param name="source">Section-to-volume transform of the section the shape comes from.</param>
        /// <param name="destination">Section-to-volume transform of the section the linked location is placed on.</param>
        /// <param name="sourceShape">The shape in the source section's mosaic space.</param>
        /// <param name="volumeShape">The shape in volume space, or null when <paramref name="sourceShape"/> lies partly outside <paramref name="source"/>.</param>
        /// <returns>The shape in the destination section's mosaic space, or null when it cannot be mapped.</returns>
        internal static SqlGeometry TryTransformMosaicShapeToSection(IVolumeToSectionTransform source,
                                                                     IVolumeToSectionTransform destination,
                                                                     SqlGeometry sourceShape,
                                                                     out SqlGeometry volumeShape)
        {
            volumeShape = source.TryMapShapeSectionToVolume(sourceShape);
            SqlGeometry? mosaicShape = volumeShape is null ? null : destination.TryMapShapeVolumeToSection(volumeShape);
            if (mosaicShape is null)
                System.Diagnostics.Trace.WriteLine("Linked location: shape lies partly outside the section mapping; no command created.", "WebAnnotation");

            return mosaicShape;
        }

        private static bool IsClosedCurve(LocationObj loc) => loc.TypeCode == LocationType.CLOSEDCURVE;

        private static void UpdateLineLocationCallback(LocationObj loc, Geometry.Vector2[] VolumeControlPoints, Geometry.Vector2[] MosaicControlPoints)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        private static void UpdateLineLocationNoSaveCallback(LocationObj loc, Geometry.Vector2[] VolumeControlPoints, Geometry.Vector2[] MosaicControlPoints)
        {
            SqlGeometry updatedMosaicShape = loc.TypeCode.GetShape(MosaicControlPoints);
            SqlGeometry updatedVolumeShape = loc.TypeCode.GetSmoothedShape(VolumeControlPoints);

            loc.VolumeShape = updatedVolumeShape;
            loc.MosaicShape = updatedMosaicShape;
        }

        private static void UpdateLineLocationCallback(LocationObj loc, Geometry.Vector2[] VolumeControlPoints, Geometry.Vector2[] MosaicControlPoints, double NewWidth)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints, NewWidth);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        private static void UpdateLineLocationNoSaveCallback(LocationObj loc, Geometry.Vector2[] VolumeControlPoints, Geometry.Vector2[] MosaicControlPoints, double NewWidth)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints);
            loc.Width = NewWidth;
        }

        /// <summary>
        /// Sets a circle from a circle in volume space (what the user drew on screen) and saves. The mosaic circle is derived by
        /// mapping, so its radius follows the section's scale. See <see cref="UpdateCircleLocationNoSaveCallback(LocationObj, IVolumeToSectionTransform, Geometry.Vector2, double)"/>.
        /// </summary>
        public static void UpdateCircleLocationCallback(LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 volumeCenter, double volumeRadius)
        {
            if (UpdateCircleLocationNoSaveCallback(loc, mapper, volumeCenter, volumeRadius))
                _ = AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        public static void UpdateCircleLocationCallback(LocationObj loc, Geometry.Vector2 WorldPosition, Geometry.Vector2 MosaicPosition)
        {
            UpdateCircleLocationNoSaveCallback(loc, WorldPosition, MosaicPosition);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        public static void UpdateCircleLocationNoSaveCallback(LocationObj loc, Geometry.Vector2 WorldPosition, Geometry.Vector2 MosaicPosition)
        {
            loc.MosaicShape = loc.MosaicShape.MoveTo(MosaicPosition);
            loc.VolumeShape = loc.VolumeShape.MoveTo(WorldPosition);


        }

        /// <summary>
        /// Sets a circle from a circle in volume space, which is what the user sees and draws. <c>VolumeShape</c> is that circle;
        /// <c>MosaicShape</c> is the same circle mapped through <paramref name="mapper"/> with the five-point scale rule
        /// (<see cref="Geometry.CircleMapping"/>), so the mosaic radius follows the section's scale and is never a copy of the volume radius.
        /// Radii below <see cref="WebAnnotation.Global.MinRadius"/> are raised to it. Nothing is changed when the circle cannot be mapped.
        /// </summary>
        /// <returns>False when the circle could not be mapped into the section.</returns>
        public static bool UpdateCircleLocationNoSaveCallback(LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 volumeCenter, double volumeRadius)
        {
            volumeRadius = Math.Max(volumeRadius, WebAnnotation.Global.MinRadius);
            if (mapper.TryMapShapeVolumeToSection(new Circle(volumeCenter, volumeRadius)) is not Circle mosaic)
            {
                System.Diagnostics.Trace.WriteLine($"Circle {loc.ID} could not be mapped into section {loc.Z}; it was left unchanged.", "WebAnnotation");
                return false;
            }

            loc.MosaicShape = SqlGeometryUtils.Extensions.ToCircle(mosaic.Center.X, mosaic.Center.Y, loc.Z, mosaic.Radius);
            loc.VolumeShape = SqlGeometryUtils.Extensions.ToCircle(volumeCenter.X, volumeCenter.Y, loc.Z, volumeRadius);
            return true;
        }

        /// <summary>
        /// Sets a circle from a circle in mosaic space (for example "Convert to circle" from a location's mosaic radius).
        /// <c>MosaicShape</c> is that circle; <c>VolumeShape</c> is the same circle mapped to volume space with the five-point
        /// scale rule. Radii below <see cref="WebAnnotation.Global.MinRadius"/> are raised to it. Nothing is changed when it cannot be mapped.
        /// </summary>
        /// <returns>False when the circle could not be mapped onto the volume.</returns>
        public static bool UpdateCircleLocationFromMosaicNoSaveCallback(LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 mosaicCenter, double mosaicRadius)
        {
            mosaicRadius = Math.Max(mosaicRadius, WebAnnotation.Global.MinRadius);
            if (mapper.TryMapShapeSectionToVolume(new Circle(mosaicCenter, mosaicRadius)) is not Circle volume)
            {
                System.Diagnostics.Trace.WriteLine($"Circle {loc.ID} could not be mapped onto the volume from section {loc.Z}; it was left unchanged.", "WebAnnotation");
                return false;
            }

            loc.MosaicShape = SqlGeometryUtils.Extensions.ToCircle(mosaicCenter.X, mosaicCenter.Y, loc.Z, mosaicRadius);
            loc.VolumeShape = SqlGeometryUtils.Extensions.ToCircle(volume.Center.X, volume.Center.Y, loc.Z, volume.Radius);
            return true;
        }

        /// <summary>
        /// The location's circle in volume space, mapped from its mosaic circle (centre <see cref="LocationObj.Position"/>,
        /// radius <see cref="LocationObj.Radius"/>, which is a mosaic radius) with the five-point scale rule. The stored
        /// <c>VolumeShape</c> is not used: the server does not return a circle's volume radius, and a stale shape moves a circle during a command.
        /// </summary>
        /// <returns>The circle, or null when it cannot be mapped onto the volume.</returns>
        public static Circle? TryGetVolumeCircle(LocationObj loc, IVolumeToSectionTransform mapper)
        {
            if (mapper.TryMapShapeSectionToVolume(new Circle(loc.Position, loc.Radius)) is not Circle volume)
                return null;

            return new Circle(new Geometry.Vector2(volume.Center.X, volume.Center.Y), volume.Radius);
        }
    }
}

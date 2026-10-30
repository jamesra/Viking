using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using VikingXNAWinForms;
using Viking.DependencyInjection;
using Viking.Services.Grpc;
using WebAnnotation.UI.Commands;
using WebAnnotation.UI.Commands.Segmentation;
using WebAnnotation.ViewModel;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using VikingXNAGraphics;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Vector2 = Geometry.Vector2;

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
        /// Link two existing locations (or a structure). Does not create a new location.
        /// </summary>
        CREATELINK,
        /// <summary>
        /// Create a new location on another section and link it to the source. WinForms CreateCommand is #if NETFRAMEWORK only.
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
        /// WinForms-only. Jotunn uses AnnotationToolHost instead of these Command types.
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
            // The command starts from the circle the forward owner draws and saves what the user leaves on screen through the
            // inverse owner; the stored VolumeShape is never read, as a stale one moved the circle during the command.
            IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number);

            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.SCALETRANSLATE:
                case LocationAction.TRANSLATE:
                    if (TryGetVolumeCircle(loc, section_mapper) is not Circle volumeCircle)
                        return null;

                    return new TranslateCircleLocationCommand(Parent,
                                                              volumeCircle,
                                                              volumePosition,
                                                              loc.Parent.Type.Color.ToXNAColor(1f),
                                                              (NewVolumePosition, NewRadius) => UpdateCircleLocationCallback(Parent, loc, section_mapper, NewVolumePosition, NewRadius));
                case LocationAction.SCALE:
                    if (TryGetVolumeCircle(loc, section_mapper) is not Circle resizedCircle)
                        return null;

                    return new ResizeCircleCommand(Parent,
                            System.Drawing.Color.FromArgb((int)loc.Parent.Type.Color).SetAlpha(0.5f),
                            resizedCircle.Center,
                            (radius) => WebAnnotation.LocationActions.UpdateCircleLocationCallback(Parent, loc, section_mapper, resizedCircle.Center, radius));
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
                                                                new TranslateCircleLocationCommand.OnCommandSuccess((NewVolumePosition, NewRadius) =>
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
                    {
                        IVolumeToSectionTransform section_mapper = Parent.Section.ActiveSectionToVolumeTransform;
                        if (EditableGeometry(loc, section_mapper) is not LocationVolumeGeometry geometry)
                            return null;

                        return new TranslateOpenCurveCommand(Parent,
                                                                 volumePosition,
                                                                 [.. geometry.ControlPoints],
                                                                 loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                 geometry.Width,
                                                                 (VolumeControlPoints, LineWidth) => UpdateLineLocationCallback(Parent, loc, section_mapper, VolumeControlPoints, LineWidth));
                    }
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                case LocationAction.ADDCONTROLPOINT:
                case LocationAction.REMOVECONTROLPOINT:
                    return CreateControlPointCommand(action, Parent, loc, adjustLineWidth: null);
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.CREATELINKEDLOCATION:
                    {
                        // The source is on another section: the command starts from the line as that section draws it, centred
                        // on the click, and the result is mapped back to this section's mosaic.
                        if (LinkedLineStart(Parent, loc, volumePosition) is not (Geometry.Vector2[] startPoints, double startWidth))
                            return null;

                        return new TranslateOpenCurveCommand(Parent,
                                                                 volumePosition,
                                                                 startPoints,
                                                                 loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                 startWidth,
                                                                 (NewVolumeControlPoints, NewWidth) => QueueLinkedLine(Parent, loc, NewVolumeControlPoints, NewWidth));
                    }
                default:
                    return null;
            }
        }

        /// <summary>
        /// The adjust, add and remove control point commands of a line or curve. Each starts from the control points the
        /// forward owner draws and saves the edited volume control points through the inverse owner
        /// (<see cref="UpdateLineLocationCallback"/>); none starts when the location cannot be mapped exactly.
        /// </summary>
        /// <param name="adjustLineWidth">The line width the adjust preview draws; null for the location's drawn width.</param>
        private static Viking.UI.Commands.Command? CreateControlPointCommand(LocationAction action,
                                                                            Viking.UI.Controls.SectionViewerControl Parent,
                                                                            LocationObj loc,
                                                                            double? adjustLineWidth)
        {
            IVolumeToSectionTransform section_mapper = Parent.Section.ActiveSectionToVolumeTransform;
            if (EditableGeometry(loc, section_mapper) is not LocationVolumeGeometry geometry)
                return null;

            Geometry.Vector2[] controlPoints = [.. geometry.ControlPoints];
            void Save(Geometry.Vector2[] VolumeControlPoints) => UpdateLineLocationCallback(Parent, loc, section_mapper, VolumeControlPoints, null);

            return action switch
            {
                LocationAction.ADJUST => new AdjustCurveControlPointCommand(Parent, controlPoints,
                                                                          loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                          adjustLineWidth ?? geometry.Width,
                                                                          IsClosedCurve(loc),
                                                                          Save),
                LocationAction.ADDCONTROLPOINT => new AddLineControlPointCommand(Parent, controlPoints, Save),
                LocationAction.REMOVECONTROLPOINT => new RemoveLineControlPointCommand(Parent, controlPoints, IsClosedCurve(loc), Save),
                _ => null,
            };
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
                    {
                        IVolumeToSectionTransform section_mapper = Parent.Section.ActiveSectionToVolumeTransform;
                        if (EditableGeometry(loc, section_mapper) is not LocationVolumeGeometry geometry)
                            return null;

                        return new TranslateClosedCurveCommand(Parent,
                                                                 volumePosition,
                                                                 [.. geometry.ControlPoints],
                                                                 loc.Parent.Type.Color.ToXNAColor().SetAlpha(0.5f),
                                                                 geometry.Width,
                                                                 (VolumeControlPoints, LineWidth) => UpdateLineLocationCallback(Parent, loc, section_mapper, VolumeControlPoints, LineWidth));
                    }
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                case LocationAction.ADDCONTROLPOINT:
                case LocationAction.REMOVECONTROLPOINT:
                    return CreateControlPointCommand(action, Parent, loc, adjustLineWidth: Global.DefaultClosedLineWidth);
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.CREATELINKEDLOCATION:
                    {
                        if (LinkedLineStart(Parent, loc, volumePosition) is not (Geometry.Vector2[] startPoints, double startWidth))
                            return null;

                        return new TranslateClosedCurveCommand(Parent,
                                                                 volumePosition,
                                                                 startPoints,
                                                                 loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                 startWidth,
                                                                 (NewVolumeControlPoints, NewWidth) => QueueLinkedLine(Parent, loc, NewVolumeControlPoints, NewWidth));
                    }
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
                    // Mosaic-first: the command moves the mosaic polygon by the inverse of the volume drag and previews it
                    // through the forward owner, so what is saved is what was shown.
                    return new TranslatePolygonCommand(Parent,
                                                               loc.MosaicShape.ToPolygon(),
                                                               volumePosition,
                                                               loc.Parent.Type.Color.ToXNAColor().SetAlpha(0.5f),
                                                               (mosaicPolygon, points) =>
                                                                    {
                                                                        SqlGeometry undoShape = loc.MosaicShape.ToSqlGeometry();
                                                                        try
                                                                        {
                                                                            loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, mosaicPolygon.ToSqlGeometry());
                                                                        }
                                                                        catch (ArgumentException e)
                                                                        {
                                                                            ShowPolygonSaveError(Parent, e);
                                                                            return;
                                                                        }

                                                                        _ = SaveThen(() => UndoManager.PushLocationShape("Move", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode));
                                                                    })
                    {
                        PreviewType = loc.TypeCode
                    };
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                case LocationAction.ADDCONTROLPOINT:
                case LocationAction.REMOVECONTROLPOINT:
                    {
                        // The commands edit the polygon the forward owner draws and hand back the edit mapped to mosaic by
                        // the inverse owner (AnnotationCommandBase.TryApplyInSection); none starts on an unmappable polygon.
                        if (EditableGeometry(loc, Parent.Section.ActiveSectionToVolumeTransform) is not LocationVolumeGeometry geometry)
                            return null;

                        void Save(Polygon MosaicPolygon, Polygon VolumePolygon) => SaveMosaicPolygonEdit(Parent, loc, MosaicPolygon);

                        return action switch
                        {
                            LocationAction.ADJUST => new AdjustPolygonVertexCommand(Parent, geometry.ControlPolygon(), loc.Parent.Type.Color.ToXNAColor(0.5f), Save),
                            LocationAction.ADDCONTROLPOINT => new AddPolygonVertexCommand(Parent, geometry.ControlPolygon(), Save),
                            _ => new RemovePolygonVertexCommand(Parent, geometry.ControlPolygon(), Save),
                        };
                    }
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
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
                                                                    SqlGeometry undoShape = loc.MosaicShape.ToSqlGeometry();

                                                                    try
                                                                    {
                                                                        loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, updatedMosaicShape);
                                                                    }
                                                                    catch (ArgumentException e)
                                                                    {
                                                                        MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                    }

                                                                    _ = SaveThen(() => UndoManager.PushLocationShape("Cut Hole", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode));
                                                                }
                                                                );

                        }
                        else
                        {
                            return new PlaceClosedCurveCommand(Parent, Microsoft.Xna.Framework.Color.White, volumePosition, Global.DefaultClosedLineWidth, (sender, volume_points) =>
                            {
                                Geometry.Vector2[] mosaic_points = Parent.Section.ActiveSectionToVolumeTransform.VolumeToSection(volume_points);
                                SqlGeometry updatedMosaicShape = loc.MosaicShape.AddInteriorPolygon(mosaic_points);
                                SqlGeometry undoShape = loc.MosaicShape.ToSqlGeometry();

                                try
                                {
                                    loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, updatedMosaicShape);
                                }
                                catch (ArgumentException e)
                                {
                                    MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }

                                _ = SaveThen(() => UndoManager.PushLocationShape("Cut Hole", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode));
                            }
                            );
                        }
                    }
                case LocationAction.REMOVEHOLE:
                    {
                        // Mosaic-first: the hole is removed from the stored polygon. The click is matched in mosaic space (when
                        // it maps) and against the polygon the forward owner draws, never the stored volume cache.
                        IVolumeToSectionTransform section_mapper = Parent.Section.ActiveSectionToVolumeTransform;
                        Geometry.Vector2 mosaicClick = LocationVolumeGeometry.TryMapPointsToMosaic([volumePosition], section_mapper, out Geometry.Vector2[] mappedClick, out _)
                            ? mappedClick[0]
                            : new Geometry.Vector2(double.NaN, double.NaN);
                        return new RemovePolygonHoleCommand(Parent,
                                                            loc.MosaicShape.ToPolygon(),
                                                            mosaicClick,
                                                            MosaicPolygon => SaveMosaicPolygonEdit(Parent, loc, MosaicPolygon),
                                                            EditableGeometry(loc, section_mapper)?.ControlPolygon(),
                                                            volumePosition);
                    }
                case LocationAction.CREATELINKEDLOCATION:
                    {
                        //The section we are linking from is on another section, so we have to:
                        // 0. Position the mosaic shape where we want the command to begin
                        // 1. Warp the mosaic using the correct transform for the source section
                        // 2. Warp the volume shape back to our section using the current transform

                        IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                        if (!LocationVolumeGeometry.TryMapPointsToMosaic([volumePosition], mapper, out Geometry.Vector2[] mosaicPosition, out _))
                            return null;

                        SqlGeometry MosaicShape = TryTransformMosaicShapeToSection(mapper, Parent.Volume.GetSectionToVolumeTransform(Parent.Section.Number), loc.MosaicShape.MoveTo(mosaicPosition[0]).ToSqlGeometry(), out _);
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
                                IReadOnlyList<Geometry.Vector2> foreground = PolygonSegmentationPrompts.CreateVolumeForegroundPoints(
                                    polygon,
                                    Parent.Section.ActiveSectionToVolumeTransform);

                                if (foreground.Count == 0)
                                {
                                    PlaceLinkedMosaicPolygon(polygon);
                                    return;
                                }

                                Polygon volumeFallback = LocationVolumeGeometry.TryMapPolygon(polygon, Parent.Section.ActiveSectionToVolumeTransform);
                                var channelManager = ServiceLocator.GetRequiredService<IGrpcChannelManager>();
                                Parent.CommandQueue.EnqueueCommand(new SegmentationCommand(
                                    Parent,
                                    foreground,
                                    Array.Empty<Geometry.Vector2>(),
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
                                    loc.Parent.TypeID,
                                    loc.ID,
                                    loc.ParentID,
                                    volumeFallback)
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

                                                                     Action recordUndo = null;
                                                                     try
                                                                     {
                                                                         recordUndo = cmd.ApplyCut(loc);
                                                                     }
                                                                     catch (ArgumentException e)
                                                                     {
                                                                         MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                     }


                                                                     _ = SaveThen(recordUndo);

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
        /// Saves a polygon edit already mapped to mosaic (by the inverse owner, or made in mosaic space) as the location's
        /// shape, the forward owner deriving its volume shape, then saves the store and records the undo step. An invalid or
        /// unmappable shape is shown and not saved. Called by the polygon vertex and hole commands.
        /// </summary>
        private static void SaveMosaicPolygonEdit(Viking.UI.Controls.SectionViewerControl Parent, LocationObj loc, Polygon MosaicPolygon)
        {
            SqlGeometry undoShape = loc.MosaicShape.ToSqlGeometry();
            try
            {
                loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
            }
            catch (ArgumentException e)
            {
                ShowPolygonSaveError(Parent, e);
                return;
            }

            _ = SaveThen(() => UndoManager.PushLocationShape("Edit Shape", loc, Parent.Section.ActiveSectionToVolumeTransform, undoShape, loc.TypeCode));
        }

        /// <summary>
        /// Shows the error box for a polygon edit rejected with an <see cref="ArgumentException"/>, such as an invalid shape from
        /// SetShapeFromGeometryInSection or RetraceAndReplacePathCommand.ApplyCut. Called by the polygon edit callbacks in
        /// <see cref="CreateCommandForPolygon"/>; each caller decides what happens after the box closes.
        /// The WPF windows show the same box through Extensions.TrySetShapeFromGeometryInSectionShowErrorDialog.
        /// </summary>
        /// <param name="parent">The section viewer that owns the box.</param>
        /// <param name="e">The rejection; its message is the box text.</param>
        private static void ShowPolygonSaveError(IWin32Window parent, ArgumentException e) =>
            MessageBox.Show(parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);

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

            // newRadius is a volume radius (the drag happened in volume space); the open section's mosaic radius is mapped from it.
            LocationObj newLoc = new(source.Parent, parent.Section.Number, source.TypeCode);
            if (!UpdateCircleLocationNoSaveCallback(newLoc, destination, newVolumePosition, newRadius, out string failure))
            {
                MessageBox.Show(parent, failure + " The location was not created.", "Recoverable Error");
                return;
            }

            parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [parent, source, newLoc]);
        }

        /// <summary>
        /// Saves pending location edits, then runs <paramref name="onSaved"/> only if the save succeeded. Lets a command's
        /// synchronous completion callback record its undo entry after the asynchronous gRPC save without blocking the UI thread.
        /// </summary>
        private static async Task SaveThen(Action onSaved)
        {
            if (await AnnotationOverlay.SaveLocationsWithMessageBoxOnError())
                onSaved?.Invoke();
        }

        private static bool IsClosedCurve(LocationObj loc) => loc.TypeCode == LocationType.CLOSEDCURVE;

        /// <summary>
        /// The location's geometry as the forward owner draws it, for a command to start editing from; null, so no command
        /// starts, when it cannot be mapped exactly (the failure policy's extrapolated outline is not editable).
        /// </summary>
        private static LocationVolumeGeometry? EditableGeometry(LocationObj loc, IVolumeToSectionTransform transform)
        {
            try
            {
                LocationVolumeGeometry geometry = LocationVolumeGeometry.For(loc, transform);
                return geometry.IsExact ? geometry : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Where a line or curve linked from <paramref name="source"/> on another section starts: the source's control points
        /// as its own section draws them, moved so their centroid is at <paramref name="volumePosition"/>, and its drawn
        /// width. Null when the source cannot be mapped.
        /// </summary>
        private static (Geometry.Vector2[] Points, double Width)? LinkedLineStart(Viking.UI.Controls.SectionViewerControl parent, LocationObj source, Geometry.Vector2 volumePosition)
        {
            if (EditableGeometry(source, parent.Volume.GetSectionToVolumeTransform((int)source.Z)) is not LocationVolumeGeometry geometry)
                return null;

            Geometry.Vector2[] points = [.. geometry.ControlPoints];
            Geometry.Vector2 offset = volumePosition - points.ToSqlGeometry().Centroid();
            return ([.. points.Select(p => p + offset)], geometry.Width);
        }

        /// <summary>
        /// Queues a line or curve on the open section, linked to <paramref name="source"/>, from the control points and width
        /// the user left on screen, mapped back to this section's mosaic by the inverse owner. Shows why when they cannot be.
        /// </summary>
        private static void QueueLinkedLine(Viking.UI.Controls.SectionViewerControl parent, LocationObj source, Geometry.Vector2[] volumeControlPoints, double volumeWidth)
        {
            if (source?.Parent is null || parent?.Section is null)
                return;

            LocationObj newLoc = new(source.Parent, parent.Section.Number, source.TypeCode);
            if (!SaveVolumeLineEdit(newLoc, parent.Section.ActiveSectionToVolumeTransform, volumeControlPoints, volumeWidth, out string failure))
            {
                ShowUnmappedEdit(parent, failure, "Could not create linked annotation");
                return;
            }

            parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [parent, source, newLoc]);
        }

        /// <summary>
        /// Saves a line or curve the user edited in volume space: the control points and width are mapped back to mosaic by the
        /// inverse owner (<see cref="LocationVolumeGeometry.ToMosaic(LocationType, IReadOnlyList{Geometry.Vector2}, IVolumeToSectionTransform, double?, IReadOnlyCollection{Geometry.Vector2[]})"/>),
        /// so the mosaic width is the volume width divided by the local scale, and the volume shape is the forward owner's.
        /// Called by the line and curve command callbacks.
        /// </summary>
        /// <param name="volumeWidth">The width the user saw, in volume units; null keeps the location's width.</param>
        /// <param name="failure">Why the edit could not be saved, for the user; null on success.</param>
        /// <returns>False, with the location unchanged, when any point cannot be mapped.</returns>
        internal static bool SaveVolumeLineEdit(LocationObj loc, IVolumeToSectionTransform transform, Geometry.Vector2[] volumeControlPoints, double? volumeWidth, out string failure)
        {
            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(loc.TypeCode, volumeControlPoints, transform, volumeWidth);
            failure = mosaic.Failure;
            if (!mosaic.Succeeded)
                return false;

            try
            {
                return loc.TryApply(mosaic, transform);
            }
            catch (ArgumentException e)
            {
                failure = e.Message;
                return false;
            }
        }

        /// <summary>Saves a line or curve edit (<see cref="SaveVolumeLineEdit"/>) and the store, or shows why it was not saved.</summary>
        private static void UpdateLineLocationCallback(IWin32Window owner, LocationObj loc, IVolumeToSectionTransform transform, Geometry.Vector2[] volumeControlPoints, double? volumeWidth)
        {
            if (SaveVolumeLineEdit(loc, transform, volumeControlPoints, volumeWidth, out string failure))
                _ = AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
            else
                ShowUnmappedEdit(owner, failure, "Could not save annotation");
        }

        /// <summary>
        /// Sets a circle from a circle in volume space (what the user drew on screen) and saves; see
        /// <see cref="UpdateCircleLocationNoSaveCallback(LocationObj, IVolumeToSectionTransform, Geometry.Vector2, double, out string)"/>.
        /// When the circle cannot be mapped the location keeps its old circle and <paramref name="owner"/> shows why.
        /// </summary>
        public static void UpdateCircleLocationCallback(IWin32Window owner, LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 volumeCenter, double volumeRadius)
        {
            if (UpdateCircleLocationNoSaveCallback(loc, mapper, volumeCenter, volumeRadius, out string failure))
                _ = AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
            else
                ShowUnmappedEdit(owner, failure, "Could not save circle");
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
            volumeShape = LocationVolumeGeometry.TryMapControlShape(sourceShape, source);
            LocationType type = sourceShape.GeometryType() == SupportedGeometryType.CURVEPOLYGON ? LocationType.CIRCLE : LocationType.POLYGON;
            MosaicGeometry? mosaic = volumeShape is null ? null : LocationVolumeGeometry.ToMosaic(type, volumeShape, destination);
            if (mosaic?.Succeeded != true)
                System.Diagnostics.Trace.WriteLine($"Linked location: shape lies partly outside the section mapping; no command created. {mosaic?.Failure}", "WebAnnotation");

            return mosaic?.Shape;
        }

        /// <summary>
        /// Sets a circle from a circle in volume space, which is what the user sees and draws. The inverse owner
        /// (<see cref="LocationVolumeGeometry.ToMosaic(Circle, IVolumeToSectionTransform)"/>) maps it back to the mosaic circle
        /// that is saved, whose forward mapping is the drawn circle, and the forward owner derives the volume shape
        /// (<c>.cursor/rules/annotation-volume-geometry.mdc</c>, <c>circle-mapping-scale.mdc</c>). Radii below
        /// <see cref="WebAnnotation.Global.MinRadius"/> are raised to it. Nothing is changed when the circle cannot be mapped.
        /// </summary>
        /// <param name="failure">Why the circle could not be mapped, for the user; null on success.</param>
        /// <returns>False when the circle could not be mapped into the section.</returns>
        public static bool UpdateCircleLocationNoSaveCallback(LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 volumeCenter, double volumeRadius, out string failure)
        {
            volumeRadius = Math.Max(volumeRadius, WebAnnotation.Global.MinRadius);
            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(new Circle(volumeCenter, volumeRadius), mapper);
            failure = mosaic.Failure;
            if (!mosaic.Succeeded)
            {
                System.Diagnostics.Trace.WriteLine($"Circle {loc.ID} could not be mapped into section {loc.Z}; it was left unchanged: {failure}", "WebAnnotation");
                return false;
            }

            try
            {
                return loc.TryApply(mosaic, mapper);
            }
            catch (ArgumentException e)
            {
                failure = e.Message;
                return false;
            }
        }

        /// <inheritdoc cref="UpdateCircleLocationNoSaveCallback(LocationObj, IVolumeToSectionTransform, Geometry.Vector2, double, out string)"/>
        public static bool UpdateCircleLocationNoSaveCallback(LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 volumeCenter, double volumeRadius) =>
            UpdateCircleLocationNoSaveCallback(loc, mapper, volumeCenter, volumeRadius, out _);

        /// <summary>
        /// Sets a circle from a circle in mosaic space (for example "Convert to circle" from a location's mosaic centre and
        /// radius). <c>MosaicShape</c> is that circle and the forward owner derives the volume shape. Radii below
        /// <see cref="WebAnnotation.Global.MinRadius"/> are raised to it. Nothing is changed when it cannot be mapped.
        /// </summary>
        /// <returns>False when the circle could not be mapped onto the volume.</returns>
        public static bool UpdateCircleLocationFromMosaicNoSaveCallback(LocationObj loc, IVolumeToSectionTransform mapper, Geometry.Vector2 mosaicCenter, double mosaicRadius)
        {
            mosaicRadius = Math.Max(mosaicRadius, WebAnnotation.Global.MinRadius);
            try
            {
                loc.SetShapeFromCircleInSection(mapper, new Circle(mosaicCenter, mosaicRadius));
                return true;
            }
            catch (ArgumentException e)
            {
                System.Diagnostics.Trace.WriteLine($"Circle {loc.ID} could not be mapped onto the volume from section {loc.Z}; it was left unchanged: {e.Message}", "WebAnnotation");
                return false;
            }
        }

        /// <summary>
        /// Tells the user that an edit was not saved because it could not be mapped back to section coordinates, and why
        /// (<see cref="MosaicGeometry.Failure"/>); the location keeps its previous shape. Called by the command callbacks here
        /// and by the commands that save through <see cref="LocationVolumeGeometry.ToMosaic(LocationType, SqlGeometry, IVolumeToSectionTransform, double?)"/>.
        /// </summary>
        internal static void ShowUnmappedEdit(IWin32Window owner, string failure, string caption)
        {
            System.Diagnostics.Trace.WriteLine($"{caption}: {failure}", "WebAnnotation");
            MessageBox.Show(owner, failure + " The annotation keeps its previous shape.", caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// The location's circle in volume space as the forward owner draws it (<see cref="LocationVolumeGeometry.Circle"/>:
        /// centre <see cref="LocationObj.Position"/> mapped, radius by the five-point scale rule). The stored
        /// <c>VolumeShape</c> is not used: a stale shape moves a circle during a command.
        /// </summary>
        /// <returns>The circle, or null when it cannot be mapped exactly onto the volume.</returns>
        public static Circle? TryGetVolumeCircle(LocationObj loc, IVolumeToSectionTransform mapper)
        {
            try
            {
                LocationVolumeGeometry geometry = LocationVolumeGeometry.For(loc, mapper);
                return geometry.IsExact ? geometry.Circle : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}

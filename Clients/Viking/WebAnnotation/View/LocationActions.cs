using Viking.AnnotationServiceTypes.Interfaces;
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Windows.Forms;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.UI.Commands;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

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
    static class LocationActions
    {
        public static Cursor GetCursor(this LocationAction action)
        {
            switch (action)
            {
                case LocationAction.NONE:
                    return Cursors.Default;
                case LocationAction.SCALETRANSLATE:
                case LocationAction.TRANSLATE:
                    return Cursors.Hand;
                case LocationAction.SCALE:
                    return Cursors.SizeAll;
                case LocationAction.ADJUST:
                    return Cursors.SizeAll;
                case LocationAction.ADDCONTROLPOINT:
                    return new Cursor(Viking.Properties.Resources.Create.Handle);
                case LocationAction.REMOVECONTROLPOINT:
                    return Cursors.No;
                case LocationAction.CREATELINK:
                    return new Cursor(Viking.Properties.Resources.Link.Handle);
                case LocationAction.CREATELINKEDLOCATION:
                    return Cursors.Cross;
                case LocationAction.CUTHOLE:
                    return new Cursor(Viking.Properties.Resources.Scissors2.Handle);
                case LocationAction.REMOVEHOLE:
                    return new Cursor(Viking.Properties.Resources.PaintBucketFill.Handle);
                case LocationAction.CHANGEBOUNDARY:
                    return Cursors.Cross;
                default:
                    return Cursors.Default;
            }
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
                                                               GridVector2 volumePosition)
        {
            Viking.UI.State.SelectedObject = null;
            //            CreateNewLinkedLocationCommand.LastEditedLocation = null; 

            switch (loc.TypeCode)
            {
                case LocationType.CIRCLE:
                    return CreateCommandForCircles(action, Parent, loc, volumePosition);
                case LocationType.POLYLINE:
                    return CreateCommandForlineOrOpenCurve(action, Parent, loc, volumePosition);
                case LocationType.OPENCURVE:
                    return CreateCommandForlineOrOpenCurve(action, Parent, loc, volumePosition);
                case LocationType.CLOSEDCURVE:
                    return CreateCommandForClosedCurve(action, Parent, loc, volumePosition);
                case LocationType.POLYGON:
                case LocationType.CURVEPOLYGON:
                    return CreateCommandForPolygon(action, Parent, loc, volumePosition);
                case LocationType.POINT:
                    throw new NotImplementedException("No commands available for polygons");
                default:
                    throw new NotImplementedException("Unexpected location type");
            }
        }


        public static Viking.UI.Commands.Command CreateCommandForCircles(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc,
                                                                         GridVector2 volumePosition)
        {
            //I had to calculate this on the fly because if the databases VolumeShape was out of date it could cause large movements of the annotation during the command.
            IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform((int)Parent.Section.Number);
            GridVector2 VolumeCircleCenter;

            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.SCALETRANSLATE:
                case LocationAction.TRANSLATE:
                    VolumeCircleCenter = section_mapper.SectionToVolume(loc.Position);
                    return new TranslateCircleLocationCommand(Parent,
                                                              new GridCircle(VolumeCircleCenter, loc.Radius),
                                                              volumePosition,
                                                              loc.Parent.Type.Color.ToXNAColor(1f),
                                                              (NewVolumePosition, NewMosaicPosition, NewRadius) => UpdateCircleLocationCallback(loc, NewVolumePosition, NewMosaicPosition, NewRadius));
                case LocationAction.SCALE:
                    VolumeCircleCenter = section_mapper.SectionToVolume(loc.Position);
                    return new ResizeCircleCommand(Parent,
                            System.Drawing.Color.FromArgb((int)loc.Parent.Type.Color).SetAlpha(0.5f),
                            VolumeCircleCenter,
                            (radius) =>
                            {
                                WebAnnotation.LocationActions.UpdateCircleLocationCallback(loc, loc.VolumePosition, loc.Position, radius);
                            }); 
                case LocationAction.ADJUST:
                    return null;
                case LocationAction.CREATESTRUCTURE:

                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.CREATELINKEDLOCATION:


                    return new TranslateCircleLocationCommand(Parent,
                                                                new GridCircle(volumePosition, loc.Radius),
                                                                loc.Parent.Type.Color.ToXNAColor(1f),
                                                                new TranslateCircleLocationCommand.OnCommandSuccess((NewVolumePosition, NewMosaicPosition, NewRadius) =>
                                                                   {
                                                                       IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                                                                       GridVector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                                                                       SqlGeometry VolumeShape;
                                                                       SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);

                                                                       LocationObj newLoc = new LocationObj(loc.Parent,
                                                                            MosaicShape,
                                                                            VolumeShape,
                                                                            Parent.Section.Number,
                                                                            loc.TypeCode);

                                                                       section_mapper = Parent.Volume.GetSectionToVolumeTransform((int)Parent.Section.Number);
                                                                       NewMosaicPosition = section_mapper.VolumeToSection(NewVolumePosition);
                                                                       UpdateCircleLocationNoSaveCallback(newLoc, NewVolumePosition, NewMosaicPosition, NewRadius);

                                                                       Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
                                                                   }));
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
                                                                         GridVector2 volumePosition)
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
                                                             loc.Width.HasValue ? loc.Width.Value : 16.0,
                                                             (VolumeControlPoints, MosaicControlPoints, LineWidth) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints, LineWidth));
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc.MosaicShape.ToPoints(),
                                                                      loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                      loc.Width.HasValue ? loc.Width.Value : 16.0,
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
                    GridVector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                    SqlGeometry VolumeShape;
                    SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);


                    return new TranslateOpenCurveCommand(Parent,
                                                             volumePosition,
                                                             MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             loc.Width.HasValue ? loc.Width.Value : 16.0,
                                                             (NewVolumeControlPoints, NewMosaicControlPoints, NewWidth) =>
                                                                {
                                                                    LocationObj newLoc = new LocationObj(loc.Parent,
                                                                       MosaicShape,
                                                                       VolumeShape,
                                                                       Parent.Section.Number,
                                                                       loc.TypeCode);

                                                                    IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform((int)Parent.Section.Number);
                                                                    NewMosaicControlPoints = section_mapper.VolumeToSection(NewVolumeControlPoints);

                                                                    UpdateLineLocationNoSaveCallback(newLoc, NewVolumeControlPoints, NewMosaicControlPoints, NewWidth);
                                                                    Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
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
                                                                         GridVector2 volumePosition)
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
                                                             loc.Width.HasValue ? loc.Width.Value : 16.0,
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
                    GridVector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                    SqlGeometry VolumeShape;
                    SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);

                    return new TranslateClosedCurveCommand(Parent,
                                                             MosaicShape.Centroid(),
                                                             MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             loc.Width.HasValue ? loc.Width.Value : 16.0,
                                                             (NewVolumeControlPoints, NewMosaicControlPoints, NewWidth) =>
                                                             {
                                                                 LocationObj newLoc = new LocationObj(loc.Parent,
                                                                    MosaicShape,
                                                                    VolumeShape,
                                                                    Parent.Section.Number,
                                                                    loc.TypeCode);

                                                                 IVolumeToSectionTransform section_mapper = Parent.Volume.GetSectionToVolumeTransform((int)Parent.Section.Number);
                                                                 NewMosaicControlPoints = section_mapper.VolumeToSection(NewVolumeControlPoints);

                                                                 UpdateLineLocationNoSaveCallback(newLoc, NewVolumeControlPoints, NewMosaicControlPoints, NewWidth);

                                                                 Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
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
                                                                         GridVector2 volumePosition)
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
                                                               (MosaicPolygon) =>
                                                                    {
                                                                        loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                                        AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
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
                                                                               loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                                               Store.Locations.Save();
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
                                                              loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                              Store.Locations.Save();
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
                                                              loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                              Store.Locations.Save();
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
                                                                loc.Width.HasValue ? loc.Width.Value : Global.DefaultClosedLineWidth,
                                                                (sender, volume_points) =>
                                                                {
                                                                    GridVector2[] mosaic_points = Parent.Section.ActiveSectionToVolumeTransform.VolumeToSection(volume_points);
                                                                    SqlGeometry updatedMosaicShape = loc.MosaicShape.AddInteriorPolygon(mosaic_points);

                                                                    try
                                                                    {
                                                                        loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, updatedMosaicShape);
                                                                    }
                                                                    catch (ArgumentException e)
                                                                    {
                                                                        MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                    }

                                                                    AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
                                                                }
                                                                );

                        }
                        else
                        {
                            return new PlaceClosedCurveCommand(Parent, Microsoft.Xna.Framework.Color.White, volumePosition, Global.DefaultClosedLineWidth, (sender, volume_points) =>
                            {
                                GridVector2[] mosaic_points = Parent.Section.ActiveSectionToVolumeTransform.VolumeToSection(volume_points);
                                SqlGeometry updatedMosaicShape = loc.MosaicShape.AddInteriorPolygon(mosaic_points);

                                try
                                {
                                    loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, updatedMosaicShape);
                                }
                                catch (ArgumentException e)
                                {
                                    MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }

                                AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
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
                                                            loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                            Store.Locations.Save();
                                                        }
                                                        );
                case LocationAction.CREATELINKEDLOCATION:
                    {
                        //The section we are linking from is on another section, so we have to:
                        // 0. Position the mosaic shape where we want the command to begin
                        // 1. Warp the mosaic using the correct transform for the source section
                        // 2. Warp the volume shape back to our section using the current transform

                        IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                        GridVector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                        SqlGeometry VolumeShape;
                        SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);

                        return new TranslatePolygonCommand(Parent,
                                                                 MosaicShape.ToPolygon(),
                                                                 volumePosition,
                                                                 loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                 (MosaicPolygon) =>
                                                                 {
                                                                     LocationObj newLoc = new LocationObj(loc.Parent,
                                                                        Parent.Section.Number,
                                                                        loc.TypeCode);
                                                                     try
                                                                     {
                                                                         newLoc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, MosaicPolygon.ToSqlGeometry());
                                                                         Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
                                                                     }
                                                                     catch (ArgumentException e)
                                                                     {
                                                                         MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                     }
                                                                 }
                                                                 );
                    }
                case LocationAction.CHANGEBOUNDARY:
                    {
                        IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                        GridVector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                        SqlGeometry VolumeShape;
                        //SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);

                        var retracecmd = new RetraceAndReplacePathCommand(Parent,
                                                                 loc.MosaicShape.ToPolygon(),
                                                                 loc.Parent.Type.Color.ToXNAColor(0.5f),
                                                                 loc.Width.HasValue ? loc.Width.Value : Global.DefaultClosedLineWidth,
                                                                 (sender, MosaicPolygon) =>
                                                                 {
                                                                     //Drawing from inside to outside:
                                                                     var cmd = (RetraceAndReplacePathCommand)sender;

                                                                     try
                                                                     {
                                                                         loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, cmd.OutputMosaicPolygon.ToSqlGeometry());
                                                                     }
                                                                     catch (ArgumentException e)
                                                                     {
                                                                         MessageBox.Show(Parent, e.Message, "Could not save Polygon", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                     }


                                                                     AnnotationOverlay.SaveLocationsWithMessageBoxOnError();

                                                                 }
                                                                 );
                        retracecmd.InitPath(new GridVector2[] { volumePosition });
                        return retracecmd;
                    }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Warp a shape from the source section's mosaic space to the destination sections mosaic space
        /// </summary>
        /// <param name="SourceShape"></param>
        /// <param name="SourceSection"></param>
        /// <param name="DestinationSection"></param>
        private static Microsoft.SqlServer.Types.SqlGeometry TransformMosaicShapeToSection(Viking.ViewModels.VolumeViewModel volume,
                                                                                           Microsoft.SqlServer.Types.SqlGeometry SourceShape,
                                                                                           int SourceSection,
                                                                                           int DestinationSection,
                                                                                           out Microsoft.SqlServer.Types.SqlGeometry VolumeShape)
        {
            Viking.VolumeModel.IVolumeToSectionTransform SourceSectionTransform = volume.GetSectionToVolumeTransform(SourceSection);
            Viking.VolumeModel.IVolumeToSectionTransform DestinationSectionTransform = volume.GetSectionToVolumeTransform(DestinationSection);
            VolumeShape = SourceSectionTransform.TryMapShapeSectionToVolume(SourceShape);
            return DestinationSectionTransform.TryMapShapeVolumeToSection(VolumeShape);
        }

        private static bool IsClosedCurve(LocationObj loc)
        {
            return loc.TypeCode == LocationType.CLOSEDCURVE;
        }


        static void UpdateLineLocationCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        static void UpdateLineLocationNoSaveCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints)
        {
            SqlGeometry updatedMosaicShape = loc.TypeCode.GetShape(MosaicControlPoints);
            SqlGeometry updatedVolumeShape = loc.TypeCode.GetSmoothedShape(VolumeControlPoints);

            loc.VolumeShape = updatedVolumeShape;
            loc.MosaicShape = updatedMosaicShape;
        }

        static void UpdateLineLocationCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints, double NewWidth)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints, NewWidth);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        static void UpdateLineLocationNoSaveCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints, double NewWidth)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints);
            loc.Width = NewWidth;
        }

        public static void UpdateCircleLocationCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition, double NewRadius)
        {
            UpdateCircleLocationNoSaveCallback(loc, WorldPosition, MosaicPosition, NewRadius);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        public static void UpdateCircleLocationCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            UpdateCircleLocationNoSaveCallback(loc, WorldPosition, MosaicPosition);
            AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        public static void UpdateCircleLocationNoSaveCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            loc.MosaicShape = loc.MosaicShape.MoveTo(MosaicPosition);
            loc.VolumeShape = loc.VolumeShape.MoveTo(WorldPosition);


        }

        public static void UpdateCircleLocationNoSaveCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition, double NewRadius)
        {
            if (NewRadius < WebAnnotation.Global.MinRadius)
                NewRadius = WebAnnotation.Global.MinRadius;

            loc.MosaicShape = SqlGeometryUtils.Extensions.ToCircle(MosaicPosition.X,
                                           MosaicPosition.Y,
                                           loc.Z,
                                           NewRadius);

            loc.VolumeShape = SqlGeometryUtils.Extensions.ToCircle(WorldPosition.X,
                                   WorldPosition.Y,
                                   loc.Z,
                                   NewRadius);
        }
    }
}

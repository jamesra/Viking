using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Geometry;
using WebAnnotation.ViewModel;
using WebAnnotation.UI.Commands;
using WebAnnotationModel;
using SqlGeometryUtils;
using VikingXNAGraphics;
using Viking.VolumeModel;
using WebAnnotationModel;
using Microsoft.SqlServer.Types;

namespace WebAnnotation.View
{
    public enum LocationAction
    {
        NONE,
        TRANSLATE, //Move the annotation
        SCALE,     //Scale the size of the annotation
        ADJUST,    //Adjust a specific control point
        ADDCONTROLPOINT, //Add a control point
        REMOVECONTROLPOINT, //Remove a control point
        CREATELINK, //Create a link to an adjacent location or structure,
        CREATELINKEDLOCATION //Create a new location and link to it
    }

    /// <summary>
    /// Manage actions available to locations
    /// </summary>
    static class LocationActions
    {
        public static Cursor GetCursor(this LocationAction action)
        {
            switch(action)
            {
                case LocationAction.NONE:
                    return Cursors.Default;
                case LocationAction.TRANSLATE:
                    return Cursors.Hand;
                case LocationAction.SCALE:
                    return Cursors.SizeAll;
                case LocationAction.ADJUST:
                    return Cursors.SizeAll;
                case LocationAction.ADDCONTROLPOINT:
                    return Cursors.UpArrow;
                case LocationAction.REMOVECONTROLPOINT:
                    return Cursors.No;
                case LocationAction.CREATELINK:
                    return Cursors.Cross;
                case LocationAction.CREATELINKEDLOCATION:
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
            switch(loc.TypeCode)
            {
                case LocationType.CIRCLE:
                    return CreateCommandForCircles(action, Parent, loc, volumePosition);
                case LocationType.POLYLINE:
                    return CreateCommandForlineOrCurve(action, Parent, loc, volumePosition);
                case LocationType.OPENCURVE:
                    return CreateCommandForlineOrCurve(action, Parent, loc, volumePosition);
                case LocationType.CLOSEDCURVE:
                    return CreateCommandForShape(action, Parent, loc, volumePosition);
                case LocationType.POLYGON:
                    return CreateCommandForShape(action, Parent, loc, volumePosition);
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
            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.TRANSLATE:
                    return new TranslateCircleLocationCommand(Parent,
                                                              new GridCircle(loc.Position, loc.Radius),
                                                              loc.Parent.Type.Color.ToXNAColor(),
                                                              (NewVolumePosition, NewMosaicPosition, NewRadius) => UpdateCircleLocationCallback(loc, NewVolumePosition, NewMosaicPosition, NewRadius));
                case LocationAction.SCALE: 
                    return new ResizeCircleCommand(Parent,
                            System.Drawing.Color.FromArgb(loc.Parent.Type.Color),
                            loc.VolumePosition,
                            (radius) => { loc.Radius = radius; Store.Locations.Save(); });
                case LocationAction.ADJUST:
                    return null;
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.CREATELINKEDLOCATION:

                    IVolumeToSectionTransform mapper = Parent.Volume.GetSectionToVolumeTransform((int)loc.Z);
                    GridVector2 MosaicPosition = mapper.VolumeToSection(volumePosition);

                    SqlGeometry VolumeShape;
                    SqlGeometry MosaicShape = TransformMosaicShapeToSection(Parent.Volume, loc.MosaicShape.MoveTo(MosaicPosition), (int)loc.Z, Parent.Section.Number, out VolumeShape);
                     
                    LocationObj newLoc = new LocationObj(loc.Parent,
                                                        MosaicShape,
                                                        VolumeShape,
                                                        Parent.Section.Number,
                                                        loc.TypeCode);

                    newLoc.Radius = loc.Radius;

                    LocationCanvasView newLocView = AnnotationViewFactory.Create(newLoc, Parent.Section.ActiveSectionToVolumeTransform);
                    return new TranslateCircleLocationCommand(Parent,
                                                                new GridCircle(MosaicShape.Centroid(), loc.Radius),
                                                                newLoc.Parent.Type.Color.ToXNAColor(),
                                                                new TranslateCircleLocationCommand.OnCommandSuccess((NewVolumePosition, NewMosaicPosition, NewRadius) =>
                                                                   {
                                                                       UpdateCircleLocationNoSaveCallback(newLoc, NewVolumePosition, NewMosaicPosition, NewRadius);

                                                                       /*Viking.UI.Commands.Command.EnqueueCommand(typeof(ResizeCircleCommand), new object[] 
                                                                           { Parent, System.Drawing.Color.FromArgb(loc.Parent.Type.Color), VolumePosition,
                                                                               new ResizeCircleCommand.OnCommandSuccess((double radius) =>
                                                                               {
                                                                                   newLoc.Radius = radius;
                                                                                   */
                                                                       Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
                                                                       /*
                                                                   })
                                                               });*/
                                                                   }));
                    
                    Viking.UI.State.SelectedObject = null;
                    return null; 
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
        public static Viking.UI.Commands.Command CreateCommandForlineOrCurve(LocationAction action,
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
                                                             loc.MosaicShape.Centroid(),
                                                             loc.MosaicShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(),
                                                             loc.Radius * 2.0,
                                                             (VolumeControlPoints, MosaicControlPoints, LineWidth) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints, LineWidth));
                case LocationAction.SCALE:
                    return null; 
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc.MosaicShape.ToPoints(),
                                                                      loc.Parent.Type.Color.ToXNAColor(), 
                                                                      loc.Radius * 2.0,
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

                    LocationObj newLoc = new LocationObj(loc.Parent,
                                                        MosaicShape,
                                                        VolumeShape,
                                                        Parent.Section.Number,
                                                        loc.TypeCode);

                    newLoc.Radius = loc.Radius;

                    LocationCanvasView newLocView = AnnotationViewFactory.Create(newLoc, Parent.Section.ActiveSectionToVolumeTransform);
                    return new TranslateOpenCurveCommand(Parent,
                                                             newLoc.MosaicShape.Centroid(),
                                                             newLoc.MosaicShape.ToPoints(),
                                                             newLoc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             newLoc.Radius * 2.0, 
                                                             (NewVolumeControlPoints, NewMosaicControlPoints, NewWidth) =>
                                                                {
                                                                    UpdateLineLocationNoSaveCallback(newLoc, NewVolumeControlPoints, NewMosaicControlPoints, NewWidth);

                                                                    Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
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
        public static Viking.UI.Commands.Command CreateCommandForShape(LocationAction action,
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
                                                             loc.Parent.Type.Color.ToXNAColor(),
                                                             loc.Radius * 2.0, 
                                                             (VolumeControlPoints, MosaicControlPoints, LineWidth) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints, LineWidth));
                case LocationAction.SCALE:
                    return null;
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc.MosaicShape.ToPoints(),
                                                                      loc.Parent.Type.Color.ToXNAColor(),
                                                                      loc.Radius * 2.0,
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

                    LocationObj newLoc = new LocationObj(loc.Parent,
                                                        MosaicShape,
                                                        VolumeShape,
                                                        Parent.Section.Number,
                                                        loc.TypeCode);

                    newLoc.Radius = loc.Radius;

                    LocationCanvasView newLocView = AnnotationViewFactory.Create(newLoc, Parent.Section.ActiveSectionToVolumeTransform);
                    return new TranslateClosedCurveCommand(Parent,
                                                             newLoc.MosaicShape.Centroid(),
                                                             newLoc.MosaicShape.ToPoints(),
                                                             newLoc.Parent.Type.Color.ToXNAColor(0.5f),
                                                             newLoc.Radius * 2.0, 
                                                             (NewVolumeControlPoints, NewMosaicControlPoints, NewWidth) =>
                                                             {
                                                                 UpdateLineLocationNoSaveCallback(newLoc, NewVolumeControlPoints, NewMosaicControlPoints, NewWidth);

                                                                 Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
                                                             }
                                                             );
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
            Store.Locations.Save();
        }

        static void UpdateLineLocationNoSaveCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints)
        {
            loc.MosaicShape = SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), MosaicControlPoints);
            loc.VolumeShape = SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), VolumeControlPoints);
        }

        static void UpdateLineLocationCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints, double NewWidth)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints, NewWidth); 
            Store.Locations.Save();
        }

        static void UpdateLineLocationNoSaveCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints, double NewWidth)
        {
            UpdateLineLocationNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints);
            loc.Radius = NewWidth / 2.0;
        }

        public static void UpdateCircleLocationCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition, double NewRadius)
        {
            UpdateCircleLocationNoSaveCallback(loc, WorldPosition, MosaicPosition, NewRadius);
            Store.Locations.Save();
        }

        public static void UpdateCircleLocationCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            UpdateCircleLocationNoSaveCallback(loc, WorldPosition, MosaicPosition);
            Store.Locations.Save();
        }

        public static void UpdateCircleLocationNoSaveCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            loc.MosaicShape = loc.MosaicShape.MoveTo(MosaicPosition);
            loc.VolumeShape = loc.VolumeShape.MoveTo(WorldPosition);
        }

        public static void UpdateCircleLocationNoSaveCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition, double NewRadius)
        {
            loc.Radius = NewRadius;
            UpdateCircleLocationNoSaveCallback(loc, WorldPosition, MosaicPosition);
        }
    }
}

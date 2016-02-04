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
using WebAnnotationModel;

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

        public static Viking.UI.Commands.Command CreateCommand(this LocationAction action, 
                                                               Viking.UI.Controls.SectionViewerControl Parent, 
                                                               LocationObj loc)
        { 
            switch(loc.TypeCode)
            {
                case LocationType.CIRCLE:
                    return CreateCommandForCircles(action, Parent, loc);
                case LocationType.POLYLINE:
                    return CreateCommandForlineOrCurve(action, Parent, loc);
                case LocationType.OPENCURVE:
                    return CreateCommandForlineOrCurve(action, Parent, loc);
                case LocationType.CLOSEDCURVE:
                    return CreateCommandForlineOrCurve(action, Parent, loc);
                case LocationType.POLYGON:
                    throw new NotImplementedException("No commands available for polygons");
                case LocationType.POINT:
                    throw new NotImplementedException("No commands available for polygons");
                default:
                    throw new NotImplementedException("Unexpected location type");
            }
        }

        public static Viking.UI.Commands.Command CreateCommandForCircles(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc)
        {
            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.TRANSLATE:
                    return new TranslateCircleLocationCommand(Parent,
                                                              new GridCircle(loc.VolumePosition, loc.Radius),
                                                              loc.Parent.Type.Color.ToXNAColor(),
                                                              (VolumePosition, MosaicPosition) => UpdateCircleLocationCallback(loc, VolumePosition, MosaicPosition));
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
                    LocationObj newLoc = new LocationObj(loc.Parent, 
                                                        loc.MosaicShape,
                                                        loc.VolumeShape,
                                                        Parent.Section.Number,
                                                        loc.TypeCode);

                    newLoc.Radius = loc.Radius;

                    LocationCanvasView newLocView = AnnotationViewFactory.Create(newLoc);
                    Viking.UI.Commands.Command.EnqueueCommand(typeof(TranslateCircleLocationCommand), new object[] 
                                                                {
                                                                    Parent,
                                                                    new GridCircle(newLoc.VolumePosition, newLoc.Radius),
                                                                    newLoc.Parent.Type.Color.ToXNAColor(),
                                                                    new TranslateCircleLocationCommand.OnCommandSuccess( (VolumePosition, MosaicPosition) =>
                                                                        {
                                                                            UpdateCircleLocationNoSaveCallback(newLoc,VolumePosition, MosaicPosition);

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
                                                                        })
                                                                });
                    
                    

                    Viking.UI.State.SelectedObject = null;
                    return null; 
                default:
                    return null;
            }
        }

        public static Viking.UI.Commands.Command CreateCommandForlineOrCurve(LocationAction action,
                                                                         Viking.UI.Controls.SectionViewerControl Parent,
                                                                         LocationObj loc)
        {
            switch (action)
            {
                case LocationAction.NONE:
                    return null;
                case LocationAction.TRANSLATE:
                    return new TranslateCurveLocationCommand(Parent,
                                                             loc.VolumeShape.Centroid(),
                                                             loc.VolumeShape.ToPoints(),
                                                             loc.Parent.Type.Color.ToXNAColor(),
                                                             loc.Radius * 2.0,
                                                             IsClosedCurve(loc),
                                                             (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.SCALE:
                    return null; 
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc.VolumeShape.ToPoints(),
                                                                      loc.Parent.Type.Color.ToXNAColor(), 
                                                                      loc.Radius * 2.0,
                                                                      IsClosedCurve(loc),
                                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.ADDCONTROLPOINT:
                    return new AddLineControlPointCommand(Parent, 
                                                      loc.VolumeShape.ToPoints(),
                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.REMOVECONTROLPOINT:
                    return new RemoveLineControlPointCommand(Parent,
                                                      loc.VolumeShape.ToPoints(),
                                                      (VolumeControlPoints, MosaicControlPoints) => UpdateLineLocationCallback(loc, VolumeControlPoints, MosaicControlPoints));
                case LocationAction.CREATELINKEDLOCATION:
                    LocationObj newLoc = new LocationObj(loc.Parent,
                                                        loc.MosaicShape,
                                                        loc.VolumeShape,
                                                        Parent.Section.Number,
                                                        loc.TypeCode);

                    newLoc.Radius = loc.Radius;

                    LocationCanvasView newLocView = AnnotationViewFactory.Create(newLoc);
                    return new TranslateCurveLocationCommand(Parent,
                                                             newLoc.VolumeShape.Centroid(),
                                                             newLoc.VolumeShape.ToPoints(),
                                                             newLoc.Parent.Type.Color.ToXNAColor(),
                                                             newLoc.Radius * 2.0,
                                                             IsClosedCurve(newLoc),
                                                             (VolumeControlPoints, MosaicControlPoints) =>
                                                                {
                                                                    UpdateLineLocationNoSaveCallback(newLoc, VolumeControlPoints, MosaicControlPoints);

                                                                    Viking.UI.Commands.Command.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), new object[] { Parent, loc, newLoc });
                                                                }
                                                             );
                default:
                    return null;
            }
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
            loc.VolumeShape = SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.VolumeShape.STGeometryType(), VolumeControlPoints);
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
    }
}

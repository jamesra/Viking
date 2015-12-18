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

namespace WebAnnotation.View
{
    public enum LocationAction
    {
        NONE,
        TRANSLATE, //Move the annotation
        SCALE,     //Scale the size of the annotation
        ADJUST,    //Adjust a specific control point
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
                    return new TranslateCircleLocationCommand(Parent, loc, TranslateCircleLocationCommand.DefaultSuccessCallback);
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
                                                                    loc,
                                                                    new TranslateCircleLocationCommand.OnCommandSuccess( (l, VolumePosition, MosaicPosition) =>
                                                                        {
                                                                            newLoc.VolumeShape = newLoc.VolumeShape.MoveTo(VolumePosition);
                                                                            newLoc.MosaicShape = newLoc.MosaicShape.MoveTo(MosaicPosition);
                                                                            
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
                    return new TranslateCurveLocationCommand(Parent, loc, TranslateCurveLocationCommand.DefaultSuccessCallback);
                case LocationAction.SCALE:
                    return null; 
                case LocationAction.ADJUST:
                    return new AdjustCurveControlPointCommand(Parent, loc, AdjustCurveControlPointCommand.DefaultSuccessCallback);
                case LocationAction.CREATELINK:
                    return new LinkAnnotationsCommand(Parent, loc);
                case LocationAction.CREATELINKEDLOCATION:
                    return null; 
                default:
                    return null;
            }
        }
    }
}

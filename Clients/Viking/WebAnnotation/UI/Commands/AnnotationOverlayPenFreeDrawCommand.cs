using Geometry;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Viking.UI.Controls;
using Viking.VolumeModel;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This class is active when the user begins drawing a path with the pen in an area there are no annotations to take action on. 
    /// The command may exit with no action, draw an open curve, or draw a closed curved polygon.  Once the geometry is placed the 
    /// user can complete the annotation
    /// </summary>
    internal class AnnotationOverlayPenFreeDrawCommand : PlaceGeometryWithPenCommandBase
    {
        /// <summary>
        /// Prevent the user from making absurdly small annotations by accident
        /// </summary>
        private double MinAreaForClosedShape => Parent.Downsample * 10 * 10;

        private double MinLengthForOpenShape => Parent.Downsample * 10;


        public AnnotationOverlayPenFreeDrawCommand(SectionViewerControl parent, Color color, double LineWidth, OnCommandSuccess success_callback) : base(parent, color, LineWidth, success_callback)
        {
        }

        public AnnotationOverlayPenFreeDrawCommand(SectionViewerControl parent, Color color, GridVector2 origin, double LineWidth, OnCommandSuccess success_callback) : base(parent, color, origin, LineWidth, success_callback)
        {
        }

        public override uint NumCurveInterpolations => throw new NotImplementedException();

        protected override bool CanCommandComplete() => true;

        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            //TODO: Prompt the user to create a closed curve type
            if (HasLoop)
            {
                GridPolygon newVolumePoly = new(PenInput.SimplifiedFirstLoop);
                if (newVolumePoly.Area < MinAreaForClosedShape)
                {
                    Deactivated = true;
                    return;
                }

                //We created a loop, here are our steps:
                //1. See if we enclose significant portions of an existing circle annotation.  If we do, convert the circle to a polygon.
                //2. If we do not enclose a circle, check if we can continue an annotation
                //3. Create a new structure using the loop.

                if (TryConvertEnclosedCircle(newVolumePoly))
                {
                    Deactivated = true;
                    return;
                }
                else if (Global.CanContinueLastTrace(Parent.Section.Number))
                {
                    LocationObj lastObj = Store.Locations.GetObjectByID(Global.LastEditedAnnotationID.Value, false);
                    if (lastObj != null && lastObj.TypeCode.AllowsClosed2DShape())
                    {
                        LocationObj newLoc = new(lastObj.Parent,
                                                             Parent.Section.Number,
                                                             lastObj.TypeCode);
                        try
                        {
                            newLoc.SetShapeFromGeometryInVolume(Parent.Section.ActiveSectionToVolumeTransform, newVolumePoly.ToSqlGeometry());
                            Parent.CommandQueue.EnqueueCommand(typeof(CreateNewLinkedLocationCommand), [Parent, lastObj, newLoc]);
                        }
                        catch (ArgumentException e)
                        {
                            System.Windows.Forms.MessageBox.Show(Parent, e.Message, "Could not save Polygon", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        }

                        Deactivated = true;
                        return;
                    }
                    else
                    {
                        //TODO: New annotation?
                        CreateNewClosedAnnotation();
                        Execute();
                        return;
                    }
                }
                else
                {
                    CreateNewClosedAnnotation();
                    Execute();
                    return;
                }

                Execute();
            }
        }

        private bool TryConvertEnclosedCircle(GridPolygon newVolumePoly)
        {
            if (!PenInput.HasSelfIntersection)
            {
                throw new ArgumentException("Cannot possibly convert a circle if our path is not a loop.");
            }

            List<LocationCircleView> intersectedCircles = IntersectedCirclesOnSection(Parent.Section.Number, newVolumePoly);
            if (!intersectedCircles.Any())
            {
                return false;
            }

            LocationCircleView intersectedCircle = intersectedCircles.OrderByDescending(c => c.VolumeCircle.Area).First();

            LocationObj obj = Store.Locations.GetObjectByID(intersectedCircle.ID, false);

            SqlGeometry original_mosaic_shape = obj.MosaicShape;
            SqlGeometry original_volume_shape = obj.MosaicShape;
            Viking.AnnotationServiceTypes.Interfaces.LocationType original_typecode = obj.TypeCode;

            try
            {
                obj.TypeCode = Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON;
                obj.SetShapeFromGeometryInVolume(Parent.Section.ActiveSectionToVolumeTransform, newVolumePoly.ToSqlGeometry());

                Store.Locations.Save();
            }
            catch (System.ServiceModel.FaultException e)
            {
                AnnotationOverlay.ShowFaultExceptionMsgBox(e);
                obj.TypeCode = original_typecode;
                obj.MosaicShape = original_mosaic_shape;
                obj.VolumeShape = original_volume_shape;
            }
            return true;
        }

        private void CreateNewClosedAnnotation()
        {
            GridPolygon newVolumePoly = new(PenInput.SimplifiedFirstLoop);

            StructureTypeObj type = Store.StructureTypes.GetObjectByID(1);//new StructureType(typeObj);
            bool StructureNeedsParent = type.ParentID.HasValue;

            StructureObj newStruct = new(type);
            LocationObj newLocation = new(newStruct,
                                            Parent.Section.Number,
                                            Viking.AnnotationServiceTypes.Interfaces.LocationType.CURVEPOLYGON);

            newLocation.SetShapeFromGeometryInVolume(Parent.Section.ActiveSectionToVolumeTransform, newVolumePoly.ToSqlGeometry());

            Parent.CommandQueue.EnqueueCommand(typeof(ShapeConfirmationCommand), [ Parent, newVolumePoly, LineWidth,
                new ShapeConfirmationCommand.OnCommandSuccess(() =>  {
                            if (StructureNeedsParent)
                            {
                                //Enqueue extra command to select a parent
                                Parent.CommandQueue.EnqueueCommand(typeof(LinkStructureToParentCommand), [Parent, newStruct, newLocation]);
                            }

                            Parent.CommandQueue.EnqueueCommand(typeof(CreateNewStructureCommand), [Parent, newStruct, newLocation]);
                        })
                ]
             );
        }

        protected override void OnPenPathComplete(object sender, GridVector2[] Path) =>
            //TODO: Prompt the user to create an open curve type if there is no curve
            //If we draw from one annotation to another we either create a location link (different sections) or a structure link (same sections).
            //If not we create a new open curve annotation.
            Execute();

        protected override void OnPenPathChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.OnPenPathChanged(sender, e);

            //This path currently only executes when the user is inside an annotation, but leaves the annotation to fire a retrace and replace command.
            //In the future we should probably fire OnLeavingAnnotation events to simplify detecting this case
            if (PenInput.Points.Count <= 1)
            {
                return;
            }

            GridLineSegment move_line = PenInput.NewestSegent;
            ICanvasView IntersectedObject = AnnotationOverlay.FirstIntersectedObjectOnSection(Parent.Section.Number, move_line);
            //            ICanvasGeometryView MouseOverAnnotation = ObjectAtPosition(WorldPosition, out distance) as ICanvasGeometryView;
            System.Diagnostics.Trace.WriteLine($"{(IntersectedObject is null ? "NULL" : IntersectedObject.ToString())}");

            //If the objects changed that means we intersected the boundary of the object.  If we are in pen mode and the intersected object qualifies we should start a retrace and replace command... 
            if (IntersectedObject != null)
            {
                if (IntersectedObject is LocationPolygonView intersectedPolyView)
                {

                    //intersectedPolyView.
                    LocationObj Loc = Store.Locations.GetObjectByID(intersectedPolyView.ID, true);
#if DEBUG
                    bool Intersection_found = move_line.Intersects(intersectedPolyView.VolumeShapeAsRendered.ToPolygon(), out GridVector2 intersection_point);
                    System.Diagnostics.Debug.Assert(Intersection_found, "Expected to find an intersection with the object boundary.");

                    Loc.VolumeShape.ToPolygon().AddVertex(intersection_point);
#endif
                    RetraceAndReplacePathCommand retraceCmd = new(Parent, Loc.MosaicShape.ToPolygon(), intersectedPolyView.Color, Loc.Width ?? Global.DefaultClosedLineWidth, (senderCmd, MosaicPolygon) =>
                    {
                        //Drawing from outside to inside:

                        RetraceAndReplacePathCommand cmd = (RetraceAndReplacePathCommand)senderCmd;

                        try
                        {
                            Loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, cmd.OutputMosaicPolygon.ToSqlGeometry());
                        }
                        catch (ArgumentException r)
                        {
                            System.Windows.Forms.MessageBox.Show(Parent, r.Message, "Could not save Polygon", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        }

                        AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
                    }
                    );

                    retraceCmd.InitPath(PenInput.Points);

                    Deactivated = true;

                    Parent.CurrentCommand = retraceCmd;
                }
            }
        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {
            //TODO: Check if we need to start a retrace and replace command

            return;
        }

        protected override bool ShapeIsValid() => true;

        /// <summary>
        /// Find the annotations intersecting the provided line on viewed section only, using annotation locations on the screen, not anatomical positions
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static List<LocationCircleView> IntersectedCirclesOnSection(int CurrentSectionNumber, GridPolygon bounds)
        {
            SectionAnnotationsView locView = AnnotationOverlay.GetAnnotationsForSection(CurrentSectionNumber);
            if (locView is null)
            {
                return null;
            }

            IEnumerable<LocationCanvasView> listObjects = locView.GetLocations(bounds.BoundingBox).Where(o => o.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE);

            IEnumerable<LocationCircleView> listCircles = listObjects.Select(o => o as LocationCircleView).Where(o => o != null);

            return [.. listCircles.Where(o => o.VolumeCircle.Intersects(bounds) || bounds.Contains(o.VolumeCircle))];
        }


    }
}

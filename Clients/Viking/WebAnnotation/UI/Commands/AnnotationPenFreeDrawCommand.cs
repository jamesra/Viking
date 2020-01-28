using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework;
using Viking.UI.Controls;
using WebAnnotation;
using WebAnnotation.View;
using WebAnnotation.ViewModel;
using WebAnnotationModel;
using Viking.VolumeModel;
using SqlGeometryUtils;
using System.Collections.Specialized;
using System.Diagnostics;
using WebAnnotation.UI.Commands;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// This class is active when the user begins drawing a path with the pen in an area there are no annotations to take action on. 
    /// The command may exit with no action, draw an open curve, or draw a closed curved polygon.  Once the geometry is placed the 
    /// user can complete the annotation
    /// </summary>
    class AnnotationPenFreeDrawCommand : PlaceGeometryWithPenCommandBase
    {
        /// <summary>
        /// Prevent the user from making absurdly small annotations by accident
        /// </summary>
        private double MinAreaForClosedShape
        {
            get
            {
                return Parent.Downsample * 10 * 10;
            }
        }

        private double MinLengthForOpenShape
        {
            get
            {
                return Parent.Downsample * 10;
            }
        }

        LocationCanvasView Annotation;

        public AnnotationPenFreeDrawCommand(SectionViewerControl parent, LocationCanvasView annotation, Color color, double LineWidth, OnCommandSuccess success_callback) : base(parent, color, LineWidth, success_callback)
        {
            Annotation = annotation;
        }

        public AnnotationPenFreeDrawCommand(SectionViewerControl parent, LocationCanvasView annotation, Color color, GridVector2 origin, double LineWidth, OnCommandSuccess success_callback) : base(parent, color, origin, LineWidth, success_callback)
        {
            Annotation = annotation;
        }

        public override uint NumCurveInterpolations => throw new NotImplementedException();

        protected override bool CanCommandComplete()
        {
            return true;
        }

        protected override void OnPathLoop(object sender, bool HasLoop)
        {
            //TODO: Prompt the user to create a closed curve type
            if (HasLoop)
            {
                GridPolygon newVolumePoly = new GridPolygon(this.PenInput.SimplifiedFirstLoop);
                if (newVolumePoly.Area < this.MinAreaForClosedShape)
                {
                    this.Deactivated = true;
                    return;
                }

                //We created a loop, here are our steps:
                //1. If our loop is entirely contained within a polygon, cut a hole.
                
                if(TryCutHole(newVolumePoly))
                {
                    this.Deactivated = true;
                    return;
                }

                //OK, we drew a loop... yay?  I can't think of a command that is applicable, so exit.
                this.Execute();
            }
        }


        private bool TryCutHole(GridPolygon newVolumePoly)
        {
            if (!this.PenInput.HasSelfIntersection)
            {
                throw new ArgumentException("Cannot possibly cut a hole if our path is not a loop.");
            }
            /*
            List<LocationPolygonView> intersectedPolys = IntersectedPolygonsOnSection(Parent.Section.Number, newVolumePoly).Where(ip => ip.VolumeShapeAsRendered.ToPolygon().Contains(newVolumePoly)).ToList();
            if (!intersectedPolys.Any())
            {
                return false;
            }

            LocationPolygonView intersectedPoly = intersectedPolys.OrderByDescending(c => c.VolumeShapeAsRendered.STArea()).First();
            */

            if(!Annotation.TypeCode.AllowsInteriorHoles())
            {
                return false;
            }

            LocationObj obj = Store.Locations.GetObjectByID(Annotation.ID, false);

            GridPolygon mosaic_shape = obj.MosaicShape.ToPolygon();
            GridPolygon new_mosiac_hole = Parent.Section.ActiveSectionToVolumeTransform.TryMapShapeVolumeToSection(newVolumePoly);
            mosaic_shape.AddInteriorRing(new_mosiac_hole);

            GridPolygon volume_shape = obj.VolumeShape.ToPolygon();
            GridPolygon new_volume_hole = newVolumePoly.Smooth(this.NumCurveInterpolations);
            volume_shape.AddInteriorRing(new_volume_hole);

            obj.MosaicShape = mosaic_shape.ToSqlGeometry();
            obj.VolumeShape = volume_shape.ToSqlGeometry();

            Store.Locations.Save();
            return true;
        }
        
        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {
            //If we draw from one annotation to another we either create a location link (different sections) or a structure link (same sections).
            //If not we create a new open curve annotation.

            GridVector2 Start = Path.Last();
            GridVector2 Finish = Path.First();

            List<HitTestResult> listFinishHitTestResults = AnnotationOverlay.GetAnnotationsAtPosition(Parent.Section.Number, Finish);

            LocationObj loc = Store.Locations.GetObjectByID(Annotation.ID, false);
            IViewLocation locationLinkCandidate = LinkAnnotationsCommand.FindBestLinkCandidate(AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number), Finish, loc);
            if (locationLinkCandidate != null)
            {
                LinkAnnotationsCommand.TryCreateLink(AnnotationOverlay.GetAnnotationsForSection(Parent.Section.Number), Finish, loc);
                this.Execute();
                return;
            }
            else
            {
                this.Deactivated = true;
            }
        }

        protected override void OnPenPathChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.OnPenPathChanged(sender, e);

            /*
            //This path is used to detect when the user starts inside an annotation, but leaves and re-enters the annotation to fire a retrace and replace command.
            //In the future we should probably fire OnLeavingAnnotation events to simplify detecting this case
            if (this.PenInput.Points.Count <= 1)
                return;

            GridLineSegment move_line = this.PenInput.NewestSegent;
            ICanvasGeometryView IntersectedObject = AnnotationOverlay.FirstIntersectedObjectOnSection(Parent.Section.Number, move_line, out double distance);
            //            ICanvasGeometryView MouseOverAnnotation = ObjectAtPosition(WorldPosition, out distance) as ICanvasGeometryView;
            System.Diagnostics.Trace.WriteLine(string.Format("{0}", IntersectedObject == null ? "NULL" : IntersectedObject.ToString()));

            //If the objects changed that means we intersected the boundary of the object.  If we are in pen mode and the intersected object qualifies we should start a retrace and replace command... 
            if (IntersectedObject != null)
            {
                LocationPolygonView intersectedPolyView = IntersectedObject as LocationPolygonView; //TODO: Needs to check for lines as well
                if (intersectedPolyView != null)
                {

                    //intersectedPolyView.
                    LocationObj Loc = Store.Locations.GetObjectByID(intersectedPolyView.ID, true);
                    GridVector2 intersection_point;
#if DEBUG
                    bool Intersection_found = move_line.Intersects(intersectedPolyView.VolumeShapeAsRendered.ToPolygon(), out intersection_point);
                    System.Diagnostics.Debug.Assert(Intersection_found, "Expected to find an intersection with the object boundary.");

                    Loc.VolumeShape.ToPolygon().AddVertex(intersection_point);
#endif
                    RetraceAndReplacePathCommand retraceCmd = new RetraceAndReplacePathCommand(Parent, Loc.MosaicShape.ToPolygon(), intersectedPolyView.Color, Loc.Width.HasValue ? Loc.Width.Value : Global.DefaultClosedLineWidth, (senderCmd, MosaicPolygon) =>
                    {
                        //Drawing from outside to inside:

                        var cmd = (RetraceAndReplacePathCommand)senderCmd;

                        try
                        {
                            Loc.SetShapeFromGeometryInSection(Parent.Section.ActiveSectionToVolumeTransform, cmd.OutputMosaicPolygon.ToSqlGeometry());
                        }
                        catch (ArgumentException r)
                        {
                            System.Windows.Forms.MessageBox.Show(Parent, r.Message, "Could not save Polygon", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                        }

                        Store.Locations.Save();
                    }
                    );

                    retraceCmd.InitPath(this.PenInput.Points);

                    this.Deactivated = true;

                    Parent.CurrentCommand = retraceCmd;
                }
            }*/
        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {
            //TODO: Check if we need to start a retrace and replace command

            return;
        }

        protected override bool ShapeIsValid()
        {
            return true;
        }


        /// <summary>
        /// Find the annotations intersecting the provided line on viewed section only, using annotation locations on the screen, not anatomical positions
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static List<LocationPolygonView> IntersectedPolygonsOnSection(int CurrentSectionNumber, GridPolygon bounds)
        {
            SectionAnnotationsView locView = AnnotationOverlay.GetAnnotationsForSection(CurrentSectionNumber);
            if (locView == null)
                return null;

            ICanvasGeometryView bestObj = null;

            var listObjects = locView.GetLocations(bounds.BoundingBox).Where(o => o.TypeCode.AllowsInteriorHoles());

            var listPolygons = listObjects.Select(o => o as LocationPolygonView).Where(o => o != null);

            return listPolygons.Where(o => {
                GridPolygon poly = o.VolumeShapeAsRendered.ToPolygon();
                return poly.Intersects(bounds) || poly.Contains(bounds);
                }).ToList();
        }
    }
}

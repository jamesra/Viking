using System.Collections.Generic;
using System.Linq;
using Geometry;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.VolumeModel;
using WebAnnotation.UI.Actions;
using WebAnnotation.View;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI
{
    /// <summary>
    /// The choices a finished pen stroke offers, shared by Viking's overlay and Jotunn's pen tool: what each annotation
    /// the stroke touched proposes, converting or cutting holes in the shapes a closed loop covers, and creating a new
    /// structure of each offered type from the stroke.
    /// </summary>
    internal static class PenStrokeActions
    {
        /// <summary>The actions the annotations in <paramref name="log"/> propose for <paramref name="path"/>.</summary>
        public static List<IAction> FromTouchedAnnotations(Path path, IReadOnlyList<InteractionLogEvent> log, int sectionNumber)
        {
            List<IAction> actions = [];
            foreach (ICanvasView annotation in log.Select(e => e.Annotation).Distinct())
            {
                if (annotation is IPenActionSupport penView)
                    actions.AddRange(penView.GetPenActionsForShapeAnnotation(path, log, sectionNumber) ?? []);
            }

            return actions;
        }

        /// <summary>
        /// Adds a convert-to-polygon choice for each circle on the section that the stroke's closed loop overlaps (the
        /// stroke log only includes circles the path entered, so a loop drawn around a circle would otherwise offer
        /// nothing) and a cut-hole choice for each polygon the loop lies over. Does nothing for an open stroke.
        /// </summary>
        public static void AddLoopActions(Path path, int sectionNumber, List<IAction> actions)
        {
            if (!path.HasSelfIntersection)
                return;

            Polygon loop = new(path.SimplifiedFirstLoop);
            foreach (LocationCircleView circle in AnnotationOverlay.IntersectedCirclesOnSection(sectionNumber, loop) ?? [])
            {
                if (actions.OfType<ChangeToPolygonAction>().Any(existing => existing.Location.ID == circle.ID))
                    continue;

                if (!Store.Locations.TryGetObjectByID(circle.ID, out LocationObj location) || location is null || location.TypeCode != LocationType.CIRCLE)
                    continue;

                actions.Add(new ChangeToPolygonAction(location, loop));
            }

            foreach (LocationPolygonView polygon in AnnotationOverlay.IntersectedPolygonsOnSection(sectionNumber, loop) ?? [])
            {
                foreach (CutHoleAction cutHole in polygon.GetCutHoleActionsForLoop(path))
                {
                    if (!actions.OfType<CutHoleAction>().Any(existing => existing.Equals(cutHole)))
                        actions.Add(cutHole);
                }
            }
        }

        /// <summary>
        /// Adds a create-structure choice per type: a closed polygon from the stroke's first loop, else a curve along it.
        /// </summary>
        public static void AddCreateStructureActions(Path path, int sectionNumber, IEnumerable<long> structureTypeIds,
            List<IAction> actions, IVolumeToSectionTransform? transform = null)
        {
            foreach (long typeId in structureTypeIds.Distinct())
            {
                actions.Add(path.HasSelfIntersection
                    ? new Create2DStructureAction(typeId, new Polygon(path.SimplifiedFirstLoop.EnsureClosedRing()), sectionNumber, transform)
                    : new Create1DStructureAction(typeId, path.SimplifiedPath.ToPolyline(), sectionNumber, transform));
            }
        }
    }
}

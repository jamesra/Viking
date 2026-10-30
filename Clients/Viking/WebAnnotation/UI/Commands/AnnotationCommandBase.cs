using Geometry;
using System;
using System.Diagnostics;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    internal abstract class AnnotationCommandBase(Viking.UI.Controls.SectionViewerControl parent) : Viking.UI.Commands.Command(parent)
    {
        protected AnnotationOverlay Overlay = AnnotationOverlay.CurrentOverlay;

        protected override void OnDeactivate()
        {
            //A bit of a hack.  We null the selected object so the viewer control doesn't decide to start the default
            //command for the selected object when it creates the next command.  It should launch the default command instead.
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        /// <summary>
        /// Maps <paramref name="volumePolygon"/>, the polygon the user edited on screen, back to section space with the inverse
        /// owner (<see cref="LocationVolumeGeometry.TryMapPolygonToMosaic"/>) and passes the result to
        /// <paramref name="applyMosaicPolygon"/>. When any exterior or hole vertex falls outside <paramref name="mapping"/>,
        /// <paramref name="applyMosaicPolygon"/> is not called, a trace line naming <paramref name="commandName"/> is written and
        /// <paramref name="onRefused"/> gets the reason to show, so the location keeps its shape.
        /// </summary>
        /// <returns>True when <paramref name="applyMosaicPolygon"/> ran.</returns>
        internal static bool TryApplyInSection(IVolumeToSectionTransform mapping, Polygon volumePolygon, Action<Polygon> applyMosaicPolygon, string commandName, Action<string>? onRefused = null)
        {
            string? failure;
            Polygon? mosaicPolygon;
            try
            {
                LocationVolumeGeometry.TryMapPolygonToMosaic(volumePolygon, mapping, out mosaicPolygon, out failure);
            }
            catch (ArgumentException e)
            {
                mosaicPolygon = null;
                failure = e.Message;
            }

            if (mosaicPolygon is null)
            {
                Trace.WriteLine(commandName + ": edited polygon not applied: " + failure, "Command");
                onRefused?.Invoke(failure ?? "The edited polygon could not be mapped into the section.");
                return false;
            }

            applyMosaicPolygon(mosaicPolygon);
            return true;
        }

        /// <summary>Shows the user why an edit was not saved (<see cref="LocationActions.ShowUnmappedEdit"/>).</summary>
        protected void ShowRefusedEdit(string failure) => LocationActions.ShowUnmappedEdit(Parent, failure, "Could not save annotation");
    }
}

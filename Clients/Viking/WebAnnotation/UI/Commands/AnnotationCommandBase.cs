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
        /// Maps <paramref name="volumePolygon"/> to section space and passes the result to <paramref name="applyMosaicPolygon"/>.
        /// When any exterior or hole vertex falls outside <paramref name="mapping"/>, <paramref name="applyMosaicPolygon"/> is not called
        /// and a trace line naming <paramref name="commandName"/> is written, so the location keeps its shape.
        /// </summary>
        /// <returns>True when <paramref name="applyMosaicPolygon"/> ran.</returns>
        internal static bool TryApplyInSection(IVolumeToSectionTransform mapping, Polygon volumePolygon, Action<Polygon> applyMosaicPolygon, string commandName)
        {
            Polygon mosaicPolygon = mapping.TryMapShapeVolumeToSection(volumePolygon);
            if (mosaicPolygon is null)
            {
                Trace.WriteLine(commandName + ": edited polygon lies partly outside the section mapping; not applied.", "Command");
                return false;
            }

            applyMosaicPolygon(mosaicPolygon);
            return true;
        }
    }
}

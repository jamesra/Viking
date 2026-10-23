using Geometry;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Viking.SectionCorrection;
using Viking.SectionCorrectionClient;
using VikingXNA;
using VikingXNAGraphics;
using GVector2 = Geometry.Vector2;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace MonogameTestbed
{
    /// <summary>
    /// Same even 1000 nm display grid as SectionCorrectionBuilder quiver previews.
    /// Samples a disk <see cref="PublishedCorrectionSet"/> or a URL-downloaded <see cref="DownloadedCorrectionSet"/>.
    /// </summary>
    static class ResidualFieldQuiverOverlay
    {
        const double DisplayPitchNm = 1000.0;

        public static void Draw(MonoTestbed window, Scene scene, BajajRepro testCase)
        {
            if (window is null || scene is null || Program.options?.ShowCorrectionField != true)
                return;

            MorphologyRegistration.TryLoadPublishedOrServiceSetAsync(Program.options).GetAwaiter().GetResult();

            if (!TrySectionZ(testCase, out int z))
                return;

            List<LineView> lines = [];
            if (MorphologyRegistration.LoadedServiceSet is DownloadedCorrectionSet service &&
                service.Sections.TryGetValue(z, out SectionLatticeField lattice))
            {
                foreach ((double ox, double oy, double dx, double dy) in lattice.EnumerateTrustedDisplaySamples(DisplayPitchNm))
                    lines.Add(Arrow(new GVector2(ox, oy), new GVector2(dx, dy)));
            }
            else if (MorphologyRegistration.LoadedPublishedSet is PublishedCorrectionSet disk &&
                     disk.TryGetSection(z, out SectionVectorField field))
            {
                foreach ((GVector2 origin, GVector2 offset) in field.EnumerateTrustedDisplaySamples(DisplayPitchNm))
                    lines.Add(Arrow(origin, offset));
            }

            if (lines.Count > 0)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. lines]);
        }

        static LineView Arrow(GVector2 origin, GVector2 offset)
        {
            double mag = offset.Magnitude;
            double cellPx = DisplayPitchNm;
            double arrowLen = Math.Min(cellPx * 0.9, Math.Max(40, mag / 500.0 * cellPx * 0.75));
            GVector2 dir = offset * (arrowLen / mag);
            return new LineView(origin, origin + dir, Math.Max(8, DisplayPitchNm * 0.04), ColorForMagnitude(mag), LineStyle.Standard);
        }

        static bool TrySectionZ(BajajRepro testCase, out int z)
        {
            z = 0;
            if (testCase?.Morphology is null || testCase.SliceLocations is null)
                return false;
            foreach (ulong id in testCase.SliceLocations)
            {
                if (testCase.Morphology.Nodes.TryGetValue(id, out AnnotationVizLib.MorphologyNode node))
                {
                    z = (int)Math.Round(node.UnscaledZ);
                    return true;
                }
            }

            return false;
        }

        static XnaColor ColorForMagnitude(double magNm)
        {
            double t = Math.Clamp(magNm / 1000.0, 0, 1);
            byte r = (byte)(40 + 200 * t);
            byte g = (byte)(180 * (1 - t));
            byte b = (byte)(220 * (1 - t));
            return new XnaColor(r, g, b) * 0.85f;
        }
    }
}

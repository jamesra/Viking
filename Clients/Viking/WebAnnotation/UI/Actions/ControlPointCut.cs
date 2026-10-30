using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Actions
{
    /// <summary>
    /// A pen cut of one ring of a polygon, saved as control points. The location's own control points on the kept side are
    /// reused exactly as they are stored in section space, so they never round-trip through the transform, and only the pen
    /// stretch adds new points (<see cref="ControlRingCurve"/>). Re-fitting the whole outline instead moved every part of it
    /// a little at each edit, and the drift grew with every edit.
    /// </summary>
    internal static class ControlPointCut
    {
        /// <summary>One side of the cut, in volume and section space.</summary>
        internal sealed class Piece(Polygon volume, Polygon mosaic, double referenceArea)
        {
            public Polygon Volume { get; } = volume;

            public Polygon Mosaic { get; } = mosaic;

            /// <summary>Area of the drawn outline of this side (old curve and pen stroke), for pairing with a walked cut.</summary>
            public double ReferenceArea { get; } = referenceArea;
        }

        /// <summary>
        /// Splices the stretch of <paramref name="penPath"/> between its first two crossings of the drawn ring into the stored ring.
        /// </summary>
        /// <param name="mosaicControl">The location's polygon as stored (section space control points).</param>
        /// <param name="ring">The ring being cut: the exterior or one hole.</param>
        /// <param name="penPath">The pen stroke in volume space.</param>
        /// <param name="tolerance">Largest distance, in volume pixels, between the new curve and the stroke or the kept curve.</param>
        /// <param name="keepsArcAfterFirstCrossing">The side <see cref="Polygon.WalkPolygonCut(Polygon, RotationDirection, IList{Vector2})"/> calls counterclockwise.</param>
        /// <param name="keepsArcAfterSecondCrossing">The other side.</param>
        /// <returns>False when the ring cannot be mapped or the stroke does not cross the drawn ring twice.</returns>
        public static bool TrySplice(Polygon mosaicControl, IVolumeToSectionTransform transform, PolygonIndex ring, IReadOnlyList<Vector2> penPath, double tolerance, out Piece keepsArcAfterFirstCrossing, out Piece keepsArcAfterSecondCrossing)
        {
            keepsArcAfterFirstCrossing = null;
            keepsArcAfterSecondCrossing = null;
            if (mosaicControl is null || penPath is null)
                return false;

            Vector2[] mosaicRing = ring.IsInner ? mosaicControl.InteriorRings[ring.InnerShapeIndex.Value] : mosaicControl.ExteriorRing;
            if (LocationVolumeGeometry.TryMapPoints(mosaicRing, transform, out Vector2[] volumeRing).Any(mapped => !mapped))
                return false;

            ControlRingCurve curve;
            try
            {
                curve = new ControlRingCurve(volumeRing, Global.AnnotationSettings.NumClosedCurveInterpolationPointsForDisplay);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (!curve.TrySplice(penPath, tolerance, out SplicedControlRing afterFirst, out SplicedControlRing afterSecond))
                return false;

            keepsArcAfterFirstCrossing = BuildPiece(afterFirst, mosaicRing, mosaicControl, transform, ring.IsInner);
            keepsArcAfterSecondCrossing = BuildPiece(afterSecond, mosaicRing, mosaicControl, transform, ring.IsInner);
            return keepsArcAfterFirstCrossing is not null || keepsArcAfterSecondCrossing is not null;
        }

        /// <summary>
        /// Matches the spliced pieces to the two sides of a cut walked on the drawn outline, by which side is smaller, so a choice
        /// preview built from the walk and the shape it saves describe the same side. Without both walked sides the pieces keep
        /// the walk's naming: the counterclockwise walk follows the ring forward from the first crossing.
        /// </summary>
        public static void PairWithWalk(Piece keepsArcAfterFirstCrossing, Piece keepsArcAfterSecondCrossing, Polygon walkClockwise, Polygon walkCounterClockwise, out Piece clockwise, out Piece counterClockwise)
        {
            counterClockwise = keepsArcAfterFirstCrossing;
            clockwise = keepsArcAfterSecondCrossing;
            if (keepsArcAfterFirstCrossing is null || keepsArcAfterSecondCrossing is null || walkClockwise is null || walkCounterClockwise is null)
                return;

            bool walkClockwiseIsSmaller = walkClockwise.Area <= walkCounterClockwise.Area;
            bool secondIsSmaller = keepsArcAfterSecondCrossing.ReferenceArea <= keepsArcAfterFirstCrossing.ReferenceArea;
            if (walkClockwiseIsSmaller != secondIsSmaller)
            {
                counterClockwise = keepsArcAfterSecondCrossing;
                clockwise = keepsArcAfterFirstCrossing;
            }
        }

        /// <summary>
        /// Builds a side in both spaces: kept control points come from the stored ring, new ones are mapped to the section. An
        /// exterior piece keeps the stored holes it still covers; a hole the cut leaves outside goes with the other piece.
        /// </summary>
        /// <returns>Null when a new point cannot be mapped or the ring is not a valid polygon.</returns>
        private static Piece BuildPiece(SplicedControlRing side, Vector2[] mosaicRing, Polygon mosaicControl, IVolumeToSectionTransform transform, bool isHole)
        {
            if (side is null)
                return null;

            Vector2[] newPoints = [.. side.ControlRing.Where((p, i) => side.OriginalIndex[i] < 0)];
            Vector2[] mappedNew = [];
            if (newPoints.Length > 0 && !LocationVolumeGeometry.TryMapPointsToMosaic(newPoints, transform, out mappedNew, out _))
                return null;

            Vector2[] mosaicPoints = new Vector2[side.ControlRing.Length];
            int next = 0;
            for (int i = 0; i < mosaicPoints.Length; i++)
                mosaicPoints[i] = side.OriginalIndex[i] >= 0 ? mosaicRing[side.OriginalIndex[i]] : mappedNew[next++];

            mosaicPoints[mosaicPoints.Length - 1] = mosaicPoints[0];

            try
            {
                Polygon volume = new(side.ControlRing);
                Polygon mosaic = new(mosaicPoints);
                if (!isHole)
                {
                    foreach (Vector2[] mosaicHole in mosaicControl.InteriorRings)
                    {
                        if (!mosaic.Covers(mosaicHole[0]))
                            continue;

                        if (LocationVolumeGeometry.TryMapPoints(mosaicHole, transform, out Vector2[] volumeHole).Any(mapped => !mapped))
                            return null;

                        mosaic.AddInteriorRing(mosaicHole);
                        volume.AddInteriorRing(volumeHole);
                    }
                }

                return new Piece(volume, mosaic, Math.Abs(SignedArea(side.ReferenceRing)));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static double SignedArea(Vector2[] closedRing)
        {
            double twice = 0;
            for (int i = 0; i < closedRing.Length - 1; i++)
                twice += (closedRing[i].X * closedRing[i + 1].Y) - (closedRing[i + 1].X * closedRing[i].Y);

            return twice / 2.0;
        }
    }
}

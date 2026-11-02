using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Viking.VolumeModel;
using WebAnnotation.UI.AutoPolygonize;
using WebAnnotationModel;

namespace WebAnnotation.UI.CellOverlap
{
    /// <summary>
    /// The regions where outlines of different cells overlap among the polygon locations it is given (one section's
    /// visible locations). Cell outlines should never overlap (<c>cell-boundaries-never-overlap.mdc</c>), so a region
    /// here is old data, an import, or a path that skipped the clip. Same-cell locations and a cell with its child or
    /// parent structures are not reported (<see cref="AutoPolygonizeSelection.IsRelatedStructure"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="Update"/> is called every frame, so it first computes a signature from the identity of each location's
    /// <c>MosaicShape</c> (a shape edit stores a new instance) and only then does any geometry. A pair's intersection is
    /// remembered while neither outline changes, so panning does not repeat it. UI thread only.
    /// </remarks>
    internal sealed class CellOverlapIndex
    {
        private readonly record struct PairKey(long A, long B, int StampA, int StampB);

        private Dictionary<PairKey, IReadOnlyList<Polygon>> pairs = [];
        private long signature;
        private bool hasSignature;

        /// <summary>The overlap regions as of the last <see cref="Update"/>.</summary>
        public IReadOnlyList<CellOverlapRegion> Regions { get; private set; } = [];

        /// <returns>True when <see cref="Regions"/> changed.</returns>
        public bool Update(IEnumerable<LocationObj> locations, IVolumeToSectionTransform transform, Func<long, long?>? structureParentLookup = null)
        {
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            List<LocationObj> eligible =
            [
                .. locations
                    .Where(location => location is not null && location.ParentID.HasValue && location.TypeCode.AllowsInteriorHoles() &&
                                       location.MosaicShape is not null && !location.MosaicShape.IsNull)
                    .OrderBy(location => location.ID)
            ];

            long next = RuntimeHelpers.GetHashCode(transform);
            foreach (LocationObj location in eligible)
            {
                next = unchecked((next * 31) + location.ID);
                next = unchecked((next * 31) + RuntimeHelpers.GetHashCode(location.MosaicShape));
                next = unchecked((next * 31) + location.ParentID!.Value);
            }

            if (hasSignature && next == signature)
                return false;

            Dictionary<PairKey, IReadOnlyList<Polygon>> remembered = [];
            List<CellOverlapRegion> regions = [];
            List<(LocationObj Location, Polygon Shape, int Stamp)> shapes = [];
            foreach (LocationObj location in eligible)
            {
                if (AutoPolygonizeSelection.TryGetVolumePolygon(location, transform, out Polygon? shape) && shape is not null)
                    shapes.Add((location, shape, RuntimeHelpers.GetHashCode(location.MosaicShape)));
            }

            for (int i = 0; i < shapes.Count; i++)
            {
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    (LocationObj first, Polygon firstShape, int firstStamp) = shapes[i];
                    (LocationObj second, Polygon secondShape, int secondStamp) = shapes[j];
                    if (first.Section != second.Section ||
                        AutoPolygonizeSelection.IsRelatedStructure(second.ParentID, first.ParentID!.Value, structureParentLookup) ||
                        !firstShape.BoundingBox.Intersects(secondShape.BoundingBox))
                    {
                        continue;
                    }

                    PairKey key = new(first.ID, second.ID, firstStamp, secondStamp);
                    if (!pairs.TryGetValue(key, out IReadOnlyList<Polygon>? parts))
                        parts = CellOverlapGeometry.Intersection(firstShape, secondShape);

                    remembered[key] = parts;
                    regions.AddRange(parts.Select(part => new CellOverlapRegion(first.ID, second.ID, part)));
                }
            }

            pairs = remembered;
            signature = next;
            hasSignature = true;
            Regions = regions;
            return true;
        }

        /// <summary>The smallest region under <paramref name="position"/> (volume space), so a small overlap inside a large one is reachable.</summary>
        public bool TryHit(Vector2 position, out CellOverlapRegion? region)
        {
            region = Regions
                .Where(candidate => candidate.Shape.BoundingBox.Contains(position) && candidate.Shape.Covers(position))
                .OrderBy(candidate => candidate.Shape.Area)
                .FirstOrDefault();
            return region is not null;
        }

        /// <summary>Forgets everything, for a section change.</summary>
        public void Clear()
        {
            pairs = [];
            Regions = [];
            hasSignature = false;
        }
    }
}

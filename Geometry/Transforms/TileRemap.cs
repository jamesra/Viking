using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry.Transforms
{
    /// <summary>
    /// A run of consecutive vertices of one shape part that lie on the same tile, as tile pixels. Serialized by VikingAU's
    /// <c>remap-to-tiles</c>, so its members are plain settable data.
    /// </summary>
    public sealed class TileRun
    {
        /// <summary>The tile number (<see cref="TileTransformInfo.TileNumber"/>).</summary>
        public int Tile { get; set; }

        /// <summary>The vertices as [x, y] pixels on the tile image, in the part's order.</summary>
        public List<double[]> Points { get; set; } = [];

        /// <summary>Indices into <see cref="Points"/> of vertices that were <see cref="TileMappingStatus.Extrapolated"/>; null if none.</summary>
        public List<int>? Extrapolated { get; set; }

        /// <summary>
        /// Per vertex, the position on a second tile whose image also holds it, as [tile, x, y], or null; the whole list is
        /// null when no vertex of the run lies in an overlap. Used only to report how far apart the two tiles put it.
        /// </summary>
        public List<double[]?>? Alternates { get; set; }
    }

    /// <summary>One part of a shape (a ring, a line, a point, a circle's five samples) as runs of tile vertices.</summary>
    public sealed class TilePart
    {
        public const string Exterior = "exterior";
        public const string Hole = "hole";
        public const string Line = "line";
        public const string Point = "point";

        /// <summary>The circle's centre then its east, north, west and south rim points (<see cref="CircleMapping.SamplePoints"/>).</summary>
        public const string CircleSamples = "circle";

        public string Role { get; set; } = Exterior;

        /// <summary>Runs in vertex order; concatenating their points gives the part's vertices.</summary>
        public List<TileRun> Runs { get; set; } = [];

        public int VertexCount => Runs.Sum(r => r.Points.Count);
    }

    /// <summary>A mosaic shape expressed in tile coordinates (<see cref="TileRemap.ToTiles"/>).</summary>
    public sealed class TileShape
    {
        /// <summary>The shape's <see cref="ShapeType2D"/> name.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>A circle's mosaic radius in the old mosaic; null for other shapes.</summary>
        public double? Radius { get; set; }

        public List<TilePart> Parts { get; set; } = [];
    }

    /// <summary>The outcome of <see cref="TileRemap.ToTiles"/>.</summary>
    public sealed class ToTilesResult
    {
        /// <summary>The shape in tile coordinates; null when any vertex has no tile (<see cref="Uncovered"/>).</summary>
        public TileShape? Shape { get; internal set; }

        /// <summary>Vertices no tile of the old mosaic covers. Non-empty means the shape cannot be remapped.</summary>
        public List<Vector2> Uncovered { get; } = [];

        public int MappedCount { get; internal set; }
        public int ExtrapolatedCount { get; internal set; }
    }

    /// <summary>The outcome of <see cref="TileRemap.FromTiles"/>.</summary>
    public sealed class FromTilesResult
    {
        /// <summary>The shape in the new mosaic; null on failure.</summary>
        public IShape2D? Shape { get; internal set; }

        /// <summary>Why the shape could not be rebuilt; null on success.</summary>
        public string? Failure { get; internal set; }

        public bool Succeeded => Failure is null;

        /// <summary>The new position of every vertex, in <see cref="TileRemap.Vertices"/> order.</summary>
        public List<Vector2> NewVertices { get; } = [];

        /// <summary>
        /// For vertices that lay in an overlap, the distance between their new position through the chosen tile and through
        /// the second tile; large where the old mosaic misregistered the overlap.
        /// </summary>
        public List<double> OverlapDisagreements { get; } = [];

        /// <summary>Tile numbers the shape uses that the new mosaic does not place.</summary>
        public SortedSet<int> MissingTiles { get; } = [];
    }

    /// <summary>
    /// Moves a mosaic shape from an old mosaic to a rebuilt one through the tiles both place: each vertex goes to a tile pixel
    /// through the old mosaic (<see cref="ToTiles"/>), then to mosaic space through the new mosaic's transform for the same
    /// tile (<see cref="FromTiles"/>). Vertices are grouped in runs that share a tile. A circle is carried as its centre and
    /// four rim points and rebuilt by the five-point mean scale (<see cref="CircleMapping"/>,
    /// <c>.cursor/rules/circle-mapping-scale.mdc</c>).
    /// </summary>
    public static class TileRemap
    {
        /// <summary>The vertices of a shape in the order <see cref="ToTiles"/> walks them (a circle gives its five samples).</summary>
        /// <exception cref="NotSupportedException">The shape is not a point, circle, line segment, polyline or polygon.</exception>
        public static IEnumerable<Vector2> Vertices(IShape2D shape) => Parts(shape).SelectMany(p => p.Vertices);

        /// <summary>Expresses a mosaic shape as tile runs through the old mosaic.</summary>
        /// <exception cref="NotSupportedException">The shape is not a point, circle, line segment, polyline or polygon.</exception>
        public static ToTilesResult ToTiles(IShape2D mosaicShape, MosaicTileLocator oldMosaic)
        {
            if (mosaicShape is null)
                throw new ArgumentNullException(nameof(mosaicShape));
            if (oldMosaic is null)
                throw new ArgumentNullException(nameof(oldMosaic));

            ToTilesResult result = new();
            TileShape shape = new()
            {
                Kind = mosaicShape.ShapeType.ToString(),
                Radius = mosaicShape is Circle circle ? circle.Radius : null,
            };

            foreach ((string role, Vector2[] vertices) in Parts(mosaicShape))
            {
                TilePart part = new() { Role = role };
                TileRun? run = null;
                int? centreTile = null;
                foreach (Vector2 v in vertices)
                {
                    TilePoint located = oldMosaic.Locate(v, out TilePoint? alternate);
                    if (located.Status == TileMappingStatus.NoTile)
                    {
                        result.Uncovered.Add(v);
                        continue;
                    }

                    // A circle's rim samples measure the local scale at its centre, so they go through the centre's tile
                    // wherever its image holds them. Through a neighbouring tile that the new mosaic places elsewhere (a
                    // repair moves misplaced tiles by thousands of pixels) one rim sample would inflate the five-point mean.
                    if (role == TilePart.CircleSamples)
                    {
                        if (centreTile is null)
                        {
                            centreTile = located.TileNumber;
                        }
                        else if (located.TileNumber != centreTile.Value &&
                                 oldMosaic.TryLocateOnTile(centreTile.Value, v, out TilePoint onCentreTile))
                        {
                            alternate = located.Status == TileMappingStatus.Mapped ? located : null;
                            located = onCentreTile;
                        }
                    }

                    if (run is null || run.Tile != located.TileNumber)
                    {
                        run = new TileRun { Tile = located.TileNumber };
                        part.Runs.Add(run);
                    }

                    if (located.Status == TileMappingStatus.Extrapolated)
                    {
                        (run.Extrapolated ??= []).Add(run.Points.Count);
                        result.ExtrapolatedCount++;
                    }
                    else
                    {
                        result.MappedCount++;
                    }

                    if (alternate is TilePoint alt)
                    {
                        run.Alternates ??= [.. Enumerable.Repeat<double[]?>(null, run.Points.Count)];
                        run.Alternates.Add([alt.TileNumber, alt.Pixel.X, alt.Pixel.Y]);
                    }
                    else
                    {
                        run.Alternates?.Add(null);
                    }

                    run.Points.Add([located.Pixel.X, located.Pixel.Y]);
                }

                shape.Parts.Add(part);
            }

            if (result.Uncovered.Count == 0)
                result.Shape = shape;

            return result;
        }

        /// <summary>
        /// Rebuilds the shape in the new mosaic: every run's pixels through the new mosaic's transform for its tile. Fails,
        /// with nothing partial, when a tile is not in the new mosaic, a pixel does not map, or the mapped vertices do not
        /// form a valid shape of the same kind.
        /// </summary>
        public static FromTilesResult FromTiles(TileShape tileShape, MosaicTileLocator newMosaic)
        {
            if (tileShape is null)
                throw new ArgumentNullException(nameof(tileShape));
            if (newMosaic is null)
                throw new ArgumentNullException(nameof(newMosaic));

            FromTilesResult result = new();
            List<(string Role, Vector2[] Vertices)> parts = [];
            foreach (TilePart part in tileShape.Parts)
            {
                List<Vector2> mapped = new(part.VertexCount);
                foreach (TileRun run in part.Runs)
                {
                    if (!newMosaic.ContainsTile(run.Tile))
                    {
                        result.MissingTiles.Add(run.Tile);
                        continue;
                    }

                    for (int i = 0; i < run.Points.Count; i++)
                    {
                        Vector2 pixel = new(run.Points[i][0], run.Points[i][1]);
                        if (!newMosaic.TryMapFromTile(run.Tile, pixel, out Vector2 p))
                        {
                            result.Failure ??= $"Tile {run.Tile}: pixel ({pixel.X:F1}, {pixel.Y:F1}) does not map in the new mosaic";
                            continue;
                        }

                        mapped.Add(p);
                        if (run.Alternates?[i] is double[] alt &&
                            newMosaic.TryMapFromTile((int)alt[0], new Vector2(alt[1], alt[2]), out Vector2 altPoint))
                        {
                            result.OverlapDisagreements.Add(Vector2.Distance(p, altPoint));
                        }
                    }
                }

                parts.Add((part.Role, mapped.ToArray()));
                result.NewVertices.AddRange(mapped);
            }

            if (result.MissingTiles.Count > 0)
                result.Failure = $"The new mosaic does not place tile(s) {string.Join(", ", result.MissingTiles)}";

            if (result.Failure is not null)
                return result;

            try
            {
                result.Shape = Build(tileShape, parts);
            }
            catch (ArgumentException e)
            {
                result.Failure = $"The remapped vertices do not form a valid {tileShape.Kind}: {e.Message}";
            }

            return result;
        }

        private static IShape2D Build(TileShape tileShape, List<(string Role, Vector2[] Vertices)> parts)
        {
            if (!Enum.TryParse(tileShape.Kind, out ShapeType2D kind))
                throw new ArgumentException($"Unknown shape kind {tileShape.Kind}");

            switch (kind)
            {
                case ShapeType2D.Point:
                    return Single(parts, TilePart.Point)[0];

                case ShapeType2D.Circle:
                {
                    Vector2[] samples = Single(parts, TilePart.CircleSamples);
                    if (samples.Length != CircleMapping.PointCount || tileShape.Radius is not double radius)
                        throw new ArgumentException("A circle needs its radius and five sample points");

                    if (!CircleMapping.TryCircleFromMappedPoints(radius, samples, RimSamplesToUse(tileShape), out Circle circle))
                        throw new ArgumentException("The circle's samples give no radius");

                    return circle;
                }

                case ShapeType2D.Line:
                {
                    Vector2[] points = Single(parts, TilePart.Line);
                    if (points.Length != 2)
                        throw new ArgumentException("A line segment needs two points");
                    return new LineSegment(points[0], points[1]);
                }

                case ShapeType2D.Polyline:
                    return new Polyline(Single(parts, TilePart.Line));

                case ShapeType2D.Polygon:
                {
                    Vector2[] exterior = Single(parts, TilePart.Exterior);
                    Vector2[][] holes = [.. parts.Where(p => p.Role == TilePart.Hole).Select(p => p.Vertices)];
                    return holes.Length == 0 ? new Polygon(exterior) : new Polygon(exterior, holes);
                }

                default:
                    throw new ArgumentException($"Shape kind {kind} is not remapped");
            }
        }

        /// <summary>
        /// Which of a circle's five samples set its new radius: the centre, and the rim samples on the centre's tile when at
        /// least one is; otherwise every rim sample. A rim sample off the centre's tile image lies on a neighbour, and a
        /// repaired mosaic may have moved that neighbour thousands of pixels (RC2 1240: a 1,346 px circle became 12,769 px).
        /// Tile images are not rescaled between mosaics, so the centre tile's local scale is the circle's.
        /// </summary>
        private static bool[] RimSamplesToUse(TileShape tileShape)
        {
            int[] tiles = [.. tileShape.Parts.Single(p => p.Role == TilePart.CircleSamples)
                                     .Runs.SelectMany(r => r.Points.Select(_ => r.Tile))];
            bool anyRimOnCentre = tiles.Skip(1).Any(t => t == tiles[0]);
            return [.. tiles.Select((t, i) => i == 0 || !anyRimOnCentre || t == tiles[0])];
        }

        private static Vector2[] Single(List<(string Role, Vector2[] Vertices)> parts, string role)
        {
            var matches = parts.Where(p => p.Role == role).ToList();
            if (matches.Count != 1 || matches[0].Vertices.Length == 0)
                throw new ArgumentException($"Expected one non-empty {role} part");
            return matches[0].Vertices;
        }

        private static IEnumerable<(string Role, Vector2[] Vertices)> Parts(IShape2D shape)
        {
            switch (shape)
            {
                case Vector2 point:
                    yield return (TilePart.Point, [point]);
                    break;
                case Circle circle:
                    yield return (TilePart.CircleSamples, CircleMapping.SamplePoints(circle.Center, circle.Radius));
                    break;
                case LineSegment segment:
                    yield return (TilePart.Line, [segment.A, segment.B]);
                    break;
                case Polyline polyline:
                    yield return (TilePart.Line, [.. polyline.Points.Select(p => p.ToVector2())]);
                    break;
                case Polygon polygon:
                    yield return (TilePart.Exterior, polygon.ExteriorRing);
                    foreach (Vector2[] hole in polygon.InteriorRings)
                        yield return (TilePart.Hole, hole);
                    break;
                default:
                    throw new NotSupportedException($"Shape type {shape?.ShapeType} is not remapped");
            }
        }
    }
}

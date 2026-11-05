using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Viking.gRPC.Geometry.V1;
using Viking.gRPC.SegmentationServiceTypes.V1;
using Viking.SegmentationPrompts.Rules;

namespace Viking.SegmentationPrompts
{
    /// <summary>
    /// Builds the segmentation server's requests for annotation locations from a serialized <c>PromptInput</c>
    /// (gRPC_Protos <c>Segmentation/SAM2/prompt_input.proto</c>), so every client asks the same question the same way
    /// and shares the server's result cache. Bytes in, bytes out: callers parse the results with their own generated
    /// types.
    /// </summary>
    /// <remarks>
    /// The rules, which follow how the served model was trained (Sam2Trainer <c>data/prompts.py</c>):
    /// <list type="bullet">
    /// <item>Tiles: mosaic space at downsample 1, <c>channel</c> and <c>transform</c> both the VikingXML tileset name, which
    /// is what the server reads tiles by when the request names an annotation context.</item>
    /// <item>A circle is its disc at the TRUE mosaic radius (the server applies the model's scale) and one click at
    /// its X, Y: training's circle prompt.</item>
    /// <item>A polygon or curve polygon is an outline to correct (<c>correction_feature</c>), a curve polygon's rings
    /// fitted as the exporter fitted them. It carries no click: the server gives each island its deepest pixel, which
    /// is training's click for a mask prompt, from the same raster it encodes.</item>
    /// <item>Background: <see cref="NeighbourRule"/>, among the candidates whose bounds meet the reach (the targets'
    /// cells grown by one cell each way). <c>neighbors</c> lists all of them, so the cache key is the same whatever
    /// the growth asks for later.</item>
    /// <item>Everything is ordered by location id, or by cell row then column, so the bytes depend on the input's
    /// content and not its order.</item>
    /// </list>
    /// </remarks>
    public static class PromptBuilder
    {
        /// <summary>Most tiles a prompt may start on (the server's own limit).</summary>
        public const int MaxStartTiles = 64;

        /// <summary>Most shapes a prompt's object may have.</summary>
        public const int MaxPromptShapes = 512;

        /// <summary>How many cells around the targets' cells the background is looked for.</summary>
        public const int ReachCells = 1;

        /// <summary>The <c>SegmentTilesStreamRequest.start</c> message (<c>SegmentTilesRequest</c>) for the input.</summary>
        /// <exception cref="ArgumentException">No targets, no volume or tileset, a target type that is not a circle or an
        /// area, circles and areas in one prompt, or bad target geometry.</exception>
        /// <exception cref="FormatException">A target's WKT that is not a POLYGON or MULTIPOLYGON.</exception>
        /// <exception cref="PromptTooLargeException">Over <see cref="MaxStartTiles"/> tiles or <see cref="MaxPromptShapes"/> shapes.</exception>
        public static byte[] BuildStart(byte[] promptInput) => Plan.Of(PromptInput.Parser.ParseFrom(promptInput)).Start().ToByteArray();

        /// <summary>
        /// A <c>PromptReach</c>: the mosaic rectangle within which the caller must pass every annotation of the section as
        /// a candidate. Candidates are not needed for this call.
        /// </summary>
        public static byte[] Reach(byte[] promptInput)
        {
            CellRange reach = Plan.ReachOf(Plan.TargetsOf(PromptInput.Parser.ParseFrom(promptInput)));
            return new PromptReach
            {
                Reach = new Rectangle
                {
                    XMin = (double)reach.ColMin * AnnotationGeometry.CellSize,
                    YMin = (double)reach.RowMin * AnnotationGeometry.CellSize,
                    XMax = (double)(reach.ColMax + 1) * AnnotationGeometry.CellSize,
                    YMax = (double)(reach.RowMax + 1) * AnnotationGeometry.CellSize,
                },
            }.ToByteArray();
        }

        /// <summary>
        /// A <c>PromptBackground</c> answering one <c>TilesNeeded</c>: the shapes of the background annotations that
        /// reach its <c>background_cells</c> and were not sent yet. The start's background counts as sent; pass the
        /// <c>sent_ids</c> of earlier answers in this stream as <paramref name="sentIds"/>.
        /// </summary>
        /// <param name="promptInput">The same input the start was built from.</param>
        /// <param name="tilesNeeded">The serialized <c>TilesNeeded</c> message.</param>
        /// <param name="sentIds">Location ids earlier answers sent.</param>
        public static byte[] BackgroundFor(byte[] promptInput, byte[] tilesNeeded, IEnumerable<long> sentIds)
        {
            Plan plan = Plan.Of(PromptInput.Parser.ParseFrom(promptInput));
            TilesNeeded needed = TilesNeeded.Parser.ParseFrom(tilesNeeded);
            return plan.Background(needed.BackgroundCells.Select(cell => (cell.Row, cell.Col)), sentIds).ToByteArray();
        }

        /// <summary>The input's targets, background and cells, worked out once.</summary>
        internal sealed class Plan
        {
            private readonly PromptInput input;
            private readonly List<AnnotationGeometry> targets;
            private readonly List<AnnotationGeometry> background;
            private readonly List<(int Row, int Col)> startCells;

            private Plan(PromptInput input, List<AnnotationGeometry> targets, List<AnnotationGeometry> background,
                List<(int Row, int Col)> startCells)
            {
                this.input = input;
                this.targets = targets;
                this.background = background;
                this.startCells = startCells;
            }

            internal static List<AnnotationGeometry> TargetsOf(PromptInput input)
            {
                if (input.Targets.Count == 0)
                    throw new ArgumentException("A prompt needs at least one target location.");
                return input.Targets
                    .GroupBy(target => target.Id).Select(group => group.First())
                    .OrderBy(target => target.Id)
                    .Select(AnnotationGeometry.From)
                    .ToList();
            }

            internal static CellRange ReachOf(IEnumerable<AnnotationGeometry> targets) =>
                targets.Select(target => target.Cells).Aggregate((a, b) => a.Union(b)).Grown(ReachCells);

            public static Plan Of(PromptInput input)
            {
                if (string.IsNullOrEmpty(input.Volume) || string.IsNullOrEmpty(input.Tileset))
                    throw new ArgumentException("A prompt needs the volume and the section's tileset name.");
                List<AnnotationGeometry> targets = TargetsOf(input);
                if (targets.Any(target => target.IsCircle) && targets.Any(target => !target.IsCircle))
                    throw new ArgumentException("A prompt is circles or areas, not both: the server takes seeds or an outline to correct.");
                int shapeCount = targets.Sum(target => target.Shapes.Count);
                if (shapeCount > MaxPromptShapes)
                    throw new PromptTooLargeException($"The targets have {shapeCount} shapes; a prompt takes at most {MaxPromptShapes}.");
                List<(int Row, int Col)> cells = targets
                    .SelectMany(target => target.Cells.RowMajor())
                    .Distinct()
                    .OrderBy(cell => cell.Row).ThenBy(cell => cell.Col)
                    .ToList();
                if (cells.Count > MaxStartTiles)
                    throw new PromptTooLargeException($"The targets cover {cells.Count} tiles; a prompt starts on at most {MaxStartTiles}.");

                CellRange reach = ReachOf(targets);
                var rule = new NeighbourRule(input.Targets, input.Structures);
                var background = new List<AnnotationGeometry>();
                foreach (PromptAnnotation candidate in input.Candidates
                    .Where(rule.Accepts)
                    .GroupBy(candidate => candidate.Id).Select(group => group.First())
                    .OrderBy(candidate => candidate.Id))
                {
                    AnnotationGeometry geometry;
                    try
                    {
                        geometry = AnnotationGeometry.From(candidate);
                    }
                    catch (Exception e) when (e is ArgumentException || e is FormatException)
                    {
                        // A neighbour whose shape cannot be read is left out by every caller alike; it must not stop
                        // the target from being segmented.
                        continue;
                    }
                    if (geometry.Cells.Meets(reach))
                        background.Add(geometry);
                }
                return new Plan(input, targets, background, cells);
            }

            public SegmentTilesRequest Start()
            {
                var request = new SegmentTilesRequest
                {
                    Grid = new TileGrid
                    {
                        Volume = input.Volume,
                        Section = input.Section,
                        Channel = input.Tileset,
                        Transform = input.Tileset,
                        Downsample = 1,
                        Space = CoordinateSpace.Mosaic,
                    },
                    RequestId = input.RequestId,
                    AnnotationContextId = input.AnnotationContextId,
                    Refresh = input.Refresh,
                };
                var foreground = new Feature();
                var correction = new Feature();
                var backgroundFeature = new Feature();
                request.Tiles.AddRange(startCells.Select(cell => new GridCell { Row = cell.Row, Col = cell.Col }));
                if (input.HasMaskThreshold)
                    request.MaskThreshold = input.MaskThreshold;
                if (!string.IsNullOrEmpty(input.ModelId) && input.ModelId != "unknown")
                    request.ModelId = input.ModelId;
                if (input.JobId != 0)
                    request.JobId = input.JobId;

                foreach (AnnotationGeometry target in targets)
                {
                    if (target.IsCircle)
                    {
                        foreground.Shapes.AddRange(target.Shapes);
                        foreground.Points.Add(new Vector2 { X = target.Annotation.X, Y = target.Annotation.Y });
                    }
                    else
                    {
                        correction.Shapes.AddRange(target.Shapes);
                    }
                    request.Locations.Add(Ref(target.Annotation));
                }

                foreach (AnnotationGeometry neighbour in background)
                {
                    if (startCells.Any(cell => neighbour.Cells.Contains(cell.Row, cell.Col)))
                        backgroundFeature.Shapes.AddRange(neighbour.Shapes);
                    request.Neighbors.Add(Ref(neighbour.Annotation));
                }

                // A feature with nothing in it is left unset, so the bytes carry only what the prompt holds.
                if (foreground.Shapes.Count > 0)
                    request.ForegroundFeature = foreground;
                if (correction.Shapes.Count > 0)
                    request.CorrectionFeature = correction;
                if (backgroundFeature.Shapes.Count > 0)
                    request.BackgroundFeature = backgroundFeature;
                return request;
            }

            public PromptBackground Background(IEnumerable<(int Row, int Col)> cells, IEnumerable<long> sentIds)
            {
                List<(int Row, int Col)> asked = cells.ToList();
                var sent = new HashSet<long>(sentIds);
                var answer = new PromptBackground();
                foreach (AnnotationGeometry neighbour in background)
                {
                    long id = neighbour.Annotation.Id;
                    if (sent.Contains(id) || startCells.Any(cell => neighbour.Cells.Contains(cell.Row, cell.Col)))
                        continue;
                    if (!asked.Any(cell => neighbour.Cells.Contains(cell.Row, cell.Col)))
                        continue;
                    answer.Shapes.AddRange(neighbour.Shapes);
                    answer.SentIds.Add(id);
                }
                return answer;
            }

            private static LocationRef Ref(PromptAnnotation annotation) =>
                new LocationRef { Id = annotation.Id, LastModifiedTicks = annotation.LastModifiedTicks };
        }
    }
}

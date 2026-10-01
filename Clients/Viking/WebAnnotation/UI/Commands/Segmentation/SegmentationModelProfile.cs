using System;
using ModelCapabilities = Viking.gRPC.SegmentationServiceTypes.V1.ModelCapabilities;
using ResolutionMode = Viking.gRPC.SegmentationServiceTypes.V1.ResolutionMode;
using SubmissionMode = Viking.gRPC.SegmentationServiceTypes.V1.SubmissionMode;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// How the segmentation model in use wants its input, using the <c>SubmissionMode</c> and
    /// <c>ResolutionMode</c> enums from segmentation.proto so client and server share one definition.
    /// Immutable so a profile adopted from the server can be swapped in with one reference write.
    /// </summary>
    internal sealed class SegmentationModelProfile
    {
        public SegmentationModelProfile(SubmissionMode submission, ResolutionMode resolution)
        {
            Submission = submission;
            Resolution = resolution;
        }

        /// <summary>Fixed grid tiles or the whole screen. Never UNSPECIFIED.</summary>
        public SubmissionMode Submission { get; }

        /// <summary>Whether the tile level can follow zoom. Not consulted for a full viewport.</summary>
        public ResolutionMode Resolution { get; }

        /// <summary>True for UploadTile/SegmentTiles on the shared grid, the only path the client implements today.</summary>
        public bool UsesFixedTiles => Submission != SubmissionMode.FullViewport;

        /// <summary>
        /// True when a tile's image and mask depend on the viewport rectangle, because the submitted
        /// image is the screen itself. Any view move then makes an earlier result stale.
        /// </summary>
        public bool ResultDependsOnViewport => !UsesFixedTiles;

        /// <summary>True when fixed tiles can be submitted at more than one level as the user zooms.</summary>
        public bool TileLevelCanChange => UsesFixedTiles && Resolution == ResolutionMode.Multi;

        /// <summary>
        /// Whether moving the camera can make a submitted or finished result wrong. False for fixed
        /// tiles at one level, where a pan or zoom leaves every tile key and mask unchanged.
        /// </summary>
        public bool CameraMoveCanStaleResults => ResultDependsOnViewport || TileLevelCanChange;

        /// <summary>
        /// The profile used until the server reports its capabilities, and the one the current SAM2 model
        /// advertises: fixed grid tiles at a single level. The local <c>SegmentationTileDownsample</c>
        /// ceiling does not change it; only a server that allows multi resolution can.
        /// </summary>
        public static SegmentationModelProfile Default { get; } =
            new(SubmissionMode.FixedTileGrid, ResolutionMode.Single);

        /// <summary>
        /// Builds the profile from what the server advertised in GetServerStatus. Submission follows the
        /// server. The local ceiling can only narrow resolution: multi needs the server to allow it and a
        /// ceiling above 1.
        /// </summary>
        /// <param name="advertised">Capabilities from GetServerStatus.</param>
        /// <param name="maxTileDownsample">The local <c>SegmentationTileDownsample</c> ceiling.</param>
        /// <exception cref="ArgumentException">
        /// A flag is UNSPECIFIED. The server always sets both, so that is a server bug, and guessing
        /// would submit input the model cannot use.
        /// </exception>
        public static SegmentationModelProfile FromAdvertised(ModelCapabilities advertised, int maxTileDownsample)
        {
            if (advertised is null)
                throw new ArgumentNullException(nameof(advertised));

            if (advertised.SubmissionMode == SubmissionMode.Unspecified ||
                advertised.ResolutionMode == ResolutionMode.Unspecified)
            {
                throw new ArgumentException(
                    $"Server advertised an unspecified mode (submission={advertised.SubmissionMode}, " +
                    $"resolution={advertised.ResolutionMode}).",
                    nameof(advertised));
            }

            ResolutionMode resolution = maxTileDownsample > 1 && advertised.ResolutionMode == ResolutionMode.Multi
                ? ResolutionMode.Multi
                : ResolutionMode.Single;

            return new SegmentationModelProfile(advertised.SubmissionMode, resolution);
        }
    }
}

using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// What the segmentation server says about how its model wants to be used, read from GetServerStatus
    /// (ModelInfo and ModelCapabilities, which come from the model's manifest).
    /// </summary>
    public sealed class SegmentationServerContract
    {
        /// <summary>ModelInfo.model_id. Sent back as SegmentTilesRequest.model_id.</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>The manifest says a request without a model_id is refused.</summary>
        public bool RequiresModelId { get; set; }

        public SegmentationServiceTypes.SubmissionMode SubmissionMode { get; set; }

        public SegmentationServiceTypes.ResolutionMode ResolutionMode { get; set; }

        /// <summary>client_contract.mask_roles; empty when the manifest lists none.</summary>
        public string[] MaskRoles { get; set; } = [];

        /// <summary>True when the model is fed by UploadTile and SegmentTilesStream, which is all this client does.</summary>
        public bool AcceptsTiles => SubmissionMode != SegmentationServiceTypes.SubmissionMode.FullViewport;

        /// <summary>
        /// Reads the contract from a status reply. A reply from a server that predates the manifest has no
        /// <c>Model</c>; it is treated as a tiled model with no model_id requirement.
        /// </summary>
        public static SegmentationServerContract FromStatus(SegmentationServiceTypes.ServerStatusResponse status)
        {
            if (status is null)
                throw new ArgumentNullException(nameof(status));

            bool requiresModelId = false;
            string[] maskRoles = [];
            string contractJson = status.Model?.ContractJson;
            if (!string.IsNullOrWhiteSpace(contractJson))
            {
                JObject contract = JObject.Parse(contractJson);
                requiresModelId = contract.Value<bool?>("requires_model_id") ?? false;
                if (contract["mask_roles"] is JArray roles)
                    maskRoles = [.. roles.Select(role => role.ToString())];
            }

            return new SegmentationServerContract
            {
                ModelId = status.Model?.ModelId ?? string.Empty,
                RequiresModelId = requiresModelId,
                SubmissionMode = status.Capabilities?.SubmissionMode ?? SegmentationServiceTypes.SubmissionMode.Unspecified,
                ResolutionMode = status.Capabilities?.ResolutionMode ?? SegmentationServiceTypes.ResolutionMode.Unspecified,
                MaskRoles = maskRoles
            };
        }
    }
}

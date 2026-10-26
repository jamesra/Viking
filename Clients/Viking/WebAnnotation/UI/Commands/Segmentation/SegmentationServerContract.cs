using Newtonsoft.Json.Linq;
using System;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// What the segmentation server says about how its model wants to be used, read from GetServerStatus (ModelInfo, which
    /// comes from the model's manifest). Every served model is a single-resolution tile grid fed by UploadTile and
    /// SegmentTilesStream, and the server encodes the prompt for it, so only the model's identity matters to the client.
    /// </summary>
    public sealed class SegmentationServerContract
    {
        /// <summary>ModelInfo.model_id. Sent back as SegmentTilesRequest.model_id.</summary>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>The manifest says a request without a model_id is refused.</summary>
        public bool RequiresModelId { get; set; }

        /// <summary>
        /// Reads the contract from a status reply. A reply without a <c>Model</c> is treated as a model with no model_id
        /// requirement.
        /// </summary>
        public static SegmentationServerContract FromStatus(SegmentationServiceTypes.ServerStatusResponse status)
        {
            if (status is null)
                throw new ArgumentNullException(nameof(status));

            bool requiresModelId = false;
            string contractJson = status.Model?.ContractJson;
            if (!string.IsNullOrWhiteSpace(contractJson))
            {
                JObject contract = JObject.Parse(contractJson);
                requiresModelId = contract.Value<bool?>("requires_model_id") ?? false;
            }

            return new SegmentationServerContract
            {
                ModelId = status.Model?.ModelId ?? string.Empty,
                RequiresModelId = requiresModelId
            };
        }
    }
}

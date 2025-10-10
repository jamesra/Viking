using Grpc.Core;
using gRPCSegmentAnything; 

namespace gRPCSegmentAnything.Services
{
    public class SegmentationService : Segmentation.SegmentationBase
    {
        private readonly ILogger<SegmentationService> _logger;

        public SegmentationService(ILogger<SegmentationService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Processes an image and a list of points to generate a labeled image.
        /// </summary>
        /// <param name="request">The gRPC request containing the image and points.</param>
        /// <param name="context">The gRPC server call context.</param>
        /// <returns>A labeled image as a response.</returns>
        public async Task<LabeledImageResponse> SegmentByPoints(ProcessImageRequest request, ServerCallContext context)
        {
            // Validate the request
            if (request.ImageData == null || request.ImageData.Length == 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Image data is required."));
            }

            if (request.Points == null || request.Points.Count == 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one point is required."));
            }

            _logger.LogInformation("Processing image with {PointCount} points.", request.Points.Count);

            // Simulate image processing (replace with actual logic)
            byte[] labeledImage = await SimulateImageProcessingAsync(request.ImageData, request.Points);

            // Return the labeled image
            return new LabeledImageResponse
            {
                LabeledImageData = Google.Protobuf.ByteString.CopyFrom(labeledImage)
            };
        }

        /// <summary>
        /// Simulates image processing and generates a labeled image.
        /// Replace this with actual image processing logic.
        /// </summary>
        /// <param name="imageData">The input image data as a byte array.</param>
        /// <param name="points">The list of points for processing.</param>
        /// <returns>The labeled image as a byte array.</returns>
        private Task<byte[]> SimulateImageProcessingAsync(byte[] imageData, Google.Protobuf.Collections.RepeatedField<Point> points)
        {
            // Placeholder logic: return the original image data as the labeled image
            // Replace this with actual image processing logic
            return Task.FromResult(imageData);
        }
    }
}

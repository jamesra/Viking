"""
Segmentation Service gRPC Server

This module implements the gRPC server for the segmentation service.
It uses grpc.aio for asynchronous operation and connects the gRPC interface
to the SegmentationModel implementation.
"""

import asyncio
import grpc
import numpy as np
import cv2
import io
from concurrent import futures
from typing import List, Tuple

# Import the generated gRPC code from the segmentation_grpc package
from segmentation_grpc.segmentation_grpc import (
    SegmentationRequest,
    SegmentationResponse,
    UploadImageRequest,
    UploadImageResponse,
    DeleteImageRequest,
    DeleteImageResponse,
    Point,
    Polygon,
    SegmentResult,
    SegmentationServiceServicer,
    add_SegmentationServiceServicer_to_server
)

# Import the segmentation model and image cache
from segmentation_server.segmentation_service import SegmentationModel
from segmentation_server.image_cache import ImageCache


class SegmentationServicer(SegmentationServiceServicer):
    """
    Implementation of the SegmentationService gRPC service.

    This class handles gRPC requests for image segmentation, converting between
    the gRPC message format and the format expected by the SegmentationModel.
    """

    def __init__(self, 
                 cache_max_memory_bytes: int = 1073741824,  # 1 GB
                 cache_ttl_seconds: int = 300):  # 5 minutes
        """
        Initialize the servicer with a SegmentationModel and ImageCache.
        
        Args:
            cache_max_memory_bytes: Maximum memory for image cache (default: 1 GB)
            cache_ttl_seconds: Time-to-live for cached images (default: 5 minutes)
        """
        self.model = SegmentationModel()
        self.image_cache = ImageCache(
            max_memory_bytes=cache_max_memory_bytes,
            ttl_seconds=cache_ttl_seconds
        )

    async def UploadImage(self, request, context):
        """
        Upload an image to the cache and return a unique ID.
        
        Args:
            request: The UploadImageRequest message
            context: The gRPC context
            
        Returns:
            An UploadImageResponse message containing the image ID
        """
        try:
            # Extract image data from request
            image_data = request.image_data
            width = request.width
            height = request.height
            
            print(f"==> UploadImage RPC called: {width}x{height}, {len(image_data)} bytes")
            
            # Upload to cache
            image_id = await self.image_cache.upload_image(image_data, width, height)
            
            print(f"<== UploadImage RPC completed: assigned ID={image_id}")
            
            # Return response with image ID
            return UploadImageResponse(image_id=image_id)
            
        except Exception as e:
            import traceback
            stack_trace = traceback.format_exc()
            print(f"Error uploading image: {e}\n{stack_trace}")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error uploading image: {e}")
    
    async def DeleteImage(self, request, context):
        """
        Delete an image from the cache.
        
        Args:
            request: The DeleteImageRequest message
            context: The gRPC context
            
        Returns:
            A DeleteImageResponse message
        """
        try:
            # Extract image ID from request
            image_id = request.image_id
            
            print(f"==> DeleteImage RPC called: ID={image_id}")
            
            # Delete from cache
            success = await self.image_cache.delete_image(image_id)
            
            print(f"<== DeleteImage RPC completed: ID={image_id}, success={success}")
            
            # Return response
            return DeleteImageResponse(success=success)
            
        except Exception as e:
            import traceback
            stack_trace = traceback.format_exc()
            print(f"Error deleting image: {e}\n{stack_trace}")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error deleting image: {e}")
    
    async def SegmentImage(self, request, context):
        """
        Implement the SegmentImage RPC method.

        Args:
            request: The SegmentationRequest message
            context: The gRPC context

        Returns:
            A SegmentationResponse message
        """
        # Determine if using cached image or inline image data
        if request.image_id != 0:
            # Use cached image
            cached_image = await self.image_cache.get_image(request.image_id)
            if cached_image is None:
                await context.abort(
                    grpc.StatusCode.NOT_FOUND,
                    f"Image ID {request.image_id} not found in cache. "
                    "It may have been evicted or expired. Please re-upload the image."
                )
                return
                        image_data, width, height = cached_image
            print(f"Using cached image: ID={request.image_id}, {width}x{height}")
        else:
            # Use inline image data (backward compatible)
            image_data = request.image_data
            width = request.width
            height = request.height
            print(f"Using inline image data: {width}x{height}")

        # Convert coordinates from the request format to a list of tuples
        coordinates = [(point.x, point.y) for point in request.coordinates]

        # Extract labels
        labels = list(request.labels)

        # Extract multimask_output flag
        multimask_output = request.multimask_output

        try:
            # Run the prediction in a separate thread to avoid blocking the event loop
            loop = asyncio.get_event_loop()

            labeled_image, segments = await loop.run_in_executor(None, lambda: self.model.segment_image(
                    image_data=image_data,
                    width=width,
                    height=height,
                    coordinates=coordinates,
                    labels=labels,
                    multimask_output=multimask_output
                )
           )

            # Convert the labeled image to bytes
            labeled_image_bytes = cv2.imencode('.png', labeled_image)[1].tobytes()

            # Create the response
            response = SegmentationResponse(
                labeled_image=labeled_image_bytes,
                width=width,
                height=height
            )

            # Add segment results to the response
            for i, segment in enumerate(segments):
                # Process the mask from segmentation_service
                if 'mask' in segment:
                    # Get mask as boolean numpy array from segmentation_service
                    mask_bool = segment['mask']

                    # Clean up the mask - keep only largest connected region and fill small holes
                    mask_bool = SegmentationModel.cleanup_mask(mask_bool)
                    
                    # Recalculate bounds for the cleaned mask
                    x, y, width, height = SegmentationModel.get_mask_bounds(mask_bool)
                    
                    # Crop the mask to the bounding box before encoding
                    if width > 0 and height > 0:
                        cropped_mask = mask_bool[y:y+height, x:x+width]
                    else:
                        cropped_mask = np.zeros((0, 0), dtype=np.bool_)
                    
                    # Encode as PNG for compression and to embed dimensions in the image format
                    # Client will decode PNG to extract width, height, and mask data
                    mask_bytes = cv2.imencode('.png', cropped_mask.astype(np.uint8) * 255)[1].tobytes()
                    
                    # Extract polygons from the cleaned mask
                    polygons = self.model.mask_to_polygons(mask_bool)
                else:
                    # Fallback if mask is missing
                    mask_bytes = b''
                    x, y, width, height = segment.get('x', 0), segment.get('y', 0), segment.get('width', 0), segment.get('height', 0)
                    polygons = []
                
                # Create the segment result with PNG-encoded mask and position
                segment_result = SegmentResult(
                    index=segment['index'],
                    score=segment['score'],
                    mask=mask_bytes,
                    X=x,
                    Y=y
                )

                # Add polygons to the segment result
                for polygon in polygons:
                    poly = Polygon()
                    for point in polygon:
                        poly.points.append(Point(x=int(point[0]), y=int(point[1])))
                    segment_result.polygons.append(poly)

                response.segments.append(segment_result)

            return response

        except (OSError, IOError) as e:
            # Handle image data errors (e.g., truncated images, corrupted data)
            error_msg = str(e).lower()
            if any(keyword in error_msg for keyword in ['truncated', 'corrupted', 'invalid', 'cannot identify image file']):
                print(f"Image data error detected: {e}")
                
                # If we're using a cached image, remove it from the cache
                if request.image_id != 0:
                    print(f"Removing corrupted image from cache: ID={request.image_id}")
                    await self.image_cache.delete_image(request.image_id)
                
                await context.abort(
                    grpc.StatusCode.INVALID_ARGUMENT,
                    f"Image data is corrupted or truncated: {e}. "
                    "If using cached image, it has been removed from cache. Please re-upload the image."
                )
            else:
                # Re-raise other OSError/IOError exceptions
                raise
        except Exception as e:
            # Log the error and return an error status
            import traceback
            stack_trace = traceback.format_exc()
            print(f"Error processing request: {e}\n{stack_trace}")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error processing request: {e}")


async def serve(port=50051, max_workers=10):
    """
    Start the gRPC server.

    Args:
        port: The port to listen on
        max_workers: The maximum number of worker threads
    """
    # Create a server with the specified number of workers
    server = grpc.aio.server(
        futures.ThreadPoolExecutor(max_workers=max_workers),
        options=[
            ('grpc.max_send_message_length',  64 * 1024 * 1024),  # 64 MB
            ('grpc.max_receive_message_length', 64 * 1024 * 1024)  # 64 MB
        ]
    )

    # Add the servicer to the server
    add_SegmentationServiceServicer_to_server(
        SegmentationServicer(), server
    )

    # Add a port for the server to listen on
    server_address = f'[::]:{port}'
    server.add_insecure_port(server_address)

    # Start the server
    await server.start()
    print(f"Server started, listening on {server_address}")

    # Keep the server running until it is terminated
    await server.wait_for_termination()


if __name__ == '__main__':
    # Run the server
    asyncio.run(serve())

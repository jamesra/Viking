"""
Segmentation Service gRPC Server

This module implements the gRPC server for the segmentation service.
It uses grpc.aio for asynchronous operation and connects the gRPC interface
to the SegmentationModel implementation.
"""

import asyncio
import threading
import time
from concurrent import futures
from typing import List, Optional, Sequence, Tuple, Any

import cv2
import grpc
import numpy as np
from grpc.aio import ServicerContext
from numpy.typing import NDArray

# Import the generated gRPC code from the segmentation_grpc package
from segmentation_grpc.segmentation_grpc import (
    SegmentationRequest,
    SegmentationResponse,
    MultiSegmentationRequest,
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
from segmentation_server.segmentation_service import SegmentInfo, SegmentationModel
from segmentation_server.image_cache import ImageCache


class SegmentationServicer(SegmentationServiceServicer):
    """
    Implementation of the SegmentationService gRPC service.

    This class handles gRPC requests for image segmentation, converting between
    the gRPC message format and the format expected by the SegmentationModel.
    
    Architecture:
        - Cached images: Each uploaded image gets its own SAM2ImagePredictor instance
          created at upload time. This eliminates set_image() calls during segmentation,
          providing major performance improvements for frequently accessed images.
        - Inline images: Backward compatibility mode that uses a shared predictor.
          Less efficient due to set_image() call per request, but works without upload.
    
    Thread Safety:
        - Each cached predictor has its own threading.Lock to handle concurrent requests
          for the same image (rare but possible scenario)
        - Predictor operations run in executor threads, so threading.Lock (not asyncio.Lock)
          is required for thread-safe access
        - The shared predictor (for inline images) is NOT thread-safe - concurrent inline
          requests may interfere with each other. This is acceptable since inline mode
          is primarily for backward compatibility.
    
    Performance:
        - Cached images: Fastest path - predictor is pre-initialized, only predict() is called
        - Inline images: Slower - requires set_image() call which processes the entire image
        - Response building: Mask cleanup, polygon extraction, and PNG encoding happen
          synchronously but are CPU-bound operations suitable for executor threads
    """

    def __init__(
        self,
        cache_max_memory_bytes: int = 1073741824,  # 1 GB
        cache_ttl_seconds: int = 300,  # 5 minutes
        inference_executor: Optional[Any] = None,
    ) -> None:
        """
        Initialize the servicer with a SegmentationModel and ImageCache.
        
        Args:
            cache_max_memory_bytes: Maximum memory for image cache (default: 1 GB)
            cache_ttl_seconds: Time-to-live for cached images (default: 5 minutes)
            inference_executor: Optional executor for model inference operations
        """
        self.model = SegmentationModel()
        self.inference_executor = inference_executor
        self.image_cache = ImageCache(
            max_memory_bytes=cache_max_memory_bytes,
            ttl_seconds=cache_ttl_seconds,
            create_predictor_func=self.model.create_predictor,
            prepare_image_func=self.model.prepare_image_for_sam2
        )
    
    def _build_segmentation_response(
        self,
        labeled_image: NDArray[np.uint16],
        segments: List[SegmentInfo],
        width: int,
        height: int
    ) -> SegmentationResponse:
        """
        Build a SegmentationResponse from segmentation results.
        
        This method handles mask cleanup, polygon extraction, and encoding for all segments.
        It's extracted to avoid code duplication across SegmentImage and MultiSegmentImage methods.
        
        Args:
            labeled_image: Labeled image array where each pixel value is a segment index
            segments: List of segment information dictionaries
            width: Image width
            height: Image height
            
        Returns:
            SegmentationResponse message with encoded masks and polygons
        """
        # Encode labeled image as PNG
        labeled_image_bytes = cv2.imencode('.png', labeled_image)[1].tobytes()
        
        # Create the response
        response = SegmentationResponse(
            labeled_image=labeled_image_bytes,
            width=width,
            height=height
        )
        
        # Process each segment
        for segment in segments:
            # Process the mask from segmentation_service
            if 'mask' in segment:
                # Get mask as boolean numpy array from segmentation_service
                mask_bool: NDArray[np.bool_] = segment['mask']
                
                # Clean up the mask - keep only largest connected region and fill small holes
                mask_bool = SegmentationModel.cleanup_mask(mask_bool)
                
                # Recalculate bounds for the cleaned mask
                x, y, mask_width, mask_height = SegmentationModel.get_mask_bounds(mask_bool)
                
                # Crop the mask to the bounding box before encoding
                if mask_width > 0 and mask_height > 0:
                    cropped_mask = mask_bool[y:y+mask_height, x:x+mask_width]
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
                x, y, mask_width, mask_height = segment.get('x', 0), segment.get('y', 0), segment.get('width', 0), segment.get('height', 0)
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
    
    async def _abort_if_image_not_found(
        self,
        cached_result: Optional[Tuple],
        image_id: int,
        context: ServicerContext
    ) -> bool:
        """
        Check if cached result is None and abort with NOT_FOUND if so.
        
        Args:
            cached_result: Result from image_cache.get_image() or None
            image_id: The image ID for error message
            context: The gRPC context
            
        Returns:
            True if should abort (cached_result is None), False otherwise
        """
        if cached_result is None:
            await context.abort(
                grpc.StatusCode.NOT_FOUND,
                f"Image ID {image_id} not found in cache. "
                "It may have been evicted or expired. Please re-upload the image."
            )
            return True
        return False
    
    async def _abort_if_predictor_unavailable(
        self,
        predictor: Optional[Any],
        image_id: int,
        context: ServicerContext
    ) -> bool:
        """
        Check if predictor is None and abort with UNAVAILABLE if so.
        
        Args:
            predictor: The predictor instance or None
            image_id: The image ID for error message
            context: The gRPC context
            
        Returns:
            True if should abort (predictor is None), False otherwise
        """
        if predictor is None:
            await context.abort(
                grpc.StatusCode.UNAVAILABLE,
                f"Predictor for image ID {image_id} is not yet ready or failed to create. "
                "Please wait a moment and try again, or re-upload the image."
            )
            return True
        return False
    
    async def _validate_coordinates(
        self,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        context: ServicerContext,
        empty_message: str = "No coordinates provided. At least one coordinate point is required."
    ) -> bool:
        """
        Validate coordinates and labels, aborting with INVALID_ARGUMENT if invalid.
        
        Args:
            coordinates: List of coordinate tuples
            labels: List of labels
            context: The gRPC context
            empty_message: Custom message for empty coordinates case
            
        Returns:
            True if should abort (validation failed), False otherwise
        """
        if len(coordinates) == 0:
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                empty_message
            )
            return True
        
        if len(coordinates) != len(labels):
            await context.abort(
                grpc.StatusCode.INVALID_ARGUMENT,
                f"Coordinates and labels length mismatch: {len(coordinates)} coordinates but {len(labels)} labels. "
                "Each coordinate must have a corresponding label."
            )
            return True
        
        return False
    
    def _extract_coordinates_and_labels_from_segment_request(
        self,
        request: SegmentationRequest
    ) -> Tuple[List[Tuple[int, int]], List[int]]:
        """
        Extract coordinates and labels from a SegmentationRequest.
        
        Args:
            request: The SegmentationRequest message
            
        Returns:
            Tuple of (coordinates, labels) lists
        """
        coordinates: List[Tuple[int, int]] = [(point.x, point.y) for point in request.coordinates]
        labels: List[int] = list(request.labels)
        return coordinates, labels
    
    def _extract_coordinates_and_labels_from_multi_request(
        self,
        request: MultiSegmentationRequest
    ) -> Tuple[List[Tuple[int, int]], List[int]]:
        """
        Extract coordinates and labels from a MultiSegmentationRequest.
        
        Points with id=0 are treated as background (label=0), others as foreground (label=1).
        
        Args:
            request: The MultiSegmentationRequest message
            
        Returns:
            Tuple of (coordinates, labels) lists
        """
        coordinates: List[Tuple[int, int]] = []
        labels: List[int] = []
        
        for point_id, point in request.foreground_points.items():
            coordinates.append((point.x, point.y))
            # id=0 -> label=0 (background), id!=0 -> label=1 (foreground)
            labels.append(0 if point_id == 0 else 1)
        
        return coordinates, labels
    
    def _segment_with_locked_predictor(
        self,
        predictor: Any,
        predictor_lock: threading.Lock,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool
    ) -> Tuple[NDArray[np.uint16], List[SegmentInfo]]:
        """
        Segment image using a locked predictor (for executor thread).
        
        This method acquires the predictor lock, performs segmentation, and releases the lock.
        Must be called from an executor thread (not async).
        
        Args:
            predictor: The SAM2ImagePredictor instance
            predictor_lock: Threading lock for the predictor
            coordinates: List of (x, y) coordinates to use as prompts
            labels: List of labels for each coordinate
            multimask_output: Whether to output multiple masks per point
            
        Returns:
            Tuple of (labeled_image, segments)
        """
        predictor_lock.acquire()
        try:
            # Use cached predictor (already has image set)
            return self.model.segment_image_with_predictor(
                predictor=predictor,
                coordinates=coordinates,
                labels=labels,
                multimask_output=multimask_output,
            )
        finally:
            predictor_lock.release()
    
    async def _handle_cached_image_segmentation(
        self,
        image_id: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        context: ServicerContext
    ) -> Optional[SegmentationResponse]:
        """
        Handle segmentation for a cached image with pre-initialized predictor.
        
        Args:
            image_id: The cached image ID
            coordinates: List of (x, y) coordinates to use as prompts
            labels: List of labels for each coordinate
            multimask_output: Whether to output multiple masks per point
            context: The gRPC context
            
        Returns:
            SegmentationResponse if successful, None if aborted due to error
        """
        # Get cached image and predictor
        cached_result = await self.image_cache.get_image(image_id)
        if await self._abort_if_image_not_found(cached_result, image_id, context):
            return SegmentationResponse()
        
        image_data, width, height, predictor, predictor_lock = cached_result
        
        if await self._abort_if_predictor_unavailable(predictor, image_id, context):
            return SegmentationResponse()
        
        start_time = time.perf_counter()
        print(f"Using cached image with predictor: ID={image_id}, {width}x{height}", end="")
        
        # Validate inputs (use custom message for MultiSegmentImage)
        if await self._validate_coordinates(
            coordinates, 
            labels, 
            context,
            empty_message="No foreground_points provided. At least one point is required."
        ):
            elapsed_time = time.perf_counter() - start_time
            print(f" (validation failed in {elapsed_time:.3f}s)")
            return SegmentationResponse()
        
        try:
            # Run the prediction in a separate thread with predictor lock
            labeled_image, segments = await asyncio.get_running_loop().run_in_executor(
                self.inference_executor,
                self._segment_with_locked_predictor,
                predictor,
                predictor_lock,
                coordinates,
                labels,
                multimask_output,
            )
        except Exception as e:
            import traceback
            stack_trace = traceback.format_exc()
            print(f"Predictor error for image ID {image_id}: {e}\n{stack_trace}")
            # Remove image from cache so it can be re-uploaded
            await self.image_cache.delete_image(image_id)
            elapsed_time = time.perf_counter() - start_time
            print(f" (failed in {elapsed_time:.3f}s)")
            await context.abort(
                grpc.StatusCode.INTERNAL,
                f"Error processing segmentation request: {e}. Image has been removed from cache. Please re-upload the image."
            )
            return SegmentationResponse()
        
        # Build and return response
        elapsed_time = time.perf_counter() - start_time
        print(f" (completed in {elapsed_time:.3f}s)")
        return self._build_segmentation_response(labeled_image, segments, width, height)
    
    async def _handle_inline_image_segmentation(
        self,
        image_data: bytes,
        width: int,
        height: int,
        coordinates: List[Tuple[int, int]],
        labels: List[int],
        multimask_output: bool,
        context: ServicerContext
    ) -> Optional[SegmentationResponse]:
        """
        Handle segmentation for an inline image using shared predictor.
        
        Args:
            image_data: The image data as bytes
            width: Image width in pixels
            height: Image height in pixels
            coordinates: List of (x, y) coordinates to use as prompts
            labels: List of labels for each coordinate
            multimask_output: Whether to output multiple masks per point
            context: The gRPC context
            
        Returns:
            SegmentationResponse if successful, None if aborted due to error
        """
        start_time = time.perf_counter()
        print(f"Using inline image data: {width}x{height}", end="")
        
        # Validate inputs (use custom message for MultiSegmentImage)
        if await self._validate_coordinates(
            coordinates, 
            labels, 
            context,
            empty_message="No foreground_points provided. At least one point is required."
        ):
            return SegmentationResponse()
        
        try:
            # Run the prediction in a separate thread to avoid blocking the event loop
            labeled_image, segments = await asyncio.get_running_loop().run_in_executor(
                self.inference_executor,
                lambda: self.model.segment_image(
                    image_data=image_data,
                    width=width,
                    height=height,
                    coordinates=coordinates,
                    labels=labels,
                    multimask_output=multimask_output,
                ),
            )
        except Exception as e:
            import traceback
            stack_trace = traceback.format_exc()
            elapsed_time = time.perf_counter() - start_time
            print(f" (failed in {elapsed_time:.3f}s)")
            print(f"Error during segmentation: {e}\n{stack_trace}")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error processing request: {e}")
            return SegmentationResponse()
        
        # Build and return response
        elapsed_time = time.perf_counter() - start_time
        print(f" (completed in {elapsed_time:.3f}s)")
        return self._build_segmentation_response(labeled_image, segments, width, height)

    async def UploadImage(
        self,
        request: UploadImageRequest,
        context: ServicerContext,
    ) -> UploadImageResponse:
        """
        Upload an image to the cache and return a unique ID.
        
        Args:
            request: The UploadImageRequest message
            context: The gRPC context
            
        Returns:
            An UploadImageResponse message containing the image ID
        """
        start_time = time.perf_counter()
        try:
            # Extract image data from request
            image_data: bytes = request.image_data
            width: int = request.width
            height: int = request.height
            
            print(f"==> UploadImage RPC called: {width}x{height}, {len(image_data)} bytes", end="")
            
            # Upload to cache (predictor will be created in executor)
            image_id: int = await self.image_cache.upload_image(
                image_data, width, height, executor=self.inference_executor
            )
            
            elapsed_time = time.perf_counter() - start_time
            print(f" <== completed: assigned ID={image_id} (took {elapsed_time:.3f}s)")
            
            # Return response with image ID
            return UploadImageResponse(image_id=image_id)
            
        except Exception as e:
            import traceback
            stack_trace = traceback.format_exc()
            elapsed_time = time.perf_counter() - start_time
            print(f" (failed in {elapsed_time:.3f}s)")
            print(f"Error uploading image: {e}\n{stack_trace}")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error uploading image: {e}")
    
    async def DeleteImage(
        self,
        request: DeleteImageRequest,
        context: ServicerContext,
    ) -> DeleteImageResponse:
        """
        Delete an image from the cache.
        
        Args:
            request: The DeleteImageRequest message
            context: The gRPC context
            
        Returns:
            A DeleteImageResponse message
        """
        start_time = time.perf_counter()
        try:
            # Extract image ID from request
            image_id: int = request.image_id
            
            print(f"==> DeleteImage RPC called: ID={image_id}", end="")
            
            # Delete from cache
            success: bool = await self.image_cache.delete_image(image_id)
            
            elapsed_time = time.perf_counter() - start_time
            print(f" <== completed: ID={image_id}, success={success} (took {elapsed_time:.3f}s)")
            
            # Return response
            return DeleteImageResponse(success=success)
            
        except Exception as e:
            import traceback
            stack_trace = traceback.format_exc()
            elapsed_time = time.perf_counter() - start_time
            print(f" (failed in {elapsed_time:.3f}s)")
            print(f"Error deleting image: {e}\n{stack_trace}")
            await context.abort(grpc.StatusCode.INTERNAL, f"Error deleting image: {e}")
    
    async def SegmentImage(
        self,
        request: SegmentationRequest,
        context: ServicerContext,
    ) -> SegmentationResponse:
        """
        Implement the SegmentImage RPC method.
        
        This method supports two modes:
        1. Cached image (image_id != 0): Uses pre-initialized predictor for best performance
        2. Inline image (image_id == 0): Uses shared predictor, requires set_image() call

        Args:
            request: The SegmentationRequest message containing coordinates, labels, and image
            context: The gRPC context

        Returns:
            A SegmentationResponse message with labeled image and segment masks/polygons
            
        Performance:
            - Cached path: O(1) predictor lookup + O(n) where n is image size for predict()
            - Inline path: O(n) for set_image() + O(n) for predict()
            - Response building: O(m * k) where m is number of segments, k is mask size
            
        Error Handling:
            - Invalid inputs (empty coordinates, length mismatch) return INVALID_ARGUMENT
            - Missing or expired images return NOT_FOUND
            - Predictor errors result in image removal from cache and INTERNAL error
            - Image data errors result in image removal and INVALID_ARGUMENT
        """
        # Extract coordinates and labels from request
        coordinates, labels = self._extract_coordinates_and_labels_from_segment_request(request)
        
        # Extract multimask_output flag
        multimask_output = request.multimask_output
        
        # Determine if using cached image or inline image data
        if request.image_id != 0:
            # Use cached image with predictor
            response = await self._handle_cached_image_segmentation(
                request.image_id,
                coordinates,
                labels,
                multimask_output,
                context
            )
            return response if response is not None else SegmentationResponse()
        else:
            # Use inline image data (backward compatible - uses shared predictor)
            response = await self._handle_inline_image_segmentation(
                request.image_data,
                request.width,
                request.height,
                coordinates,
                labels,
                multimask_output,
                context
            )
            return response if response is not None else SegmentationResponse()
    
    async def MultiSegmentImage(
        self,
        request: MultiSegmentationRequest,
        context: ServicerContext,
    ) -> SegmentationResponse:
        """
        Implement the MultiSegmentImage RPC method.
        
        Similar to SegmentImage but accepts a dictionary mapping point IDs to coordinates.
        Points with id=0 are treated as background (label=0), others as foreground (label=1).
        
        This method supports two modes:
        1. Cached image (image_id != 0): Uses pre-initialized predictor for best performance
        2. Inline image (image_id == 0): Uses shared predictor, requires set_image() call

        Args:
            request: The MultiSegmentationRequest message with foreground_points map
            context: The gRPC context

        Returns:
            A SegmentationResponse message with labeled image and segment masks/polygons
            
        Performance:
            - Same as SegmentImage - cached path is faster than inline path
            - Point ID to label conversion is O(p) where p is number of points
            
        Error Handling:
            - Empty foreground_points returns INVALID_ARGUMENT
            - Same error handling as SegmentImage for cache/predictor issues
        """
        # Extract coordinates and labels from request
        coordinates, labels = self._extract_coordinates_and_labels_from_multi_request(request)
        
        # Extract multimask_output flag
        multimask_output = request.multimask_output
        
        # Determine if using cached image or inline image data
        if request.image_id != 0:
            # Use cached image with predictor
            response = await self._handle_cached_image_segmentation(
                request.image_id,
                coordinates,
                labels,
                multimask_output,
                context
            )
            return response if response is not None else SegmentationResponse()
        else:
            # Use inline image data (backward compatible - uses shared predictor)
            response = await self._handle_inline_image_segmentation(
                request.image_data,
                request.width,
                request.height,
                coordinates,
                labels,
                multimask_output,
                context
            )
            return response if response is not None else SegmentationResponse()


async def serve(port: int = 50051, max_workers: int = 10, inference_workers: Optional[int] = None) -> None:
    """
    Start the gRPC server.

    Args:
        port: The port to listen on
        max_workers: The maximum number of worker threads for gRPC server
        inference_workers: The number of worker threads for model inference (default: same as max_workers)
    """
    # Create a dedicated executor for model inference operations
    if inference_workers is None:
        inference_workers = max_workers
    
    inference_executor = futures.ThreadPoolExecutor(max_workers=inference_workers)
    
    # Create a server with the specified number of workers
    server = grpc.aio.server(
        futures.ThreadPoolExecutor(max_workers=max_workers),
        options=[
            ('grpc.max_send_message_length',  64 * 1024 * 1024),  # 64 MB
            ('grpc.max_receive_message_length', 64 * 1024 * 1024)  # 64 MB
        ]
    )

    # Add the servicer to the server with inference executor
    add_SegmentationServiceServicer_to_server(
        SegmentationServicer(inference_executor=inference_executor), server
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

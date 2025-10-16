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
from segmentation_grpc import (
    SegmentationRequest,
    SegmentationResponse,
    Point,
    Polygon,
    SegmentResult,
    SegmentationServiceServicer,
    add_SegmentationServiceServicer_to_server
)

# Import the segmentation model
from segmentation_server.segmentation_service import SegmentationModel


class SegmentationServicer(SegmentationServiceServicer):
    """
    Implementation of the SegmentationService gRPC service.

    This class handles gRPC requests for image segmentation, converting between
    the gRPC message format and the format expected by the SegmentationModel.
    """

    def __init__(self):
        """Initialize the servicer with a SegmentationModel."""
        self.model = SegmentationModel()

    async def SegmentImage(self, request, context):
        """
        Implement the SegmentImage RPC method.

        Args:
            request: The SegmentationRequest message
            context: The gRPC context

        Returns:
            A SegmentationResponse message
        """
        # Extract data from the request
        image_data = request.image_data
        width = request.width
        height = request.height

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
                # Create the segment result
                segment_result = SegmentResult(
                    index=segment['index'],
                    score=segment['score'],
                    mask=segment['mask']
                )

                # Extract polygons from the mask
                if 'mask' in segment:
                    # Decode the mask from bytes to numpy array
                    mask_np = cv2.imdecode(np.frombuffer(segment['mask'], np.uint8), cv2.IMREAD_GRAYSCALE)
                    mask_bool = mask_np > 0

                    # Extract polygons from the mask
                    polygons = self.model.mask_to_polygons(mask_bool)

                    # Add polygons to the segment result
                    for polygon in polygons:
                        poly = Polygon()
                        for point in polygon:
                            poly.points.append(Point(x=int(point[0]), y=int(point[1])))
                        segment_result.polygons.append(poly)

                response.segments.append(segment_result)

            return response

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

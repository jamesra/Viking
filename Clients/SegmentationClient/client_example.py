"""
Segmentation Service Client Example

This module provides an example of how to use the segmentation service from a client.
It demonstrates how to create a request, call the service, and process the response.
"""

import asyncio
import argparse
import grpc
import numpy as np
from PIL import Image
import io
import matplotlib.pyplot as plt
import os
import sys
import cv2

from typing import Sequence, NamedTuple, Tuple

from numpy.typing import NDArray

# Import the generated gRPC code from the segmentation_grpc package
import segmentation_grpc
from segmentation_grpc import (
    SegmentationRequest,
    SegmentationResponse,
    Point,
    Polygon,
    SegmentResult,
    SegmentationServiceStub
)

np.random.seed(16)

class Segment(NamedTuple):
    """
    A named tuple to represent a segment in the segmentation response.

    Attributes:
        index: The index of the segment
        score: The score of the segment
        mask: The mask of the segment as a PIL Image
        polygons: List of polygons representing the contours of the segment
    """
    index: int
    score: float
    mask: NDArray[bool]
    polygons: list[list[tuple[int, int]]] = []


def colorize_labels(labeled_image: NDArray) -> NDArray:
    # Convert the labeled image to a numpy array
    labeled_array = np.array(labeled_image)

    # Create a random colormap for the labeled image
    unique_indices = np.unique(labeled_array)
    num_segments = len(unique_indices)

    # Generate random colors for each segment (excluding background which is usually 0)
      # For reproducibility, remove this if you want truly random colors each time
    random_colors = np.random.randint(0, 256, size=(num_segments, 3), dtype=np.uint8)
    random_colors[0] = [0, 0, 0]  # Set background color to black

    # Create a colormap from the labeled image
    colored_image = np.zeros((labeled_array.shape[0], labeled_array.shape[1], 3), dtype=np.uint8)

    # Map each label to a color
    for i, label in enumerate(unique_indices):
        colored_image[labeled_array == label] = random_colors[i]

    return colored_image


def show_labeled_image(original_image, labeled_image, segments, coordinates=None, labels=None):
    """
    Display the labeled image with only the highest scoring segment.

    Args:
        original_image: The original image as a numpy array
        labeled_image: The labeled image as a PIL Image
        segments: List of segment information
        coordinates: List of (x, y) coordinates used as prompts
        labels: List of labels for each coordinate (1 for foreground, 0 for background)
    """
    # Sort segments by score in descending order
    sorted_segments = sorted(segments, key=lambda x: x.score if hasattr(x, 'score') else x['score'], reverse=True)
    
    # Only keep the segment with the highest score
    best_segment = sorted_segments[0] if sorted_segments else None
    
    if best_segment is None:
        print("No segments found.")
        return
    
    # Convert the labeled image to a numpy array
    labeled_array = np.array(labeled_image)

    colorized_labels = colorize_labels(labeled_array)

    # If original_image is grayscale, convert to BGR for overlay
    original_image_3ch = original_image
    if len(original_image.shape) == 2:
        original_image_3ch = cv2.cvtColor(original_image, cv2.COLOR_GRAY2BGR)

    # Create the blended overlay image
    overlay = cv2.addWeighted(
        original_image_3ch,
        1.0,
        colorized_labels,
        .5,
        0
    )

    # Create a figure with just 2 subplots (overlay and best segment)
    fig, axes = plt.subplots(1, 2, figsize=(10, 5))

    # Show the labeled image
    axes[0].imshow(overlay)
    axes[0].set_title("Labeled Image (Best Segment)")

    # Generate random colors for polygons

    # Draw polygons for the best segment on the original image
    if hasattr(best_segment, 'polygons'):
        for polygon in best_segment.polygons:
            # Convert polygon points to numpy arrays for plotting
            polygon_array = np.array(polygon)
            if len(polygon_array) > 0:
                # Generate a random color for this polygon
                random_color = np.random.rand(3,)  # RGB values between 0 and 1
                # Draw the polygon with the random color
                axes[0].plot(polygon_array[:, 0], polygon_array[:, 1], color=random_color, linewidth=2)

    # Draw the input points on the overlay image
    if coordinates is not None and labels is not None:
        for (x, y), label in zip(coordinates, labels):
            if label == 1:  # Foreground point - green circle
                axes[0].plot(x, y, 'go', markersize=10, markerfacecolor='none')  # Green hollow circle
            else:  # Background point - red cross
                axes[0].plot(x, y, 'rx', markersize=10)  # 'rx' means red cross

    axes[0].axis('off')

    # Show the best segment
    # Get the mask depending on the structure of the segment object
    mask_array = None
    segment_score = 0
    segment_index = 0
    
    if hasattr(best_segment, 'mask'):
        mask_array = best_segment.mask
        segment_score = best_segment.score
        segment_index = best_segment.index
    else:  # Dictionary format
        mask_array = np.array(best_segment['mask'])
        segment_score = best_segment['score']
        segment_index = best_segment['index']

    if mask_array is not None:
        # Show the mask
        axes[1].imshow(mask_array, cmap='gray')
        axes[1].set_title(f"Best Segment (Score: {segment_score:.3f})")

        # Draw polygons on the mask if they exist
        if hasattr(best_segment, 'polygons'):
            np.random.seed(42)  # Same seed for consistency
            for polygon in best_segment.polygons:
                # Convert polygon points to numpy arrays for plotting
                polygon_array = np.array(polygon)
                if len(polygon_array) > 0:
                    # Generate a random color for this polygon
                    random_color = np.random.rand(3,)  # RGB values between 0 and 1
                    # Draw the polygon with the random color
                    axes[1].plot(polygon_array[:, 0], polygon_array[:, 1], color=random_color, linewidth=2)

        # Draw the input points on the segment image if provided
        if coordinates is not None and labels is not None:
            for (x, y), label in zip(coordinates, labels):
                if label == 1:  # Foreground point - green circle
                    axes[1].plot(x, y, 'go', markersize=8, markerfacecolor='none')  # Green hollow circle
                else:  # Background point - red cross
                    axes[1].plot(x, y, 'rx', markersize=8)  # 'rx' means red cross

        axes[1].axis('off')

    plt.tight_layout()
    plt.show()


async def segment_image(server_address: str, image_path: str, coordinates: tuple[int, int], labels: Sequence[bool], multimask_output: bool=True) -> tuple[NDArray, Sequence[Segment]]:
    """
    Segment an image using the segmentation service.

    Args:
        server_address: The address of the segmentation service (host:port)
        image_path: Path to the image file
        coordinates: List of (x, y) coordinates to use as prompts
        labels: List of labels for each coordinate (1 for foreground, 0 for background)
        multimask_output: Whether to output multiple masks per point

    Returns:
        A tuple containing:
        - labeled_image: The labeled image as a PIL Image
        - segments: List of segment information
    """
    # Load the image
    image = Image.open(image_path)

    # Convert to grayscale if needed
    if image.mode != 'L':
        grayscale_image = image.convert('L')
    else:
        grayscale_image = image

    # Convert the image to bytes
    buffer = io.BytesIO()
    grayscale_image.save(buffer, format='PNG')
    image_data = buffer.getvalue()

    # Create the request
    request = SegmentationRequest(
        image_data=image_data,
        width=grayscale_image.width,
        height=grayscale_image.height,
        multimask_output=multimask_output
    )

    # Add coordinates and labels
    for x, y in coordinates:
        point = Point(x=x, y=y)
        request.coordinates.append(point)

    for label in labels:
        request.labels.append(label)

    # Create a gRPC channel
    async with grpc.aio.insecure_channel(server_address) as channel:
        # Create a stub
        stub = SegmentationServiceStub(channel)

        try:
            # Call the service
            response = await stub.SegmentImage(request) # type: SegmentationResponse

            # Process the response
            labeled_image = Image.open(io.BytesIO(response.labeled_image))

            # Process segments
            segments = []
            for segment in response.segments:
                # Extract polygons from the response
                polygons = []
                for polygon in segment.polygons:
                    points = [(point.x, point.y) for point in polygon.points]
                    polygons.append(points)

                segments.append(Segment(
                    segment.index,
                    segment.score,
                    np.array(Image.open(io.BytesIO(segment.mask)), dtype=np.uint8) if segment.mask else None,
                    polygons
                ))

            return labeled_image, segments

        except grpc.RpcError as e:
            print(f"RPC error: {e.details()}")
            return None, None

def pair_of_numbers(value: str):
    try:
        x, y = map(int, value.split(','))
        return (x, y)
    except ValueError:
        raise argparse.ArgumentTypeError('Value must be in format "x,y" where x and y are integers')



async def main():
    """Main entry point for the client example."""
    # Parse command-line arguments
    parser = argparse.ArgumentParser(description='Segment an image using the segmentation service.')
    parser.add_argument('--server', type=str, default='localhost:50051',
                        help='The address of the segmentation service (default: localhost:50051)')
    parser.add_argument('--image', type=str, required=True,
                        help='Path to the image file')
    parser.add_argument('--coordinates', type=pair_of_numbers, nargs='+', required=True,
                        help='Coordinates as x1,y1; x2,y2;... (e.g., 100,200 300,400)')
    parser.add_argument('--labels', type=str, required=False,
                        help='Labels as l1,l2,... (e.g., 1,0). 1 indicates the point is in the foreground, 0 in the background.  Defaults to assuming all points are foreground.')
    parser.add_argument('--multimask', action='store_true',
                        help='Output multiple masks per point')
    args = parser.parse_args()

    # Parse coordinates
    coordinates = args.coordinates
    print(f'coordinates: {args.coordinates}')  

    # Parse labels
    if args.labels is None:
        labels = [1] * len(coordinates) + 1
    else:
        labels = list(map(int, args.labels.split(',')))

    # Check that the number of coordinates matches the number of labels
    if len(coordinates) != len(labels):
        print(f"Error: Number of coordinates ({len(coordinates)}) does not match number of labels ({len(labels)})")
        print("Coordinates:")
        for coord in coordinates:
            print(f'\t{coord}')
        print (f"Labels: {(',').join(str(labels))}")
        return

    # Segment the image
    labeled_image, segments = await segment_image(
        args.server,
        args.image,
        coordinates,
        labels,
        args.multimask
    )

    if labeled_image is not None and segments is not None:
        # Show the results
        image = Image.open(args.image)
        image = image.convert('RGB')
        image_array = np.array(image)
        show_labeled_image(image_array, segments[0].mask, segments, coordinates, labels)


if __name__ == '__main__':
    # Run the main function
    asyncio.run(main())
"""
Segmentation Service Client Example

Segments a picture from point prompts and plots the masks. The server works on a fixed 1024 x 1024 tile grid, so the
picture is laid on a grid of its own and its tiles are uploaded as the server asks for them
(``segmentation_server.picture_segmentation`` does the bookkeeping; this file is the gRPC side).
"""

import argparse
import asyncio
from typing import Optional, Sequence

import grpc
import matplotlib.pyplot as plt
import numpy as np
from numpy.typing import NDArray
from PIL import Image

from segmentation_grpc import SegmentationServiceStub, ServerStatusRequest
from segmentation_server.picture_segmentation import (
    PictureClick,
    PictureGrid,
    PictureSegment,
    segment_picture,
)

np.random.seed(16)


def show_segments(
    image: NDArray,
    segments: Sequence[PictureSegment],
    coordinates: Optional[Sequence[tuple[int, int]]] = None,
    labels: Optional[Sequence[int]] = None,
) -> None:
    """Plot the picture with each island's mask in its own colour, and the clicks."""
    overlay = np.zeros(image.shape[:2] + (4,), dtype=np.float32)
    for segment in segments:
        colour = np.append(np.random.rand(3), 0.5)
        height, width = segment.mask.shape
        region = overlay[segment.y:segment.y + height, segment.x:segment.x + width]
        region[segment.mask] = colour

    _figure, axes = plt.subplots(1, 1, figsize=(10, 10))
    axes.imshow(image)
    axes.imshow(overlay)
    if coordinates:
        for (x, y), label in zip(coordinates, labels or [1] * len(coordinates)):
            axes.plot(x, y, 'g*' if label == 1 else 'r*', markersize=15)
    title = ", ".join(f"island {segment.island}: {segment.score:.3f}" for segment in segments) or "no mask"
    axes.set_title(title)
    axes.axis('off')
    plt.tight_layout()
    plt.show()


async def segment_image(
    server_address: str,
    image_path: str,
    coordinates: Sequence[tuple[int, int]],
    labels: Sequence[int],
    ca_cert: str | None = None,
) -> tuple[Optional[NDArray], Optional[Sequence[PictureSegment]]]:
    """
    Segment an image using the segmentation service.

    Args:
        server_address: The address of the segmentation service (host:port)
        image_path: Path to the image file
        coordinates: List of (x, y) image pixels to use as prompts (Y down, from the top-left corner)
        labels: List of labels for each coordinate (1 for foreground, 0 for background)
        ca_cert: PEM file to trust as the server's root. Needed only for a self-signed
            development certificate; leave None to use the system roots.

    Returns:
        The picture as an RGB array and one mask per island of the prompt, or (None, None) on an RPC error.
    """
    image = np.array(Image.open(image_path).convert('RGB'))
    picture = PictureGrid(image)
    clicks = [PictureClick(x=int(x), y=int(y), label=int(label)) for (x, y), label in zip(coordinates, labels)]

    async with _channel_for(server_address, ca_cert=ca_cert) as channel:
        stub = SegmentationServiceStub(channel)
        try:
            status = await stub.GetServerStatus(ServerStatusRequest())
            model_id = status.model.model_id if status.model.model_id != "unknown" else None
            _response, segments = await segment_picture(
                picture,
                clicks,
                upload=stub.UploadTile,
                converse=stub.SegmentTilesStream,
                model_id=model_id,
            )
            return image, segments
        except grpc.RpcError as e:
            print(f"RPC error: {e.details()}")
            return None, None


def _channel_for(server_address: str, ca_cert: str | None = None):
    """Open a TLS channel. The server has no cleartext listener.

    ``ca_cert`` is a PEM file to trust as the root, for a self-signed development server.
    """
    root_certificates = None
    if ca_cert:
        with open(ca_cert, "rb") as certificate_file:
            root_certificates = certificate_file.read()
    return grpc.aio.secure_channel(
        server_address, grpc.ssl_channel_credentials(root_certificates=root_certificates)
    )


def pair_of_numbers(value: str):
    try:
        x, y = map(int, value.split(','))
        return (x, y)
    except ValueError:
        raise argparse.ArgumentTypeError('Value must be in format "x,y" where x and y are integers')


async def main():
    """Main entry point for the client example."""
    parser = argparse.ArgumentParser(description='Segment an image using the segmentation service.')
    parser.add_argument('--server', type=str, default='localhost:40443',
                        help='The address of the segmentation service (default: localhost:40443). '
                             'The connection is always TLS.')
    parser.add_argument('--image', type=str, required=True,
                        help='Path to the image file')
    parser.add_argument('--coordinates', type=pair_of_numbers, nargs='+', required=True,
                        help='Coordinates as x1,y1; x2,y2;... (e.g., 100,200 300,400)')
    parser.add_argument('--labels', type=str, required=False,
                        help='Labels as l1,l2,... (e.g., 1,0). 1 indicates the point is in the foreground, 0 in the background.  Defaults to assuming all points are foreground.')
    parser.add_argument('--ca-cert', type=str, default=None,
                        help='PEM file to trust as the server root, for a self-signed development '
                             'certificate (python -m segmentation_server.dev_cert)')
    args = parser.parse_args()

    coordinates = args.coordinates
    print(f'coordinates: {args.coordinates}')

    if args.labels is None:
        labels = [1] * len(coordinates)
    else:
        labels = list(map(int, args.labels.split(',')))

    if len(coordinates) != len(labels):
        print(f"Error: Number of coordinates ({len(coordinates)}) does not match number of labels ({len(labels)})")
        return

    image, segments = await segment_image(args.server, args.image, coordinates, labels, ca_cert=args.ca_cert)
    if image is not None and segments is not None:
        show_segments(image, segments, coordinates, labels)


if __name__ == '__main__':
    asyncio.run(main())

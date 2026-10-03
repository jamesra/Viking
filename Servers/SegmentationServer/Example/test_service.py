"""
Test Segmentation Service

Smoke test against a real server process. It:
1. Writes a self-signed development certificate (the server only speaks TLS)
2. Starts `python -m segmentation_server` with that certificate
3. Waits until GetServerStatus answers over TLS
4. Runs the client example on a sample image, trusting the certificate
5. Shuts the server down

Needs a GPU machine with torch and SAM2 installed, like the server itself.
"""

import asyncio
import os
import socket
import subprocess
import sys
import tempfile
import time
from pathlib import Path

import grpc
import numpy as np
from PIL import Image

from client_example import _channel_for, segment_image, show_labeled_image
from segmentation_grpc import ServerStatusRequest, SegmentationServiceStub
from segmentation_server.dev_cert import generate_self_signed

STARTUP_TIMEOUT_SECONDS = 300


def _free_port() -> int:
    with socket.socket(socket.AF_INET6, socket.SOCK_STREAM) as probe:
        probe.bind(("::", 0))
        return probe.getsockname()[1]


async def _wait_until_serving(address: str, ca_cert: str, process: subprocess.Popen) -> bool:
    """Poll GetServerStatus until it answers, the process exits, or the timeout passes."""
    deadline = time.monotonic() + STARTUP_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        if process.poll() is not None:
            return False
        try:
            async with _channel_for(address, ca_cert=ca_cert) as channel:
                await SegmentationServiceStub(channel).GetServerStatus(
                    ServerStatusRequest(), timeout=5
                )
            return True
        except grpc.RpcError:
            await asyncio.sleep(2)
    return False


def _find_sample_image() -> str:
    script_dir = Path(__file__).resolve().parent
    local_image = script_dir / "images" / "RodBC3578GJ_Aii2_Z311_X19750_Y33227_W1531_H1124_DS1.png"
    if local_image.is_file():
        return str(local_image)
    example_folder = Path.home() / "SAM2-Docker" / "examples"
    for root, _, files in os.walk(example_folder):
        for file in files:
            if file.lower().endswith(('.png', '.jpg', '.jpeg')):
                return os.path.join(root, file)
    return input("Image path: ")


async def test_service():
    """Start the service over TLS, segment one image, and shut it down."""
    port = _free_port()
    with tempfile.TemporaryDirectory() as folder:
        cert, key = generate_self_signed(Path(folder) / "cert.pem", Path(folder) / "key.pem")
        environment = dict(os.environ, SSL_CERT_PATH=str(cert), SSL_KEY_PATH=str(key))

        print("Starting the service...")
        service_process = subprocess.Popen(
            [sys.executable, '-m', 'segmentation_server',
             '--tls-port', str(port), '--workers', '4', '--no-compile-image-encoder'],
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
        )

        try:
            address = f'localhost:{port}'
            print("Waiting for the service to answer over TLS...")
            if not await _wait_until_serving(address, str(cert), service_process):
                print("The service did not start.")
                return

            sample_image = _find_sample_image()
            print(f"Running the client example with image: {sample_image}")
            labeled_image, segments = await segment_image(
                address,
                sample_image,
                [(500, 375)],
                [1],
                multimask_output=True,
                ca_cert=str(cert),
            )

            if labeled_image is not None and segments is not None:
                print("Segmentation successful!")
                print(f"Found {len(segments)} segments.")
                image_array = np.array(Image.open(sample_image).convert('RGB'))
                show_labeled_image(image_array, labeled_image, segments)
            else:
                print("Segmentation failed.")
        finally:
            print("Shutting down the service...")
            service_process.terminate()
            try:
                output, _ = service_process.communicate(timeout=60)
            except subprocess.TimeoutExpired:
                service_process.kill()
                output, _ = service_process.communicate()
            if output:
                print("Service output:")
                print(output)


if __name__ == '__main__':
    asyncio.run(test_service())

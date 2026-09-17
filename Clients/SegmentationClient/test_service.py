"""
Test Segmentation Service

This script tests the segmentation service by:
1. Generating the gRPC code
2. Starting the service in the background
3. Running the client example with a sample image
4. Shutting down the service
"""

import os
import sys
import asyncio
import subprocess
import signal
import time
import numpy as np
from PIL import Image
from pathlib import Path

# Import the generate_grpc_code function from the segmentation_grpc package
from segmentation_grpc import generate_grpc_code

# Import the client functions from the SegmentationClient package
from SegmentationClient.client_example import segment_image, show_labeled_image


async def test_service():
    """Test the segmentation service."""
    # Generate the gRPC code
    print("Generating gRPC code...")
    if not generate_grpc_code():
        print("Failed to generate gRPC code. Exiting.")
        return

    # Start the service in the background
    print("Starting the service...")
    service_process = subprocess.Popen(
        [sys.executable, '-m', 'SegmentationServer', '--port', '50051', '--workers', '4'],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True
    )

    try:
        # Wait for the service to start
        print("Waiting for the service to start...")
        time.sleep(5)  # Give the service some time to start

        # Find a sample image
        example_folder = os.path.join(Path.home(), 'SAM2-Docker/examples')
        sample_image = os.path.join(example_folder, 'images/RodBC3578GJ_Aii2_Z311_X19750_Y33227_W1531_H1124_DS1.png')

        if not os.path.exists(sample_image):
            print(f"Sample image not found at {sample_image}")
            print("Looking for any image file in the examples directory...")

            # Look for any image file in the examples directory
            for root, _, files in os.walk(example_folder):
                for file in files:
                    if file.lower().endswith(('.png', '.jpg', '.jpeg')):
                        sample_image = os.path.join(root, file)
                        print(f"Found image: {sample_image}")
                        break
                if os.path.exists(sample_image):
                    break

        if not os.path.exists(sample_image):
            print("No sample image found. Please provide an image path.")
            sample_image = input("Image path: ")

        # Define coordinates and labels
        coordinates = [(500, 375)]  # Example coordinates
        labels = [1]  # Example labels (1 for foreground)

        # Run the client example
        print(f"Running the client example with image: {sample_image}")
        labeled_image, segments = await segment_image(
            'localhost:50051',
            sample_image,
            coordinates,
            labels,
            multimask_output=True
        )

        if labeled_image is not None and segments is not None:
            print("Segmentation successful!")
            print(f"Found {len(segments)} segments.")

            # Show the results
            image = Image.open(sample_image)
            image = image.convert('RGB')
            image_array = np.array(image)
            show_labeled_image(image_array, labeled_image, segments)
        else:
            print("Segmentation failed.")

    finally:
        # Shut down the service
        print("Shutting down the service...")
        service_process.send_signal(signal.SIGINT)

        # Wait for the service to shut down
        stdout, stderr = service_process.communicate(timeout=10)

        # Print any output from the service
        if stdout:
            print("Service stdout:")
            print(stdout)

        if stderr:
            print("Service stderr:")
            print(stderr)


if __name__ == '__main__':
    # Run the test
    asyncio.run(test_service())

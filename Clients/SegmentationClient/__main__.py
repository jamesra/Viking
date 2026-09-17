"""
Main entry point for the SegmentationClient package.

This module allows running the package as a Python module:
python -m SegmentationClient
"""

import sys
import asyncio
from SegmentationClient.client_example import main

if __name__ == "__main__":
    # Run the main function from client_example.py
    asyncio.run(main())
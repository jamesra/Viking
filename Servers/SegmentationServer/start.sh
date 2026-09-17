#!/bin/bash
set -e

if [ "$VS_CODE_DEBUG" = "true" ] && [ "$PYCHARM_DEBUG" = "true" ]; then
    echo "Starting with both VS Code (debugpy) and PyCharm debugging..."
    python3 -Xfrozen_modules=off -m debugpy --listen 0.0.0.0:5678 --wait-for-client -m segmentation_server
elif [ "$VS_CODE_DEBUG" = "true" ]; then
    echo "Starting with VS Code debugpy on port 5678..."
    python3 -Xfrozen_modules=off -m debugpy --listen 0.0.0.0:5678 --wait-for-client -m segmentation_server
elif [ "$PYCHARM_DEBUG" = "true" ]; then
    echo "Starting with PyCharm debugging on port 12348..."
    python3 -c "import pydevd_pycharm; pydevd_pycharm.settrace('0.0.0.0', port=12348, stdout_to_server=True, stderr_to_server=True, suspend=False); import segmentation_server.__main__ as main_mod; import asyncio; asyncio.run(main_mod.main())"
else
    echo "Starting normally..."
    python3 -m segmentation_server
fi

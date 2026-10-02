# Segmentation client example

Command-line sample for the segmentation server. It opens an image, sends foreground and background points, and plots the mask with matplotlib.

From this directory, with `segmentation_grpc` and the dependencies in `pyproject.toml` installed:

```bash
python client_example.py --server localhost:40080 --image path/to/image.png --coordinates 100,200 300,400 --labels 1,0
```

The default call is `UploadImage`, then `SegmentImage` with the returned `image_id`, then `DeleteImage`. `--labels` uses `1` for foreground and `0` for background. Omit `--labels` to treat every point as foreground. `--inline` sends the image bytes on the segment request instead of caching them. `--tls` uses TLS, and so does port 443.

A browser page with the same point prompts can be turned on with the server flag `--demo-site`. See the segmentation server README.

# Segmentation client example

Command-line sample for the segmentation server. It opens an image, sends foreground and background points, and plots the mask with matplotlib.

From this directory, with `segmentation_grpc` and the dependencies in `pyproject.toml` installed:

```bash
python client_example.py --server segmentation.codepharm.net:40443 --image path/to/image.png --coordinates 100,200 300,400 --labels 1,0
```

The default call is `UploadImage`, then `SegmentImage` with the returned `image_id`, then `DeleteImage`. `--labels` uses `1` for foreground and `0` for background. Omit `--labels` to treat every point as foreground. `--inline` sends the image bytes on the segment request instead of caching them.

The server only speaks TLS, so the client always opens a TLS channel. Against a server with a Let's Encrypt certificate nothing more is needed. For a local server with a self-signed certificate, make one and trust it:

```bash
python -m segmentation_server.dev_cert --out ./dev-cert
# start the server with SSL_CERT_PATH=./dev-cert/cert.pem SSL_KEY_PATH=./dev-cert/key.pem
python client_example.py --server localhost:443 --ca-cert ./dev-cert/cert.pem --image path/to/image.png --coordinates 100,200
```

`test_service.py` does all of that in one go: it starts a server process with a temporary certificate, waits for it, segments one image, and shuts it down.

A browser page with the same point prompts can be turned on with the server flag `--demo-site`. See the segmentation server README.

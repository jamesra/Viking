
###################
SegmentationService
###################

Source: ``gRPC_Protos/Segmentation/SAM2/segmentation.proto``

Package: ``segmentation``

Host: the SAM2 segmentation server

Interactive tracing. The client uploads a mosaic image, or the 1024×1024 tiles that cover the view, and sends foreground and background points. The server returns polygons. Viking draws those polygons as a location. It does not use the labeled-image PNG.

Points are pixels in the image that was uploaded. For tiles, X increases to the right and Y increases upward (positions in the tiles' coordinate space divided by the tile downsample). ``TileCoord.row`` and ``col`` are ``floor(position / (1024 * downsample))``. Row increases with Y. ``TileCoord.space`` says which space: ``COORDINATE_SPACE_MOSAIC`` is the assembled section before any slice-to-volume registration, ``COORDINATE_SPACE_VOLUME`` is after it. Prompts, masks, ``origin_x/y`` and polygons of a request are in its tiles' space. ``COORDINATE_SPACE_UNSPECIFIED`` is taken as volume space for clients built before the field; the next release build drops that.

``SegmentTilesRequest.annotation_context_id`` names the Identity annotation context the client has open. A server that context is local to (the SegmentationService's local resources in Identity) reads plain greyscale cells from the context's VikingXML tileset and does not ask the client for them.

``TileCoord`` is the cache key shared by clients: ``volume``, ``section``, ``channel``, ``transform``, ``downsample``, ``row``, ``col``. Do not invent a second id for the same cell. The server treats two uploads with one key as the same picture and hands one client's tile to another, so the key must name everything that changes the render. Viking puts the channel set it blends (each channel's name, colour and source section) and the view options drawn into a capture into ``channel``; a plain greyscale view sends just the channel name.

Upload and cache
================

.. list-table::
   :header-rows: 1
   :widths: 28 72

   * - RPC
     - Behavior
   * - ``UploadImage``
     - Stores one image (``image_data``, ``width``, ``height``) and returns ``image_id``. Later calls pass that id and skip re-encoding.
   * - ``UploadTile``
     - Stores one 1024×1024 cell. A ``TileCoord`` that is already cached keeps its entry, whatever bytes come with the upload, and is not re-encoded. ``already_cached`` is true in that case. There is no image id. ``SegmentTiles`` addresses the cell by ``TileCoord``. The server also keeps each new tile's bytes in host memory and, with the embedding disk cache on, on disk, so it can rebuild the tile after the GPU cache drops it without asking a client.
   * - ``DeleteImage``
     - Drops a cached ``image_id``.
   * - ``GetServerStatus``
     - ``version``, ``uptime_seconds``, a status ``message``, ``in_flight_requests``, ``cached_images``, ``cache_memory_bytes``, ``recent_latency_ms`` (an exponential moving average of upload and segment calls), and ``inference_workers``.

Segment
=======

.. list-table::
   :header-rows: 1
   :widths: 28 72

   * - RPC
     - Behavior
   * - ``SegmentTilesStream``
     - Bidirectional stream. The client sends one ``start`` (``SegmentTilesRequest``): the cells under its prompt, which it need not have uploaded, plus foreground and background points in the fused mosaic. For every cell it needs, the start cells included, the server looks in its GPU cache, then in its stored tiles, then waits for another stream that is already fetching the cell, and only then asks this client. When the mask reaches cells none of those supply, it sends ``needed`` (``TilesNeeded``) and waits. The client uploads those cells with ``UploadTile`` and sends an ``answer`` (``TilesAnswer``) listing each tile as ``ready`` or ``unavailable``. The server continues the same growth, so nothing already predicted is repeated. When nothing more is needed it sends ``result`` (the ``SegmentationResponse``, polygons in the mosaic space) and ends the call.
   * - ``SegmentImage``
     - One point set on one image. Non-zero ``image_id`` uses the cache and ignores ``image_data``. ``image_id`` 0 sends ``image_data`` and re-encodes every call. ``coordinates`` and ``labels`` are parallel arrays (label 1 foreground, 0 background). ``omit_labeled_image`` skips the full-frame PNG. Viking sets it.
   * - ``SegmentImageSets``
     - A stream of independent point sets for one cached image, and a stream of responses. The first message must carry ``image_id``. Later messages may leave it 0 to reuse that id. The server writes one ``SegmentationResponse`` as soon as that set finishes. This is auto-segmentation of many prompts.
   * - ``MultiSegmentImage``
     - One image, many objects. ``foreground_points`` maps a label to a point. Label 0 is background. Any other label is its own object.

``multimask_output`` asks SAM2 for its extra mask candidates. The response lists one ``SegmentResult`` per accepted mask.

Response
========

``SegmentationResponse``

* ``labeled_image``, ``width``, ``height`` — full-frame label image. Empty when ``omit_labeled_image`` was set.
* ``segments`` — one ``SegmentResult`` per object.
* ``origin_x``, ``origin_y`` — bottom-left of a fused tile mosaic, in downsample pixels, Y upward. ``SegmentImage`` leaves these at 0 because the mosaic is the uploaded image.
* ``request_id`` — the ``SegmentTilesRequest.request_id`` echoed back, 0 when the request had none.

``SegmentResult``

* ``index`` — value of this object in the labeled image
* ``score`` — 0 to 1
* ``mask``, ``x``, ``y`` — optional binary mask and its top-left in image pixels. Often omitted.
* ``polygons`` — contours. This is what Viking turns into a location.

A polygon is an ordered list of ``Point`` (``x``, ``y``).

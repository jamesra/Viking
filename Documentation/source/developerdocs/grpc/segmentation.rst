
###################
SegmentationService
###################

Source: ``gRPC_Protos/Segmentation/SAM2/segmentation.proto``

Package: ``segmentation``

Host: the SAM2 segmentation server

Interactive tracing. The client uploads a mosaic image, or the 1024×1024 tiles that cover the view, and sends foreground and background points. The server returns polygons. Viking draws those polygons as a location. It does not use the labeled-image PNG.

Points are pixels in the image that was uploaded. For tiles, X increases to the right and Y increases upward (world divided by the tile downsample). ``TileCoord.row`` and ``col`` are ``floor(world / (1024 * downsample))``. Row increases with world Y.

``TileCoord`` is the cache key shared by clients: ``volume``, ``section``, ``channel``, ``transform``, ``downsample``, ``row``, ``col``. Do not invent a second id for the same cell.

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
     - Stores one 1024×1024 cell. Uploading the same ``TileCoord`` with the same bytes refreshes the entry and does not re-encode. ``already_cached`` is true in that case. There is no image id. ``SegmentTiles`` addresses the cell by ``TileCoord``.
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
     - Bidirectional stream. The client sends one ``start`` (``SegmentTilesRequest``): the tiles it uploaded, plus foreground and background points in the fused mosaic. The server grows the mask over the cells it has. When the mask reaches cells it does not have, it sends ``needed`` (``TilesNeeded``) and waits. The client uploads those cells with ``UploadTile`` and sends an ``answer`` (``TilesAnswer``) listing each tile as ``ready`` or ``unavailable``. The server continues the same growth, so nothing already predicted is repeated. When nothing more is needed it sends ``result`` (the ``SegmentationResponse``, polygons in the mosaic space) and ends the call.
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

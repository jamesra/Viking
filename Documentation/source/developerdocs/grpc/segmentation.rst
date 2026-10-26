
###################
SegmentationService
###################

Source: ``gRPC_Protos/Segmentation/SAM2/segmentation.proto`` (imports ``gRPC_Protos/Common/geometry.proto``)

Package: ``segmentation``

Host: the SAM2 segmentation server (``Servers/SegmentationServer``; its ``README.md`` describes the server side in full)

Interactive tracing. The client names the 1024×1024 tiles under its prompt, sends what the object is (clicks, annotation shapes, a painted bitmap) and what it is not (the neighbouring annotations), uploads the tiles the server asks for, and gets SAM2's logits back, one result per island of the prompt. Viking contours the logits and draws the contour as a location.

The service has four RPCs: ``UploadTile``, ``SegmentTilesStream``, ``GetHeldTiles``, and ``GetServerStatus``. The server only speaks TLS.

Tiles
=====

Every tile is on a fixed grid of 1024×1024 tiles. A tile is a ``TileGrid`` and a ``GridCell`` (:doc:`geometry`). A call names its grid once, and every tile in it is a cell. Row and column are ``floor(position / (1024 * downsample))`` from the origin, and row increases with Y.

``TileGrid.space`` is ``COORDINATE_SPACE_MOSAIC`` (the assembled section before any slice-to-volume registration) or ``COORDINATE_SPACE_VOLUME`` (after it). ``UNSPECIFIED``, or a downsample that is not positive, is ``INVALID_ARGUMENT`` on every RPC.

The grid and cell are the cache key shared by clients. Do not invent a second id for the same cell. The server treats two uploads with one key as the same picture and hands one client's tile to another, so the grid must name everything that changes the render. Viking puts the channel set it blends (each channel's name, colour and source section) and the view options drawn into a capture into ``channel``. A plain greyscale view sends just the channel name.

.. list-table::
   :header-rows: 1
   :widths: 24 76

   * - RPC
     - Behavior
   * - ``UploadTile``
     - Stores one tile: ``grid``, ``cell``, and ``image_data``, a 1024×1024 greyscale or RGB PNG, Y down. Any other size is ``INVALID_ARGUMENT``. A tile that is already cached keeps its entry, whatever bytes come with the upload, and is not re-encoded; ``already_cached`` is true then. An upload a ``SegmentTilesStream`` is waiting for wakes that stream. The server also keeps each new tile's bytes in host memory and, with the embedding disk cache on, on disk, so it can rebuild the tile after the GPU cache drops it without asking a client.
   * - ``GetHeldTiles``
     - Takes a ``grid``, up to 256 ``cells``, and ``annotation_context_id``. Returns one ``held`` flag per cell, in request order. A cell is held when ``SegmentTilesStream`` would not ask for it: it is in the GPU cache or the stored tiles, or the server reads it from the context's tileset. The answer pins and refreshes nothing; a cell dropped since is asked for during the request as usual. Clients call it before uploading tiles ahead of a request (Viking's auto-segment prefetch) and skip the held cells. Requests need not call it: the stream makes the same check.
   * - ``GetServerStatus``
     - ``version``, ``uptime_seconds``, a status ``message``, ``in_flight_requests`` (``UploadTile`` and ``SegmentTilesStream`` calls), ``cached_images``, ``cache_memory_bytes``, ``recent_latency_ms`` (an exponential moving average of call duration), ``inference_workers``, ``encoder_generation`` (``eager`` or ``compiled``), ``compile_status`` (``off``, ``warming``, ``ready`` or ``failed``), and ``model``.

No client deletes tiles. Idle LRU and the GPU memory check reclaim cache slots.

For every tile a stream needs, the cells named in ``start`` included, the server looks in its GPU cache, then in its stored tiles, then waits for another stream that is already fetching the cell, and only then asks this client. When ``annotation_context_id`` names an Identity annotation context the server is local to (the SegmentationService's local resources in Identity), the server reads plain greyscale mosaic cells (``space`` ``MOSAIC``, ``channel`` with no ``[...]`` suffix) from the context's VikingXML tileset itself, before asking the client.

``ModelInfo``
-------------

``GetServerStatus.model`` describes the served model, from the manifest that travels with its weights. It is always set; a model with no manifest reports ``model_id`` ``unknown``.

* ``model_id`` — ``v<VERSION>``, such as ``v5``. Send it back as ``SegmentTilesRequest.model_id``.
* ``model_version`` — one export of that model, ``v<VERSION>.<EPOCH>``, such as ``v5.4``.
* ``display_name`` and ``description`` — what the operator wrote.
* ``prompt_encoding`` — the encoding the server builds SAM2's mask prompt with, such as ``edge-confidence-v6``. The client never encodes; this is for display and logs.
* ``training_data_set`` and ``training_data_fingerprint`` — the training data the model saw. ``unknown`` when not recorded.
* ``contract_json`` — the manifest's ``client_contract`` as JSON (``requires_model_id``, ...). ``circle_diameter_scale`` is applied by the server and is informational here.

Prompt
======

``SegmentTilesRequest`` is the first message of a stream.

.. list-table::
   :header-rows: 1
   :widths: 26 74

   * - Field
     - Meaning
   * - ``grid``
     - The grid every tile of the call is on.
   * - ``tiles``
     - The cells under the prompt. They need not be uploaded: the server asks for the ones it does not hold.
   * - ``foreground_feature``
     - What the object is.
   * - ``background_feature``
     - What the object is not: the neighbouring annotations. Send the shapes that reach the cells in ``tiles``. The server asks for more as the object grows (see ``background_cells`` below).
   * - ``mask_tile_count``
     - How many ``MaskTile``\ s follow ``start``, foreground and background together. 0 when there is no painted bitmap.
   * - ``mask_threshold``
     - Logit above which a pixel is object. Higher values shrink the mask; lower values grow it. Optional: when it is omitted the server uses its own default.
   * - ``use_mask_input``
     - Predict each window twice; the second pass is given the first pass's logits as SAM2's mask input along with the same prompt.
   * - ``request_id``
     - Client-issued ticket, echoed in the response and written to the server log. 0 means not set.
   * - ``model_id``
     - The model the client built the prompt for. Another id is ``FAILED_PRECONDITION``. Unset is accepted unless the model's manifest says ``requires_model_id``.
   * - ``annotation_context_id``
     - The Identity annotation context the client has open, 0 when it has none.

Every coordinate is in the grid's space at its downsample: X right, Y up, mosaic or volume pixels divided by the downsample. Pixel ``(x, y)`` covers ``[x, x + 1) × [y, y + 1)``. A click is the pixel it falls in.

A ``Feature`` is one side of the prompt:

* ``points`` — SAM2 clicks, as ``Vector2``.
* ``shapes`` — annotation shapes (circle, ellipse, line, polygon) as ``Shape``. A ``Shape.point`` is ``INVALID_ARGUMENT``: a click goes in ``points``. Circles carry their true radius.

A request needs a foreground click, shape, or mask tile (``INVALID_ARGUMENT`` otherwise).

Roles
-----

The client does not say how each part is used. The server encodes the prompt for the model it serves (``ModelInfo.prompt_encoding``), the way that model was trained, and decides from what each part is:

* A foreground shape (a circle, a carried polygon) is softened: its edge is not trusted. A circle is softened by the model's ``circle_diameter_scale``.
* A foreground bitmap is used as drawn.
* Several foreground shapes and the foreground bitmap are one object, combined by the largest encoded value.
* Background shapes and the background bitmap are what the object is not, each trusted by its geometry: a circle by its radius, any other shape by its edge.

Mask tiles
----------

A painted bitmap of any size is sent as the tiles it touches, at most one bitmap per side. ``MaskTile`` has ``cell`` (on the call's grid), ``kind`` (``MASK_KIND_FOREGROUND`` or ``MASK_KIND_BACKGROUND``), ``full`` (every pixel set; ``png`` is then empty), ``png``, and ``scale``. ``png`` is a 1-bit greyscale PNG, Y down, 1024×1024 when ``scale`` is 0 or 1, or 256×256 when ``scale`` is 4 (each PNG pixel stands for a 4×4 block). A non-zero pixel is set. An empty tile has an empty ``png`` and ``full`` false.

Mask tiles travel in ``mask_tiles`` stream messages right after ``start``. The server waits for all of them before growth starts. A repeated (cell, kind), a PNG of the wrong size, an unset kind, or more tiles than announced is ``INVALID_ARGUMENT``. Mask tiles belong to one call and never enter the image cache.

Islands
-------

An object prompt that falls apart is several objects. Each island is segmented by its own walk and returned as its own ``SegmentResult``. Islands are numbered 1, 2, ... by decreasing trusted area. A foreground click goes with the island it lies in; a click outside every island goes with island 1. The background goes with every island. Clicks alone are one island.

The conversation
================

``SegmentTilesStream`` is a bidirectional stream.

.. list-table::
   :header-rows: 1
   :widths: 30 70

   * - Message
     - Meaning
   * - ``start`` (client)
     - ``SegmentTilesRequest``. The first message, once.
   * - ``mask_tiles`` (client)
     - The mask tiles announced by ``mask_tile_count``, right after ``start``.
   * - ``needed`` (server)
     - ``TilesNeeded``: ``tiles`` to upload with ``UploadTile``, and ``background_cells``, cells the object has grown into whose neighbouring annotations the server has not been sent.
   * - ``answer`` (client)
     - ``TilesAnswer``, one per ``TilesNeeded``, in order: ``unavailable`` lists the tiles the client could not supply (capture failed, off the volume), and ``background_shapes`` holds the background shapes that reach the ``background_cells`` and that the client has not sent before (none when no neighbour reaches them).
   * - ``result`` (server)
     - The finished ``SegmentationResponse``. The server ends the call after sending it.

1. Send ``start``, then the announced mask tiles. Nothing needs uploading first.
2. For each ``needed``, upload its ``tiles`` with ``UploadTile``, then send one ``answer``. The answer reports failures only: uploads are not listed, because the server sees them arrive. A tile is settled when its upload arrives or the answer lists it as unavailable. The server uses each tile as soon as its upload lands, without waiting for the answer, and growth does not enter unavailable tiles.
3. The server continues the same growth after each round, so nothing already predicted is repeated. When nothing more is needed it sends ``result``.

A ``needed`` can list fewer tiles than the walk wanted, or none (only ``background_cells``), because a tile another stream has already asked its client for is not asked for again. Each cell's neighbours are asked about once per call.

The server pins every tile it uses for the whole call. A client that cancels, or ends its half of the stream while tiles are awaited, abandons the request and gets no result.

Status codes
------------

* After ``result`` the call ends ``OK``.
* ``DEADLINE_EXCEEDED`` — the announced mask tiles, or an upload or answer that is owed, did not arrive within the server's idle limit (default 60 s).
* ``INVALID_ARGUMENT`` — a malformed conversation or request (for example an answer with no ``needed`` outstanding).
* ``RESOURCE_EXHAUSTED`` — over a server limit, naming the limit. Defaults: 64 tiles, 512 points and 512 shapes per ``Feature`` (and shapes per answer), 256 mask tiles, 32 concurrent streams. A call refused for concurrency can be retried.
* ``FAILED_PRECONDITION`` — another ``model_id``; or ``NO_MATCHING_MASK ...`` when no island's starting tiles yield a mask that fits the prompt.
* ``UNAVAILABLE`` — the server lost its GPU.

Result
======

``SegmentationResponse`` holds ``segments`` and ``request_id`` (the request's ``request_id`` echoed back, 0 when it carried none). There is one ``SegmentResult`` per island that found something, in island order. An island that finds nothing has no result, so island numbers can have gaps.

A ``SegmentResult`` is SAM2's logits at their native resolution:

* ``island`` — which island of the object prompt this is.
* ``score`` — confidence, 0 to 1.
* ``logits`` — little-endian float16, row-major, ``logits_size.width`` per row, rows from the top (largest Y) down. Samples nobody predicted hold a large negative value.
* ``logits_size`` — width and height in samples (``IntSize``).
* ``logits_origin`` — the top-left pixel of sample (0, 0), as an ``IntPoint`` pixel index in grid space (X right, Y up, at the grid's downsample).
* ``logits_scale`` — pixels per logit sample on both axes (4 for a SAM2 window).

With origin ``(ox, oy)`` and scale ``s``, sample ``(i, j)`` (column ``i``, row ``j``) covers::

   pixel columns  ox + i * s  ..  ox + (i + 1) * s - 1
   pixel rows     oy - (j + 1) * s + 1  ..  oy - j * s

and its centre is ``(ox + (i + 0.5) * s, oy + 1 - (j + 0.5) * s)``. The client contours the logits at the request's ``mask_threshold`` and places the contour with the origin and scale.

Removed
=======

The image calls (``UploadImage``, ``SegmentImage``, ``SegmentImageSets``, ``MultiSegmentImage``, ``DeleteImage``) and ``TileCoord`` are gone; every call is on a ``TileGrid``. Responses no longer carry polygons, masks, the labeled image, or upscaled logits, and requests no longer carry rectangles, mask roles, ``multimask_output`` or response-content flags. ``TilesAnswer`` no longer lists ready tiles.

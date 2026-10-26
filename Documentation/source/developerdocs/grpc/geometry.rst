
###############
Shared geometry
###############

Source: ``gRPC_Protos/Common/geometry.proto``

Package: ``viking.geometry.v1``

These messages are not a service. Every Viking gRPC service uses them: :doc:`annotate-locations` and :doc:`annotate-structures` for positions, shapes and regions, :doc:`annotate-section-corrections` for positions and offsets, and :doc:`segmentation` for tiles and prompts. Coordinates are doubles. The integer types are only for things that are integers: grid cells, pixel positions, and pixel or sample counts.

Coordinates and frames
======================

``Vector2``
   A point or vector: ``x``, ``y``. X increases to the right and Y increases upward unless the field that uses it says otherwise.

``Rectangle``
   An axis-aligned box: ``x_min``, ``y_min``, ``x_max``, ``y_max``, with ``x_min <= x <= x_max`` and ``y_min <= y <= y_max``.

``CoordinateSpace``
   Which coordinate system a shape's numbers are in.

   .. list-table::
      :header-rows: 1
      :widths: 34 66

      * - Value
        - Meaning
      * - ``COORDINATE_SPACE_UNSPECIFIED``
        - Not set. Refused wherever a space is required.
      * - ``COORDINATE_SPACE_MOSAIC``
        - A section's assembled mosaic image, before any slice-to-volume registration (stos) warps it.
      * - ``COORDINATE_SPACE_VOLUME``
        - The registered volume: the section after its slice-to-volume transform.

``Frame``
   The frame a shape's numbers are in, for messages that do not imply one: ``space``, ``section`` (Z), and ``downsample`` (pixels of the space per coordinate unit; 0 or 1 is full resolution).

Integer types
=============

Each concept has its own type, so a field reads as what it is.

``GridCell``
   A cell of a grid: ``row`` counts along Y and ``col`` along X, from the grid's origin.

``IntSize``
   A count of pixels or samples along each axis: ``width``, ``height``.

``IntPoint``
   An integer pixel position: ``x``, ``y``. The field that uses it states the frame (Y up in mosaic or volume pixels, or Y down in an image).

Tile grid
=========

``TileGrid`` is one grid of 1024×1024 tiles: what every cell of the grid shares. A tile is a ``TileGrid`` and a ``GridCell``. Row and column are ``floor(position / (1024 * downsample))`` in ``space``, from the origin (0, 0). Row increases with Y.

.. list-table::
   :header-rows: 1
   :widths: 20 80

   * - Field
     - Meaning
   * - ``volume``
     - VikingXML volume name.
   * - ``section``
     - Section number (Z).
   * - ``channel``
     - Channel name, plus a bracketed suffix for anything else drawn into the capture. A bare channel name is a plain greyscale view.
   * - ``transform``
     - ``<volume transform>|<section tile transform>``: the StosGroup (empty in mosaic space) and the ``.mosaic`` the pyramid is placed with.
   * - ``downsample``
     - Pixels of the space per tile pixel.
   * - ``space``
     - ``COORDINATE_SPACE_MOSAIC`` or ``COORDINATE_SPACE_VOLUME``. ``UNSPECIFIED`` is refused.

The grid is part of every tile's cache identity. ``channel`` must name everything, beyond the other fields, that changes the render.

Shapes
======

``Shape`` is one annotation geometry. It is a ``oneof``:

.. list-table::
   :header-rows: 1
   :widths: 16 84

   * - Case
     - Meaning
   * - ``point``
     - A ``Vector2``.
   * - ``circle``
     - ``Circle``: ``center`` and ``radius``.
   * - ``ellipse``
     - ``Ellipse``: ``center``, ``semi_major``, ``semi_minor``, and ``rotation``, the major axis's angle from +X, counter-clockwise, in radians.
   * - ``line``
     - ``Line``: ``points``, ``closed``, ``width``, and ``fit``. A line is drawn with a width.
   * - ``polygon``
     - ``Polygon``: an ``exterior`` ``Ring``, ``holes`` (more rings), and ``fit``.

A ``Ring`` is a closed list of ``points``. The first vertex is not repeated at the end. Orientation is not significant.

``CurveFit`` says how a curved outline is drawn from its control points, so every consumer draws the same outline. ``method`` is ``CURVE_METHOD_NONE`` (straight segments between the points) or ``CURVE_METHOD_CATMULL_ROM`` (centripetal Catmull-Rom through the control points, as ``Geometry/CurveFitting/CatmullRom.cs`` ``FitCurve`` draws it). ``interpolations`` is the number of points added between consecutive control points. It is ignored for ``CURVE_METHOD_NONE``.

An annotation's type follows from the shape. Nothing else states it:

.. list-table::
   :header-rows: 1
   :widths: 46 54

   * - Shape
     - ``AnnotationType``
   * - ``point``
     - ``POINT``
   * - ``circle``
     - ``CIRCLE``
   * - ``ellipse``
     - ``ELLIPSE``
   * - ``line``, open, no fit
     - ``POLYLINE``
   * - ``line``, open, with a fit
     - ``OPENCURVE``
   * - ``line``, closed, with a fit
     - ``CLOSEDCURVE``
   * - ``polygon``, no fit
     - ``POLYGON``
   * - ``polygon``, with a fit
     - ``CURVEPOLYGON`` (each ring's points are control points, each ring fitted closed)

Circles and widths between frames
=================================

A circle's centre is the annotation's position, never a point of its outline. ``radius`` is the true radius in the shape's frame.

Between frames (mosaic and volume, or one section and another) the radius is carried by the five-point mean scale, never copied. Map the centre ``c`` and the four axis rim points ``c + R e_k``; the new radius is::

   R' = R * mean_k( |map(c + R e_k) - map(c)| / R )

A ``Line``'s ``width`` is in the frame's units and is carried between frames by the same mean scale.

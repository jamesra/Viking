
##########################
Shared annotation messages
##########################

Source: ``gRPC_Protos/Annotation/AnnotationTypes.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

These messages are not a service. The annotation RPCs use them.

Coordinates
===========

Positions, shapes and regions use the shared types on :doc:`geometry`: a position is a ``Vector2``, a shape is a ``Shape``, and a region is a ``Rectangle``. Mosaic positions are section pixels. Volume positions are the same point after the section transform. Volume nanometers used by :doc:`annotate-section-corrections` are these coordinates multiplied by :doc:`annotate-metadata` ``Scale``.

``AxisUnits`` is a unit name (``nm``, ``mm``, and so on) and a scale value. ``Scale`` has ``x``, ``y``, and optional ``z``.

Shapes
======

``AnnotationType`` names how a location is drawn and edited. A ``Location`` does not send it: its type follows from its ``Shape`` (the case, ``Line.closed`` and the fit; see :doc:`geometry`). The enum remains because ``StructureType.AllowedShapes`` is a bit mask indexed by these values.

.. list-table::
   :header-rows: 1
   :widths: 20 80

   * - Value
     - Meaning
   * - ``POINT``
     - A point.
   * - ``CIRCLE``
     - A circle. Radius is ``Circle.radius``.
   * - ``ELLIPSE``
     - An ellipse.
   * - ``POLYLINE``
     - An open chain of straight segments.
   * - ``POLYGON``
     - A filled polygon. Exterior vertices are not curve-fit.
   * - ``OPENCURVE``
     - An open curve with a line width. Extra vertices come from curve fitting.
   * - ``CURVEPOLYGON``
     - A polygon whose outer and inner rings are curve-fit.
   * - ``CLOSEDCURVE``
     - A closed ring of segments with a line width.

``DBAction`` is ``NONE``, ``INSERT``, ``UPDATE``, or ``DELETE``. Permitted-link updates still use this enum. Structure, structure-type, and location updates use a ``oneof`` instead.

Records
=======

``StructureType``
   A schema node. ``parent_id`` is the parent type. ``abstract`` types are not placed directly. ``color`` is a packed color. ``code`` is the short name. ``AllowedShapes`` has bit ``1 << AnnotationType`` set for each type this structure type may use. ``permitted_structure_links`` is filled when the type is loaded with its link rules. ``attributes`` and ``structure_attributes`` are the XML attribute templates.

``Structure``
   One annotated object. ``type_id`` is the structure type. ``parent_id`` is the parent structure, not the type. ``child_ids`` and ``links`` are included when the call returns the graph. ``confidence`` and ``verified`` are the tracing flags. ``label`` is the display name.

``StructureLink``
   An edge from ``source_id`` to ``target_id``. ``bidirectional`` means the edge is stored in both directions. ``tags`` is free text.

``Location``
   One annotation on a section. ``parent_id`` is the owning structure. ``section`` is Z. ``mosaic_position`` and ``volume_position`` are ``Vector2`` positions in mosaic and volume space (a circle's centre; for other shapes the position Viking stores with the location). ``mosaic_geometry`` and ``volume_geometry`` are the ``Shape`` in each space. The shape carries what used to be separate fields: a circle's radius, a line's width and closed flag, and the curve fit. For the curve types (open curve, closed curve, curve polygon) ``volume_geometry`` is the drawn, fitted outline sent without a fit, and the annotation's type follows from ``mosaic_geometry``. A circle's volume radius is the mosaic radius carried by the five-point mean scale (:doc:`geometry`). ``terminal`` ends a branch. ``off_edge`` marks a contour that leaves the section. ``links`` are the other location ids this location connects to.

``LocationLink``
   A pair of location ids. Direction is source to target.

``PermittedStructureLink``
   A schema rule: structures of ``source_type_id`` may link to ``target_type_id``. ``bidirectional`` allows the reverse pair as well.

Timestamps are ``google.protobuf.Timestamp`` and are UTC.

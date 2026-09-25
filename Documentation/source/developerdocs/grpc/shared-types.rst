
##########################
Shared annotation messages
##########################

Source: ``gRPCAnnotationServiceTypes/Protos/AnnotationTypes.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

These messages are not a service. The annotation RPCs use them.

Coordinates
===========

``AnnotationPoint`` is ``x``, ``y``, and optional ``z``. Mosaic positions are section pixels. Volume positions are the same point after the section transform. Volume nanometers used by the correction services are these coordinates multiplied by :doc:`annotate-metadata` ``Scale``.

``BoundingRectangle`` is an axis-aligned box: ``xmin``, ``ymin``, ``xmax``, ``ymax``.

``Geometry`` is one of:

* ``text`` — a geometry string the annotation database already stores
* ``binary`` — the same geometry as bytes

``AxisUnits`` is a unit name (``nm``, ``mm``, and so on) and a scale value. ``Scale`` has ``x``, ``y``, and optional ``z``.

Shapes
======

``AnnotationType`` selects how a location is drawn and edited.

.. list-table::
   :header-rows: 1
   :widths: 20 80

   * - Value
     - Meaning
   * - ``POINT``
     - A point.
   * - ``CIRCLE``
     - A circle. Radius is ``Location.radius``.
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
   A schema node. ``parent_id`` is the parent type. ``abstract`` types are not placed directly. ``color`` is a packed color. ``code`` is the short name. ``AllowedShapes`` is the set of ``AnnotationType`` values this type may use. ``permitted_structure_links`` is filled when the type is loaded with its link rules. ``attributes`` and ``structure_attributes`` are the XML attribute templates.

``Structure``
   One annotated object. ``type_id`` is the structure type. ``parent_id`` is the parent structure, not the type. ``child_ids`` and ``links`` are included when the call returns the graph. ``confidence`` and ``verified`` are the tracing flags. ``label`` is the display name.

``StructureLink``
   An edge from ``source_id`` to ``target_id``. ``bidirectional`` means the edge is stored in both directions. ``tags`` is free text.

``Location``
   One annotation on a section. ``parent_id`` is the owning structure. ``section`` is Z. ``mosaic_position`` and ``mosaic_shape`` are section space. ``volume_position`` and ``volume_shape`` are volume space. ``closed`` is the shape flag. ``terminal`` ends a branch. ``off_edge`` marks a contour that leaves the section. ``radius`` is the circle radius or the hit radius. ``width`` is the line width when the shape has one. ``links`` are the other location ids this location connects to. ``type_code`` is the ``AnnotationType``.

``LocationLink``
   A pair of location ids. Direction is source to target.

``PermittedStructureLink``
   A schema rule: structures of ``source_type_id`` may link to ``target_type_id``. ``bidirectional`` allows the reverse pair as well.

Timestamps are ``google.protobuf.Timestamp`` and are UTC.

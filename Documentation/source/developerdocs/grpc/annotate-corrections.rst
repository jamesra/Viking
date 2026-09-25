
###################
AnnotateCorrections
###################

Source: ``gRPCAnnotationServiceTypes/Protos/Correction.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

Host: GrpcAnnotationService

This service only reads correction files that SectionCorrectionBuilder already wrote. It does not rebuild them. For a host that rebuilds many volumes and can correct whole structures, use :doc:`annotate-section-corrections`.

A correction set is a residual vector field for one VikingXML Stos group (for example ``SliceToVolume``). After mosaic placement and Stos, a point can still sit off the consensus of neighboring processes. The field is that leftover, stored as a sparse lattice.

XY in these messages is volume nanometers: annotation coordinates times :doc:`annotate-metadata` ``Scale``. ``pitch_nm`` is the lattice step. It is not the scale.

Status codes
============

* Unknown ``stos_group``: ``NOT_FOUND``.
* ``GetSectionCorrection`` for a Z with no map: ``NOT_FOUND`` (``no correction for section {z} in '{name}'``).
* ``CorrectPoints`` with no sections, a section with no points, or a repeated ``z``: ``INVALID_ARGUMENT``.

RPCs
====

.. list-table::
   :header-rows: 1
   :widths: 28 72

   * - RPC
     - Behavior
   * - ``ListCorrectionSets``
     - Every published Stos group on this server, with provenance.
   * - ``ListCorrectedSections``
     - The Z values that have a map for ``stos_group``.
   * - ``GetCorrectionManifest``
     - Provenance plus the Z list. ``volume_url`` is the REST URL of ``volume.npz`` when that file is hosted. It is empty when the set is gRPC-only.
   * - ``GetSectionCorrection``
     - The sparse lattice for one Z. Use this to interpolate on the client.
   * - ``CorrectPoints``
     - Samples the field. Points are grouped by section. Every point on a section that has a map is interpolated.

Provenance
==========

Two published sets are the same build when ``stos_group`` and ``CorrectionProvenance`` match. Sampling does not read these fields. They tell a client when to drop a cache.

* ``built_utc`` — when the builder finished
* ``annotation_watermark`` — newest annotation ``LastModified`` included in the build
* ``pitch_nm`` — lattice step
* ``kernel_radius_nm`` — how far the builder looked for votes at each node (default 8000)
* ``min_annotation_votes`` — minimum votes before a node is stored
* ``ResidualWindow`` — the leave-one-out Catmull-Rom window used while measuring votes

``ResidualWindow`` is both-sided and one-sided. Both-sided needs ``min_both`` neighbors below and above the section and keeps at most ``max_both`` on each side (defaults 2 and 3). A terminal section needs ``min_one`` neighbors on the side that has them and keeps at most ``max_one`` (defaults 5 and 7). The builder would rather leave a node empty than copy a single neighbor.

Lattice
=======

``SectionCorrection`` is a sparse grid. The world position of node ``(gx, gy)`` is::

   x = origin_x_nm + gx * pitch_nm
   y = origin_y_nm + gy * pitch_nm

``LatticeVector.dx`` and ``dy`` are added to a volume point: corrected ``(x + dx, y + dy)``. ``vote_count`` is how many annotations supported that node.

CorrectPoints
=============

Group points with the same Z into one ``SectionPoints``. Do not send the same Z twice.

``CorrectedSection.found_section`` is false when that Z has no map. Those points come back with ``dx = dy = 0`` and ``trusted = false``.

``CorrectedXY.trusted`` means the point sat on measured support: a 2×2 occupied cell, or inverse-distance weighting inside the kernel radius. Interpolation still runs when ``trusted`` is false. The returned delta is then usually zero and should not be applied as a warp.

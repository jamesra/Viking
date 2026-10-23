
##########################
AnnotateSectionCorrections
##########################

Source: ``gRPC_Protos/SectionCorrection/section_correction.proto``

Package: ``Viking.SectionCorrectionServiceTypes.gRPC.V1``

Host: GrpcSectionCorrectionService

Same residual fields as :doc:`annotate-corrections`, for many volumes on one host. Files live under ``{root}/{volume_name}/{stos_group}/``. This service also rebuilds those files and can warp whole structures.

XY is volume nanometers (annotation coordinates times ``AnnotateMetaData.Scale``).

``CorrectionMode`` matches ``AnnotationVizLib.CorrectionMode``:

.. list-table::
   :header-rows: 1
   :widths: 36 64

   * - Value
     - Meaning
   * - ``CORRECTION_MODE_NONE``
     - Do not move points.
   * - ``CORRECTION_MODE_NEIGHBOR``
     - Sample the published residual field.
   * - ``CORRECTION_MODE_CURVE_FIT``
     - Leave-one-out Catmull-Rom along the process.
   * - ``CORRECTION_MODE_ALL``
     - Neighbor, then curve fit. This is the default when ``correction`` is omitted.

Status codes
============

* Unknown ``volume_name`` or ``stos_group``: ``NOT_FOUND``.
* ``GetSectionCorrection`` for a Z with no map: ``NOT_FOUND`` (``no correction for section {z} in '{volume}' '{name}'``).
* ``CorrectPoints`` with no sections, a section with no points, or a repeated ``z``: ``INVALID_ARGUMENT``.

Catalog
=======

.. list-table::
   :header-rows: 1
   :widths: 32 68

   * - RPC
     - Behavior
   * - ``ListVolumes``
     - Volumes this host rebuilds, plus any volume folder already on disk. Each ``VolumeInfo`` has ``volume_name``, the VikingXML URL, and the annotation endpoint.
   * - ``GetRebuildStatus``
     - Whether a rebuild is running, when the last one started and finished, ``last_error``, and progress of the running pass (``current_volume``, ``current_step``, ``groups_done`` of ``groups_total``). ``service_version`` is the host assembly version. It changes when the algorithm or ``CorrectStructures`` changes even if ``built_utc`` on the field does not. Cache keys should include both.
   * - ``ListCorrectionSets``
     - Published sets. An empty ``volume_name`` lists every volume.
   * - ``ListCorrectedSections``
     - Z values for one volume and Stos group.
   * - ``GetCorrectionManifest``
     - Provenance and the Z list.
   * - ``GetSectionCorrection``
     - Sparse lattice for one Z. Node ``(gx, gy)`` sits at ``(gx * pitch_nm, gy * pitch_nm)`` in volume nanometers. Add ``dx`` and ``dy`` to a volume point.
   * - ``GetStructureFingerprint``
     - The current annotation fingerprint of ``structure_ids`` (and children when ``include_children`` is set), from SQL aggregates only: no volume load and no fit, so it answers in well under a second. See `Caching`_.

Provenance is ``built_utc`` (when the set was published), ``pitch_nm`` and ``kernel_radius_nm``.

Sampling
========

``CorrectPoints`` samples XY that the caller already has. Group points by Z into ``SectionPoints``. The response sections are in the same order as the request. ``found_section`` false means that Z has no map: deltas are zero and ``trusted`` is false. ``trusted`` means the point was on measured support, not that interpolation failed to run.

``CorrectStructures`` loads the locations for ``structure_ids`` from the annotation service named on that volume. ``include_children`` also loads child structures. It then applies ``correction`` (default All). Each ``CorrectedLocation`` is one location: ids, Z, corrected ``x`` and ``y``, and the delta. ``service_version`` is echoed so the client can key a cache without a separate status call.

``CorrectStructures`` is the call morphology and export use when they want warped contours. ``CorrectPoints`` is the call for a handful of positions the client already holds.

Caching
=======

A cached ``CorrectStructures`` answer is stale when any of these changed since it was fetched:

* ``service_version`` (``GetRebuildStatus``): the algorithm changed.
* The set's ``CorrectionProvenance.built_utc`` (``ListCorrectionSets``): the residual field was rebuilt.
* ``annotation_fingerprint`` (``GetStructureFingerprint``, same ``structure_ids`` and ``include_children``): an annotation of those structures was added, edited or deleted.

``CorrectStructuresResponse`` also carries ``annotation_watermark``, the newest location or link edit time. It does not move when a location or link is deleted, so compare fingerprints, not watermarks. The response's fingerprint is read before its rows, so an edit made during the call shows up as a change on the next check.

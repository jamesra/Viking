
##########################
AnnotateSectionCorrections
##########################

Source: ``gRPC_Protos/SectionCorrection/section_correction_v2.proto``

Package: ``Viking.SectionCorrectionServiceTypes.gRPC.V2``

Host: GrpcSectionCorrectionService

A correction set is a residual vector field for one VikingXML Stos group (for example ``SliceToVolume``) of one volume. After mosaic placement and Stos, a point can still sit off the consensus of neighboring processes. The field is that leftover, stored as a sparse lattice. This host serves the sets of many volumes, rebuilds them (files live under ``{root}/{volume_name}/{stos_group}/``), and can warp whole structures. It is the only correction service: the annotation service's ``AnnotateCorrections`` was removed.

All positions and offsets are volume nanometers (annotation coordinates times ``AnnotateMetaData.Scale``), carried as the shared ``viking.geometry.v1.Vector2`` (``gRPC_Protos/Common/geometry.proto``). A correction set is named by ``CorrectionSetId`` (``volume_name`` plus ``stos_group``).

Protocol version
================

The protocol is versioned by package name. V2 (``section_correction_v2.proto``, package ``...gRPC.V2``) is the only version served; V1 is retired. Viking, Jotunn, MonogameTestbed, DataExport and the service's ``correction-menu`` use it.

``CorrectionMode``:

.. list-table::
   :header-rows: 1
   :widths: 36 64

   * - Value
     - Meaning
   * - ``CORRECTION_MODE_NEIGHBOR`` (0, the default)
     - Sample the published residual field. From 1.7.0, ``CorrectStructures`` then fits each location on a damaged section (the set's ``damaged_z``) along its process, through the locations on undamaged sections; see `Damaged sections`_. ``GetSectionCorrection`` and ``CorrectPoints`` are the field alone.
   * - ``CORRECTION_MODE_NONE`` (1)
     - Do not move points: the StosGroup mapping alone.

Values 2 and 3 (``ALL`` and ``CURVE_FIT``) are reserved in the proto. Service 1.5.2 restores the leave-one-out Catmull-Rom fit along each process, for a damaged-sections-only test: sent as a raw integer, 4 answers the field then the fit; 2 and 3 still answer the field. Unset or 0 is the field, 1 is none, and any other value is refused with ``INVALID_ARGUMENT``. New code should send 0 or 1. The measurements are in ``docs/publication_notes.md`` (2026-10-27).

Status codes
============

* Unknown correction set: ``NOT_FOUND``.
* ``GetSectionCorrection`` for a Z with no map: ``NOT_FOUND``.
* ``CorrectPoints`` with no sections, a section with no points, or a repeated ``z``: ``INVALID_ARGUMENT``.
* ``CorrectStructures`` and ``GetAnswerStamp`` with no ``structure_ids``: ``INVALID_ARGUMENT``; a volume with no annotation database mapping: ``FAILED_PRECONDITION``.
* A volume host, Identity or annotation database that is unreachable or times out: ``UNAVAILABLE`` (retry).

Calls
=====

.. list-table::
   :header-rows: 1
   :widths: 32 68

   * - RPC
     - Behavior
   * - ``ListVolumes``
     - Volumes this host rebuilds: each ``VolumeInfo`` has ``volume_name`` (the annotation database), the VikingXML URL, and the annotation endpoint.
   * - ``GetRebuildStatus``
     - Whether a rebuild is running, when the last one started and finished, ``last_error``, and progress of the running pass (``current_volume``, ``current_step``, ``groups_done`` of ``groups_total``). ``service_version`` is the host version.
   * - ``RebuildVolume``
     - Starts a rebuild in the background. Only accepted from the service host itself (the container's ``correction-menu``).
   * - ``ListCorrectionSets``
     - Every published ``CorrectionSet`` (or one volume's): its id, ``built_utc``, ``pitch_nm``, ``kernel_radius_nm``, the sections with a map, and ``damaged_z``, the damaged sections the field was built with (from 1.6.0, builder 3; see `Damaged sections`_).
   * - ``GetSectionCorrection``
     - Sparse lattice for one Z. Each ``LatticeNode`` names its cell as a ``GridCell`` ``node``; cell ``(row, col)`` sits at ``(col * pitch_nm, row * pitch_nm)``. Add its ``offset`` to a volume point.
   * - ``CorrectPoints``
     - Samples the field at points the caller holds. One ``SectionOffsets`` per request section, in request order, with one ``PointOffset`` per point. ``found`` false means that Z has no map: offsets are zero and untrusted. ``trusted`` means the point was on measured support.
   * - ``CorrectStructures``
     - Loads the locations of ``structure_ids`` (and child structures when ``include_children`` is set), maps them through the StosGroup, then applies ``correction``. Each ``CorrectedLocation`` carries the corrected ``position`` and the ``offset`` from the uncorrected one. The response carries an ``AnswerStamp``.
   * - ``GetAnswerStamp``
     - Takes the same request as ``CorrectStructures`` and returns the stamp its answer would carry now, from SQL aggregates and the set catalog alone (no volume load, no fit): well under a second.

Damaged sections
================

A volume's damaged sections are its blocks' ``NonStosSectionNumbers``, the sections the slice-to-volume registration was built without. The builder reads them from the nornir files next to the VikingXML: ``<VikingXML folder>/VolumeData.xml`` names each block (``Block_Link Path``), and ``<folder>/<block>/VolumeData.xml`` holds the list.

The field is built in two passes, so that a tear or fold cannot pull good sections:

1. The main fit uses no annotation on a damaged section. A process that crosses one is linked directly between the sections either side, and skipped sections do not count toward the gap limit.
2. Each damaged section's own shift is then fitted from the annotations on it, against the sections either side as already corrected and held fixed. It is published only when it lowers those triples' median second difference.

If ``<VikingXML folder>/VolumeData.xml`` does not exist, the builder looks in ``<parent folder>/<volume name>/``: RC1's VikingXML is under ``RABBIT/`` and its volume files are under ``RC1/``. If the volume files cannot be read, every section is built alike, as before builder 3, and ``damaged_z`` is empty.

The field is smooth, so it cannot follow a tear or fold. From service 1.7.0, ``CorrectStructures`` in the default mode finishes the job on damaged sections. It runs the leave-one-out Catmull-Rom fit on locations on damaged sections only, using only locations on undamaged sections as fitting points. The window reaches out to seven of those on each side, so a run of damaged sections is bridged. The guards against lengthening a location's links and against overlapping its own section's neighbours still apply. Locations on undamaged sections get the field alone. On 48 cells the fit raised the mean overlap of links touching a damaged section from 0.649 to 0.728 (``docs/publication_notes.md``). The fit corrects annotation positions, for consumers that measure annotations. Viking and Jotunn shift the image by the field (``GetSectionCorrection``, ``CorrectPoints``) and do not move annotations, so this step does not apply to them.

Caching
=======

``AnswerStamp`` holds everything a ``CorrectStructures`` answer depends on: ``service_version`` (the algorithm), ``field_built_utc`` (the published field it sampled), and ``annotation_fingerprint`` (the structures' annotation rows; it changes on any insert, edit or delete). Two answers to the same request with equal stamps are identical.

To check a cached answer, send the same request to ``GetAnswerStamp`` and compare the stamp with the cached one. Re-fetch only when they differ. The response's stamp is read before its rows, so an edit made during the call shows up as a change on the next check.

``annotation_watermark`` is the newest location edit or link creation time. It is informational: it does not move when a location or link is deleted, so compare stamps, not watermarks.

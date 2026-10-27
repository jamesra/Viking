
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
     - Sample the published residual field.
   * - ``CORRECTION_MODE_NONE`` (1)
     - Do not move points: the StosGroup mapping alone.

Values 2 and 3 (``ALL`` and ``CURVE_FIT``, a leave-one-out Catmull-Rom fit along each process after the field) were removed in service 1.5.0 and are reserved. A request with either is refused with ``INVALID_ARGUMENT``. On 11 cells the fit kept the field's mean overlap of linked outlines and quadrupled the large losses. The measurements are in ``docs/publication_notes.md`` (2026-10-27). MonogameTestbed keeps the fit as an opt-in ``--correction curvefit`` or ``all`` for experiments.

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
     - Every published ``CorrectionSet`` (or one volume's): its id, ``built_utc``, ``pitch_nm``, ``kernel_radius_nm`` and the sections with a map.
   * - ``GetSectionCorrection``
     - Sparse lattice for one Z. Each ``LatticeNode`` names its cell as a ``GridCell`` ``node``; cell ``(row, col)`` sits at ``(col * pitch_nm, row * pitch_nm)``. Add its ``offset`` to a volume point.
   * - ``CorrectPoints``
     - Samples the field at points the caller holds. One ``SectionOffsets`` per request section, in request order, with one ``PointOffset`` per point. ``found`` false means that Z has no map: offsets are zero and untrusted. ``trusted`` means the point was on measured support.
   * - ``CorrectStructures``
     - Loads the locations of ``structure_ids`` (and child structures when ``include_children`` is set), maps them through the StosGroup, then applies ``correction``. Each ``CorrectedLocation`` carries the corrected ``position`` and the ``offset`` from the uncorrected one. The response carries an ``AnswerStamp``.
   * - ``GetAnswerStamp``
     - Takes the same request as ``CorrectStructures`` and returns the stamp its answer would carry now, from SQL aggregates and the set catalog alone (no volume load, no fit): well under a second.

Caching
=======

``AnswerStamp`` holds everything a ``CorrectStructures`` answer depends on: ``service_version`` (the algorithm), ``field_built_utc`` (the published field it sampled), and ``annotation_fingerprint`` (the structures' annotation rows; it changes on any insert, edit or delete). Two answers to the same request with equal stamps are identical.

To check a cached answer, send the same request to ``GetAnswerStamp`` and compare the stamp with the cached one. Re-fetch only when they differ. The response's stamp is read before its rows, so an edit made during the call shows up as a change on the next check.

``annotation_watermark`` is the newest location edit or link creation time. It is informational: it does not move when a location or link is deleted, so compare stamps, not watermarks.

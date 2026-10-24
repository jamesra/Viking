
##########################
AnnotateSectionCorrections
##########################

Source: ``gRPC_Protos/SectionCorrection/section_correction_v2.proto``

Package: ``Viking.SectionCorrectionServiceTypes.gRPC.V2``

Host: GrpcSectionCorrectionService

Same residual fields as :doc:`annotate-corrections`, for many volumes on one host. Files live under ``{root}/{volume_name}/{stos_group}/``. This service also rebuilds those files and can warp whole structures.

All positions and offsets are volume nanometers (annotation coordinates times ``AnnotateMetaData.Scale``), carried as ``Vector2``. A correction set is named by ``CorrectionSetId`` (``volume_name`` plus ``stos_group``).

Protocol versions
=================

The protocol is versioned by package name, so each version is a separate gRPC service on the same address.

* **V2** (``section_correction_v2.proto``, package ``...gRPC.V2``) is current. Viking, Jotunn, MonogameTestbed, DataExport and the service's ``correction-menu`` use it.
* **V1** (``section_correction.proto``, package ``...gRPC.V1``) is still served for installed Viking builds and SBFSEM-tools. It will be removed once both use V2. Do not write new code against it.

Both versions run on the same service code (``CorrectionServiceCore``) and give the same answers.

``CorrectionMode``:

.. list-table::
   :header-rows: 1
   :widths: 36 64

   * - Value
     - Meaning
   * - ``CORRECTION_MODE_ALL`` (0, the default)
     - Neighbor, then curve fit.
   * - ``CORRECTION_MODE_NONE``
     - Do not move points.
   * - ``CORRECTION_MODE_NEIGHBOR``
     - Sample the published residual field.
   * - ``CORRECTION_MODE_CURVE_FIT``
     - Leave-one-out Catmull-Rom along each process.

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
     - Sparse lattice for one Z. Node ``(gx, gy)`` sits at ``(gx * pitch_nm, gy * pitch_nm)``; add its ``offset`` to a volume point.
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

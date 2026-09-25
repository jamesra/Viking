
##################
AnnotateStructures
##################

Source: ``gRPCAnnotationServiceTypes/Protos/Structure.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

Host: GrpcAnnotationService

A structure is one annotated object: a cell, a process, or any other thing that owns locations. This service creates them, walks parent and link graphs, and merges or splits them. The record shapes are ``Structure`` and ``StructureLink`` on :doc:`shared-types`.

Create and fetch
================

.. list-table::
   :header-rows: 1
   :widths: 36 64

   * - RPC
     - Behavior
   * - ``CreateStructure``
     - Inserts a structure and its first location together. Both are returned with server ids. Use this when the tracer starts a new object. Adding a later location is :doc:`annotate-locations` ``CreateLocation``.
   * - ``GetStructureByID``
     - One structure by id.
   * - ``GetStructuresByID``
     - Many structures by id. Unknown ids are omitted.
   * - ``GetStructures``
     - Every structure. Prefer a narrower call on a full connectome.
   * - ``GetStructuresOfType``
     - Every structure whose ``type_id`` is the request ``id``.
   * - ``GetStructuresForSection``
     - Structures that own a location on section ``z``. Optional ``modified_after_this_utc_time`` limits the set to rows changed after that UTC time. The response includes ``deleted_ids`` and ``query_executed_time``. Pass that timestamp back as the next watermark.
   * - ``GetStructuresInMosaicRegion``
     - Same as the section call, plus a mosaic ``region`` and ``min_radius``. Locations smaller than ``min_radius`` are ignored.
   * - ``GetStructuresInVolumeRegion``
     - Same filter in volume space instead of mosaic space.
   * - ``GetChildStructures``
     - Direct children of ``structure_id`` (``Structure.parent_id``).
   * - ``NumberOfLocations``
     - How many locations the structure owns.

Region responses carry ``deleted_ids`` so a client that is polling can drop rows that disappeared after the watermark. ``query_executed_time`` is the server clock for that read. Store it and send it as ``modified_after_this_utc_time`` on the next poll.

Links and networks
==================

.. list-table::
   :header-rows: 1
   :widths: 36 64

   * - RPC
     - Behavior
   * - ``CreateStructureLink``
     - Inserts one ``StructureLink``. The type pair must be allowed by :doc:`permitted-structure-links`.
   * - ``DeleteStructureLink``
     - Removes the edge ``source_id`` to ``target_id``.
   * - ``GetLinkedStructures``
     - Links touching structure ``id``.
   * - ``GetNetworkedStructures``
     - Structure ids reachable in ``num_hops`` along structure links, starting from ``ids``.
   * - ``GetChildStructuresInNetwork``
     - Structures reached by walking ``parent_id``, ``num_hops`` down from ``ids``.
   * - ``GetStructureLinksInNetwork``
     - The link rows inside the same hop radius.
   * - ``UpdateLinks``
     - Writes a batch of ``StructureLink`` rows.

``num_hops`` is the number of edges to walk. Zero returns the seeds only.

Unfinished branches
===================

``GetUnfinishedLocations`` returns location ids that are open ends of ``structure_id`` (not marked terminal, still expected to continue on another section).

``GetUnfinishedLocationsWithPosition`` returns the same tips as ``LocationPositionOnly``: ``id``, ``position``, and ``radius``. Use this when the client only needs to draw the jump target.

Edits
=====

``Update`` takes ``StructureChangeRequest`` items. Each item is one of ``create``, ``update``, or ``delete`` (an id). Each response item has ``success`` and ``created``, ``updated``, or ``deletedId``.

``Merge`` keeps ``keep_id`` and folds ``merge_id`` into it. Locations and links move onto the kept structure. The response is ``kept_id``.

``Split`` cuts ``id`` so that locations from ``first_location_id_of_split_structure`` onward become a new structure. The response is the new structure id.

``SplitAtLocationLink`` cuts the location link between ``location_id_of_keep_structure`` and ``location_id_of_split_structure``. The second location and the branch beyond it become a new structure.

``GetStructureChangeLog`` returns structures whose rows changed. Filter with optional ``structure_id``, ``begin_time``, and ``end_time`` (UTC). Omit a bound to leave that side open.

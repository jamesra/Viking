
#################
AnnotateLocations
#################

Source: ``gRPCAnnotationServiceTypes/Protos/Location.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

Host: GrpcAnnotationService

A location is one shape on one section. This is the call the viewer uses while panning: load the annotations in the visible rectangle, then poll for changes. The record shapes are ``Location`` and ``LocationLink`` on :doc:`shared-types`.

Create, read, update
====================

.. list-table::
   :header-rows: 1
   :widths: 40 60

   * - RPC
     - Behavior
   * - ``CreateLocation``
     - Inserts one location on an existing structure (``parent_id``). Returns the stored row.
   * - ``GetLocationByID``
     - One location.
   * - ``GetLocationsByID``
     - Many locations. Unknown ids are omitted.
   * - ``GetLastModifiedLocation``
     - The location with the newest ``last_modified``. Used to see whether the server has moved since the client last looked.
   * - ``GetStructureLocations``
     - Every location owned by ``structure_id``.
   * - ``GetLocationsForSection``
     - Every location on ``section``. Includes ``query_executed_time``.
   * - ``Update``
     - A batch of creates, updates, and deletes. Each ``LocationChangeRequest`` is one of ``create``, ``update``, or ``delete`` (an id). Each response has ``success`` and ``created``, ``updated``, or ``deletedId``.

Region queries
==============

These take section ``z``, a mosaic ``Geometry`` ``region``, ``min_radius``, and an optional UTC watermark ``modified_after_this_utc_time``.

.. list-table::
   :header-rows: 1
   :widths: 42 58

   * - RPC
     - Returns
   * - ``GetLocationChangesInMosaicRegion``
     - Locations changed after the watermark, plus ``deleted_ids`` and ``query_executed_time``.
   * - ``GetAnnotationsInMosaicRegion``
     - An ``AnnotationSet`` (structures and locations for the hits), plus ``deleted_ids`` and ``query_executed_time``. This is the call that fills the canvas.
   * - ``GetLocationChanges``
     - Section-wide changes after the watermark. No geometry filter.
   * - ``StreamLocationChangesInMosaicRegion``
     - The same change query as a stream of ``LocationRegionChunk``.
   * - ``StreamAnnotationsInMosaicRegion``
     - The annotation-set query as a stream of ``AnnotationRegionChunk``.

Streaming is for a large field of view. The first chunk carries ``query_executed_time``. Location chunks put rows in ``locations``. Annotation chunks put a partial ``AnnotationSet`` in ``partial`` (locations and the parent structures for that batch). ``deleted_ids`` should be read from the chunk that sets them, preferably the last. Stop when ``is_last`` is true. Save ``query_executed_time`` and send it as the next watermark.

``min_radius`` drops shapes smaller than that radius so a zoomed-out view does not pull every punctum.

Links
=====

.. list-table::
   :header-rows: 1
   :widths: 44 56

   * - RPC
     - Behavior
   * - ``CreateLocationLink``
     - Connects ``source_id`` to ``target_id``. The two locations continue one branch across sections.
   * - ``DeleteLocationLink``
     - Removes that pair.
   * - ``GetLinkedLocations``
     - Location ids linked to ``id``.
   * - ``GetLocationLinksForSection``
     - Links for ``section``. The response has ``results``, ``deleted``, and a UTC ``query_executed_time``. The request timestamps ``modified_after_this_time`` and ``query_executed_time`` are ``int64``, and the request also carries ``deleted_links``.
   * - ``GetLocationLinksForSectionInMosaicRegion``
     - Links for ``section`` inside ``bbox``, ignoring shapes smaller than ``min_radius``. The response is ``results`` only. The request still carries ``modified_after_this_utc_time``, ``query_executed_time``, and ``deleted_links`` as ``int64`` timestamps plus link rows.

Change log
==========

``GetLocationChangeLog`` returns ``LocationHistory`` rows for ``structure_id`` between optional ``begin_time`` and ``end_time``. ``changed_column_mask`` is a bit mask of which columns the history row recorded. The mask values are the database column mask, not an enum in this proto.

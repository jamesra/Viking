
######################
AnnotateStructureTypes
######################

Source: ``gRPCAnnotationServiceTypes/Protos/StructureType.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

Host: GrpcAnnotationService

The annotation schema: cell types, processes, and the other names a tracer can assign. Viking loads the full set at startup (``GetStructureTypes``) and keeps it in memory. A missing schema means the client cannot name new structures.

The record shape is ``StructureType`` on :doc:`shared-types`.

RPCs
====

.. list-table::
   :header-rows: 1
   :widths: 32 68

   * - RPC
     - Behavior
   * - ``CreateStructureType``
     - Inserts one type and returns the stored row, including the server-assigned ``id``.
   * - ``GetStructureTypes``
     - Returns every type. This is the startup call.
   * - ``GetStructureTypeByID``
     - Returns one type. ``id`` is required.
   * - ``GetStructureTypesByIDs``
     - Returns the types for the given ids. Unknown ids are omitted.
   * - ``Update``
     - Applies a batch of creates, updates, and deletes. See below.

Updates
=======

``UpdateStructureTypesRequest`` is a list of ``StructureTypeChangeRequest``. Each item is exactly one of:

* ``create`` — a ``StructureType``
* ``update`` — a ``StructureType`` with the existing ``id``
* ``delete`` — the type id

Each ``StructureTypeChangeResponse`` has ``success`` and the matching result: ``created``, ``updated``, or ``deletedId``. A failed item does not remove the other items from the response. Check ``success`` per item.

``parent_id`` is another structure type, so parents must exist before children are created. Deleting a type that still has structures is rejected by the database.


#######################
PermittedStructureLinks
#######################

Source: ``gRPCAnnotationServiceTypes/Protos/PermittedStructureLinks.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

Host: GrpcAnnotationService

Schema rules for which structure types may be linked. ``AnnotateStructures.CreateStructureLink`` rejects a pair that is not permitted. The row shape is ``PermittedStructureLink`` on :doc:`shared-types`: ``source_type_id``, ``target_type_id``, and ``bidirectional``.

RPCs
====

.. list-table::
   :header-rows: 1
   :widths: 36 64

   * - RPC
     - Behavior
   * - ``GetPermittedStructureLinks``
     - Every rule. Clients load this with the structure types.
   * - ``CreatePermittedStructureLink``
     - Inserts one rule and returns the stored row.
   * - ``UpdatePermittedStructureLinks``
     - Applies a batch. Each change has a ``DBAction`` (``INSERT``, ``UPDATE``, or ``DELETE``) and the ``PermittedStructureLink``. Each response repeats the action, ``Sucess`` (the proto spells it that way), and the stored row when there is one.

``bidirectional`` true means the editor may create the link in either direction. The stored rule is still one row.

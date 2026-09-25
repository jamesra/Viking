
################
AnnotateMetaData
################

Source: ``gRPCAnnotationServiceTypes/Protos/AnnotationMetaData.proto``

Package: ``Viking.AnnotationServiceTypes.gRPC.V1``

Host: GrpcAnnotationService

Returns the volume scale stored with the annotation database. Correction services treat XY as volume nanometers: annotation coordinates times this scale. The lattice pitch on a correction set is a separate number and is not this scale.

RPCs
====

.. list-table::
   :header-rows: 1
   :widths: 28 72

   * - RPC
     - Behavior
   * - ``Scale``
     - No request fields. Returns ``Scale`` with ``x``, ``y``, and optional ``z`` (:doc:`shared-types`).

There is one published scale per service instance. Clients read it once when they open a volume and keep it for the session.

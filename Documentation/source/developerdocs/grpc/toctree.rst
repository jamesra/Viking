
################
gRPC protocols
################

Viking talks to three gRPC hosts. The ``.proto`` files in the ``gRPC_Protos`` submodule are the contract. Each service below has its own page.

Package ``viking.geometry.v1`` (``gRPC_Protos/Common/geometry.proto``) holds the geometry every service shares: positions, rectangles, annotation shapes, and the tile grid. It is described on :doc:`geometry`.

Package ``Viking.AnnotationServiceTypes.gRPC.V1`` (``gRPC_Protos/Annotation/``) is the annotation database, served by GrpcAnnotationService. Shared annotation messages are in ``AnnotationTypes.proto`` and are described on :doc:`shared-types`.

Package ``Viking.SectionCorrectionServiceTypes.gRPC.V2`` is the multi-volume residual-field host (GrpcSectionCorrectionService). The annotation service has no correction calls.

Package ``segmentation`` is the SAM2 segmentation server.

.. toctree::
   :maxdepth: 1

   Shared geometry <geometry>
   Shared annotation messages <shared-types>
   AnnotateMetaData <annotate-metadata>
   AnnotateStructureTypes <annotate-structure-types>
   AnnotateStructures <annotate-structures>
   AnnotateLocations <annotate-locations>
   PermittedStructureLinks <permitted-structure-links>
   AnnotateSectionCorrections <annotate-section-corrections>
   SegmentationService <segmentation>


################
gRPC protocols
################

Viking talks to three gRPC hosts. The ``.proto`` files are the contract. Each service below has its own page.

Package ``Viking.AnnotationServiceTypes.gRPC.V1`` is the annotation database, served by GrpcAnnotationService. Shared messages are in ``gRPCAnnotationServiceTypes/Protos/AnnotationTypes.proto`` and are described on :doc:`shared-types`.

Package ``Viking.SectionCorrectionServiceTypes.gRPC.V1`` is the multi-volume residual-field host (GrpcSectionCorrectionService). It is not the same service as ``AnnotateCorrections``.

Package ``segmentation`` is the SAM2 segmentation server.

.. toctree::
   :maxdepth: 1

   Shared messages <shared-types>
   AnnotateMetaData <annotate-metadata>
   AnnotateStructureTypes <annotate-structure-types>
   AnnotateStructures <annotate-structures>
   AnnotateLocations <annotate-locations>
   PermittedStructureLinks <permitted-structure-links>
   AnnotateCorrections <annotate-corrections>
   AnnotateSectionCorrections <annotate-section-corrections>
   SegmentationService <segmentation>

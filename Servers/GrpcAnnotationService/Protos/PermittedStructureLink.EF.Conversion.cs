namespace GrpcAnnotationService.Protos
{
    public static class PermitterStructureLinkEFExtensions
    {
        public static Viking.DataModel.Annotation.PermittedStructureLink ToPermittedStructureLink(this global::Viking.gRPC.AnnotationTypes.V1.Protos.PermittedStructureLink src)
        {
            var converted = new Viking.DataModel.Annotation.PermittedStructureLink
            {
                Bidirectional = src.Bidirectional,
                SourceTypeId = src.SourceTypeId,
                TargetTypeId = src.TargetTypeId
            }; 
             
            return converted;
        }


        public static global::Viking.gRPC.AnnotationTypes.V1.Protos.PermittedStructureLink ToProtobufMessage(this Viking.DataModel.Annotation.PermittedStructureLink src)
        {
            //var compositeLinks = src.LocationLinkANavigations.ToList();
            //compositeLinks.AddRange(src.LocationLinkBNavigations);

            var value = new global::Viking.gRPC.AnnotationTypes.V1.Protos.PermittedStructureLink
            {  
                SourceTypeId = src.SourceTypeId,
                TargetTypeId = src.TargetTypeId,
                Bidirectional = src.Bidirectional
            };

            return value;
        }

    }
}

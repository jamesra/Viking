namespace gRPCAnnotationService.Protos
{
    public static class StructureEFExtensions
    {
        public static Viking.DataModel.Annotation.Structure ToStructure(this global::Viking.gRPC.AnnotationTypes.V1.Protos.Structure src)
        {
            var converted = new Viking.DataModel.Annotation.Structure
            {
                Id = src.Id,
                ParentId = src.HasParentId ? src.ParentId : default,
                Confidence = src.Confidence,
                Created = src.Created.ToDateTime(),
                Label = src.Label,
                LastModified = src.LastModified.ToDateTime(),
                Notes = src.Notes,
                TypeId = src.TypeId, 
                Verified = src.Verified,
                Username = src.Username,
            }; 

            /*
            converted.LocationLinkANavigations = src.Links.Where(l => l > src.Id)
                                                                 .Select(x => new Viking.DataModel.Annotation.LocationLink() { A = src.Id, B = x })
                                                                 .ToList();
            converted.LocationLinkBNavigations = src.Links.Where(l => l < src.Id)
                                                                 .Select(x => new Viking.DataModel.Annotation.LocationLink() { A = x, B = src.Id })
                                                                 .ToList();
            */
            return converted;
        }


        public static global::Viking.gRPC.AnnotationTypes.V1.Protos.Structure ToProtobufMessage(this Viking.DataModel.Annotation.Structure src)
        {
            //var compositeLinks = src.LocationLinkANavigations.ToList();
            //compositeLinks.AddRange(src.LocationLinkBNavigations);

            var value = new global::Viking.gRPC.AnnotationTypes.V1.Protos.Structure
            {  
                Id = src.Id,
                ParentId = src.ParentId.HasValue ? src.ParentId.Value : 0,
                Confidence = src.Confidence,
                Created = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(src.Created),
                Label = src.Label,
                LastModified = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(src.LastModified),
                Notes = src.Notes,
                TypeId = src.TypeId,
                Verified = src.Verified,
                Username = src.Username,
            };

            if(false == src.ParentId.HasValue)
                value.ClearParentId();
            
            //value.Links.AddRange(compositeLinks.Select(ll => ll.A == src.Id ? ll.B : ll.A));

            return value;
        }

    }
}

namespace gRPCAnnotationService.Protos
{
    /// <summary>
    /// Structure proto ↔ EF. Links are not mapped here — StructureService.AttachStructureLinksAsync
    /// fills Structure.Links. ParentId 0 then ClearParentId so proto3 optional stays unset for roots.
    /// </summary>
    public static class StructureEFExtensions
    {
        public static Viking.DataModel.Annotation.Structure ToStructure(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure src)
        {
            var converted = new Viking.DataModel.Annotation.Structure
            {
                Id = src.Id,
                ParentId = src.HasParentId ? src.ParentId : (long?)null,
                Confidence = src.Confidence,
                // Store Save omits timestamps; ApplyUpdate stamps LastModified itself.
                Created = src.Created?.ToDateTime() ?? default,
                Label = src.Label,
                LastModified = src.LastModified?.ToDateTime() ?? default,
                Notes = src.Notes,
                TypeId = src.TypeId, 
                Verified = src.Verified,
                Username = src.Username,
                Tags = string.IsNullOrWhiteSpace(src.Attributes) ? null : src.Attributes,
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


        public static global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure ToProtobufMessage(this Viking.DataModel.Annotation.Structure src)
        {
            //var compositeLinks = src.LocationLinkANavigations.ToList();
            //compositeLinks.AddRange(src.LocationLinkBNavigations);

            var value = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Structure
            {  
                Id = src.Id,
                ParentId = src.ParentId.HasValue ? src.ParentId.Value : 0,
                Confidence = src.Confidence,
                Created = ToUtcTimestamp(src.Created),
                Label = src.Label ?? string.Empty,
                LastModified = ToUtcTimestamp(src.LastModified),
                Notes = src.Notes ?? string.Empty,
                TypeId = src.TypeId,
                Verified = src.Verified,
                Username = src.Username ?? string.Empty,
                Attributes = src.Tags ?? string.Empty,
            };

            // Proto3 optional: assigning 0 then clearing keeps HasParentId false for roots.
            if(false == src.ParentId.HasValue)
                value.ClearParentId();
            
            //value.Links.AddRange(compositeLinks.Select(ll => ll.A == src.Id ? ll.B : ll.A));

            return value;
        }

        private static Google.Protobuf.WellKnownTypes.Timestamp ToUtcTimestamp(System.DateTime dateTime) =>
            Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                System.DateTime.SpecifyKind(dateTime, System.DateTimeKind.Utc));
    }
}

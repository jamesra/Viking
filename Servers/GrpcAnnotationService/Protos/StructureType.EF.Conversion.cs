namespace gRPCAnnotationService.Protos
{
    public static class StructureTypeEFExtensions
    {
        public static Viking.DataModel.Annotation.StructureType ToStructureType(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType src)
        {
            var converted = new Viking.DataModel.Annotation.StructureType
            {
                Id = src.Id,
                // Ternary must use (long?)null — bare `default` promotes to 0L and breaks the FK.
                ParentId = src.HasParentId ? src.ParentId : (long?)null,
                Created = src.Created.ToDateTime(),
                LastModified = src.LastModified.ToDateTime(),
                Notes = src.Notes,
                Username = src.Username,
                Tags = string.IsNullOrWhiteSpace(src.Attributes) ? null : src.Attributes,
                Abstract = src.Abstract,
                Code = src.Code,
                Color = unchecked((int)src.Color),
                Name = src.Name,
                StructureTags = string.IsNullOrWhiteSpace(src.StructureAttributes) ? null : src.StructureAttributes,
                MarkupType = "Point",
                HotKey = "\0",
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

        public static void Sync(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType src,
          ref Viking.DataModel.Annotation.StructureType converted)
        {
                converted.Id = src.Id;
                converted.ParentId = src.HasParentId ? src.ParentId : (long?)null;
                converted.Created = src.Created.ToDateTime();
                converted.LastModified = src.LastModified.ToDateTime();
                converted.Notes = src.Notes;
                converted.Username = src.Username;
                converted.Tags = string.IsNullOrWhiteSpace(src.Attributes) ? null : src.Attributes;
                converted.Abstract = src.Abstract;
                converted.Code = src.Code;
                converted.Color = unchecked((int)src.Color);
                converted.Name = src.Name;
                converted.StructureTags = string.IsNullOrWhiteSpace(src.StructureAttributes) ? null : src.StructureAttributes;
        }


        public static global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType ToProtobufMessage(this Viking.DataModel.Annotation.StructureType src)
        {
            //var compositeLinks = src.LocationLinkANavigations.ToList();
            //compositeLinks.AddRange(src.LocationLinkBNavigations);

            var value = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType
            {  
                Id = src.Id,
                ParentId = src.ParentId.HasValue ? src.ParentId.Value : 0,
                Created = ToUtcTimestamp(src.Created),
                LastModified = ToUtcTimestamp(src.LastModified),
                Notes = src.Notes,
                Username = src.Username,
                Attributes = src.Tags ?? string.Empty,
                Abstract = src.Abstract,
                Code = src.Code,
                Color = unchecked((uint)src.Color),
                Name = src.Name,
                StructureAttributes = src.StructureTags ?? string.Empty,
                //Markuptype = src.MarkupType
            };

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

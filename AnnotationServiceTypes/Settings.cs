using System;
using AnnotationService.Types;
using ProtoBuf;

namespace AnnotationService.Types
{
    public static class Settings
    {
        /// <summary>
        /// Optional pre-initalization of serializers to prevent a threading deadlock in protobuf I saw once after using it for years.
        /// </summary>
        public static void PrepareSerializers()
        {
            Serializer.PrepareSerializer<Enum>();

            Serializer.PrepareSerializer<DataObject>();
            Serializer.PrepareSerializer<AnnotationSet>();

            //Geometry.cs
            Serializer.PrepareSerializer<AnnotationPoint>();
            Serializer.PrepareSerializer<BoundingBox>();
            Serializer.PrepareSerializer<BoundingRectangle>();

            Serializer.PrepareSerializer<Scale>();

            Serializer.PrepareSerializer<Location>();
            Serializer.PrepareSerializer<LocationLink>();
            Serializer.PrepareSerializer<PermittedStructureLink>();
            Serializer.PrepareSerializer<Structure>();
            Serializer.PrepareSerializer<StructureType>();
            Serializer.PrepareSerializer<StructureLink>();
            
            
        }

    }
}
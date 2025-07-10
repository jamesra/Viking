using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Geometry;

namespace VolumeModel
{
    /// <summary>
    /// Modern JSON-based serializer to replace BinaryFormatter for transform serialization
    /// </summary>
    public static class JsonTransformSerializer
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Allow polymorphic serialization for ITransform implementations
            Converters = 
            {
                new JsonStringEnumConverter(),
                new TransformJsonConverter()
            }
        };

        /// <summary>
        /// Serialize an ITransform to a stream using JSON
        /// </summary>
        public static void Serialize(Stream stream, ITransform transform)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            JsonSerializer.Serialize(stream, transform, _jsonOptions);
        }

        /// <summary>
        /// Deserialize an ITransform from a stream using JSON
        /// </summary>
        public static ITransform Deserialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return JsonSerializer.Deserialize<ITransform>(stream, _jsonOptions);
        }

        /// <summary>
        /// Serialize an array of ITransform objects to a stream using JSON
        /// </summary>
        public static void SerializeArray(Stream stream, ITransform[] transforms)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            
            if (transforms == null)
                throw new ArgumentNullException(nameof(transforms));

            JsonSerializer.Serialize(stream, transforms, _jsonOptions);
        }

        /// <summary>
        /// Deserialize an array of ITransform objects from a stream using JSON
        /// </summary>
        public static ITransform[] DeserializeArray(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            return JsonSerializer.Deserialize<ITransform[]>(stream, _jsonOptions);
        }
    }

    /// <summary>
    /// Custom JSON converter to handle polymorphic serialization of ITransform implementations
    /// </summary>
    public class TransformJsonConverter : JsonConverter<ITransform>
    {
        public override ITransform Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start of object");

            using var jsonDoc = JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;

            // Try to determine the concrete type based on available properties
            // This is a simplified approach - you may need to add more type discrimination logic
            if (root.TryGetProperty("triangleIndicies", out _))
            {
                // Likely a triangulation transform
                return JsonSerializer.Deserialize<Geometry.Transforms.TriangulationTransform>(root.GetRawText(), options);
            }
            else if (root.TryGetProperty("mapPoints", out _))
            {
                // Likely a control point transform
                return JsonSerializer.Deserialize<Geometry.Transforms.RBFTransform>(root.GetRawText(), options);
            }
            else
            {
                // Default fallback - you may need to add more specific type handling
                throw new JsonException("Unable to determine transform type from JSON");
            }
        }

        public override void Write(Utf8JsonWriter writer, ITransform value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Serialize the concrete type
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
} 
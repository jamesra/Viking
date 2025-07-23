using Geometry;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // Allow polymorphic serialization for ITransform implementations
            Converters = 
            {
                new JsonStringEnumConverter(),
                new TransformJsonConverter(),
                new ComputedPropertyJsonConverter()
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

            using(var jsonDoc = JsonDocument.ParseValue(ref reader))
            { 
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
        }

        public override void Write(Utf8JsonWriter writer, ITransform value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
             
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }


    /// <summary>
    /// Custom JSON converter that filters out computed properties (getter-only properties)
    /// </summary>
    public class ComputedPropertyJsonConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            // Only apply to types that might have computed properties
            return typeToConvert.Namespace?.StartsWith("Geometry") == true || 
                   typeToConvert.Namespace?.StartsWith("VolumeModel") == true;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var converterType = typeof(ComputedPropertyJsonConverterInner<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType);
        }

        private class ComputedPropertyJsonConverterInner<T> : JsonConverter<T>
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // For reading, we'll use the default behavior
                return JsonSerializer.Deserialize<T>(ref reader, options);
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                var type = typeof(T);
                var properties = type.GetProperties()
                    .Where(p => p.CanRead && p.CanWrite) // Only include properties with both getter and setter
                    .ToList();

                writer.WriteStartObject();

                foreach (var property in properties)
                {
                    var propertyValue = property.GetValue(value);
                    if (propertyValue != null)
                    {
                        var propertyName = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
                        writer.WritePropertyName(propertyName);
                        JsonSerializer.Serialize(writer, propertyValue, property.PropertyType, options);
                    }
                }

                writer.WriteEndObject();
            }
        }
    }
} 
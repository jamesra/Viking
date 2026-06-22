using Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VolumeModel
{
    /// <summary>
    /// Modern JSON-based serializer to replace BinaryFormatter for transform serialization
    /// </summary>
    public static class JsonTransformSerializer
    {
        // Read options must not include ComputedPropertyJsonConverter: its Read implementation
        // delegates to JsonSerializer.Deserialize with the same options and overflows the stack.
        internal static JsonSerializerOptions ReadOptions { get; } = CreateOptions(includeComputedPropertyConverter: false);

        private static readonly JsonSerializerOptions _writeJsonOptions = CreateOptions(includeComputedPropertyConverter: true);

        private static JsonSerializerOptions CreateOptions(bool includeComputedPropertyConverter)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
            };

            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new TransformJsonConverter());
            if (includeComputedPropertyConverter)
                options.Converters.Add(new ComputedPropertyJsonConverter());

            return options;
        }

        /// <summary>
        /// Serialize an ITransform to a stream using JSON
        /// </summary>
        public static void Serialize(Stream stream, ITransform transform)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            JsonSerializer.Serialize(stream, transform, _writeJsonOptions);
        }

        /// <summary>
        /// Deserialize an ITransform from a stream using JSON
        /// </summary>
        public static ITransform Deserialize(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            return JsonSerializer.Deserialize<ITransform>(stream, ReadOptions);
        }

        /// <summary>
        /// Serialize an array of ITransform objects to a stream using JSON
        /// </summary>
        public static void SerializeArray(Stream stream, ITransform[] transforms)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (transforms is null)
                throw new ArgumentNullException(nameof(transforms));

            JsonSerializer.Serialize(stream, transforms, _writeJsonOptions);
        }

        /// <summary>
        /// Deserialize an array of ITransform objects from a stream using JSON
        /// </summary>
        public static ITransform[] DeserializeArray(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            // Deserialize each element via TransformJsonConverter so ITransform[] never routes through
            // ComputedPropertyJsonConverter (write-only; would recurse if used on read).
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                throw new JsonException("Expected JSON array of transforms");

            var converter = new TransformJsonConverter();
            var results = new ITransform[root.GetArrayLength()];
            for (int i = 0; i < results.Length; i++)
            {
                byte[] elementBytes = System.Text.Encoding.UTF8.GetBytes(root[i].GetRawText());
                var reader = new Utf8JsonReader(elementBytes);
                reader.Read();
                results[i] = converter.Read(ref reader, typeof(ITransform), ReadOptions)
                    ?? throw new JsonException($"Transform at index {i} deserialized to null");
            }

            return results;
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

            using JsonDocument jsonDoc = JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;

            // Try to determine the concrete type based on available properties
            // This is a simplified approach - you may need to add more type discrimination logic
            if (root.TryGetProperty("triangleIndicies", out _))
            {
                // Likely a triangulation transform
                return JsonSerializer.Deserialize<Geometry.Transforms.TriangulationTransform>(root.GetRawText(), JsonTransformSerializer.ReadOptions);
            }
            else if (root.TryGetProperty("mapPoints", out _))
            {
                // Likely a control point transform
                return JsonSerializer.Deserialize<Geometry.Transforms.RBFTransform>(root.GetRawText(), JsonTransformSerializer.ReadOptions);
            }
            else
            {
                // Default fallback - you may need to add more specific type handling
                throw new JsonException("Unable to determine transform type from JSON");
            }
        }

        public override void Write(Utf8JsonWriter writer, ITransform value, JsonSerializerOptions options)
        {
            if (value is null)
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
            if (typeToConvert is null ||
                typeToConvert.IsArray ||
                typeToConvert.IsInterface ||
                typeToConvert.IsAbstract ||
                typeToConvert == typeof(ITransform))
            {
                return false;
            }

            // Only apply to concrete Geometry/VolumeModel types that might have computed properties
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
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException(
                    "ComputedPropertyJsonConverter is write-only. Use JsonTransformSerializer.ReadOptions for deserialization.");

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                    return;
                }

                var type = typeof(T);
                List<PropertyInfo> properties = [.. type.GetProperties().Where(p => p.CanRead && p.CanWrite)];

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
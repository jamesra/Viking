using Geometry;
using Geometry.Transforms;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VolumeModel
{
    public static class JsonTransformSerializer
    {
        internal static JsonSerializerOptions ReadOptions { get; } = CreateOptions();
        private static readonly JsonSerializerOptions _writeJsonOptions = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
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
            options.Converters.Add(new GridVector2JsonConverter());
            options.Converters.Add(new GridRectangleJsonConverter());
            options.Converters.Add(new TransformBasicInfoJsonConverter());
            options.Converters.Add(new MappingGridVector2JsonConverter());
            options.Converters.Add(new TransformJsonConverter());
            return options;
        }

        public static void Serialize(Stream stream, ITransform transform)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            using var writer = new Utf8JsonWriter(stream);
            TransformJsonConverter.WriteTransform(writer, transform, _writeJsonOptions);
            writer.Flush();
        }

        public static ITransform Deserialize(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            return JsonSerializer.Deserialize<ITransform>(stream, ReadOptions);
        }

        public static void SerializeArray(Stream stream, ITransform[] transforms)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (transforms is null)
                throw new ArgumentNullException(nameof(transforms));

            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartArray();
            foreach (ITransform transform in transforms)
                TransformJsonConverter.WriteTransform(writer, transform, _writeJsonOptions);
            writer.WriteEndArray();
            writer.Flush();
        }

        public static ITransform[] DeserializeArray(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

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

    public class TransformJsonConverter : JsonConverter<ITransform>
    {
        public override ITransform Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start of object");

            using JsonDocument jsonDoc = JsonDocument.ParseValue(ref reader);
            return DeserializeTransform(jsonDoc.RootElement);
        }

        internal static ITransform DeserializeTransform(JsonElement root)
        {
            if (root.TryGetProperty("gridSizeX", out JsonElement gridSizeXEl))
            {
                MappingGridVector2[] mapPoints = ReadMapPoints(root);
                TransformBasicInfo info = ReadInfo(root);
                GridRectangle mappedBounds = root.TryGetProperty("mappedBounds", out JsonElement mb)
                    ? GridRectangleSerialization.Read(mb)
                    : mapPoints.MappedBounds();
                return new GridTransform(mapPoints, mappedBounds, gridSizeXEl.GetInt32(),
                    root.GetProperty("gridSizeY").GetInt32(), info);
            }

            if (root.TryGetProperty("triangleIndicies", out _))
            {
                MappingGridVector2[] mapPoints = ReadMapPoints(root);
                return new MeshTransform(mapPoints, ReadInfo(root));
            }

            if (root.TryGetProperty("mapPoints", out _))
            {
                MappingGridVector2[] mapPoints = ReadMapPoints(root);
                return new RBFTransform(mapPoints, ReadInfo(root));
            }

            throw new JsonException("Unable to determine transform type from JSON");
        }

        private static MappingGridVector2[] ReadMapPoints(JsonElement root) =>
            root.GetProperty("mapPoints").Deserialize<MappingGridVector2[]>(JsonTransformSerializer.ReadOptions)
            ?? throw new JsonException("mapPoints is null");

        private static TransformBasicInfo ReadInfo(JsonElement root) =>
            TransformBasicInfoSerialization.Read(root.GetProperty("info"));

        public override void Write(Utf8JsonWriter writer, ITransform value, JsonSerializerOptions options) =>
            WriteTransform(writer, value, options);

        internal static void WriteTransform(Utf8JsonWriter writer, ITransform value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            if (value is not ReferencePointBasedTransform rbt || value is not ITransformInfo infoTransform)
                throw new JsonException($"Unsupported transform type for cache: {value.GetType().Name}");

            writer.WriteStartObject();
            writer.WritePropertyName("mapPoints");
            WriteMapPoints(writer, rbt.MapPoints);
            writer.WritePropertyName("info");
            TransformBasicInfoSerialization.Write(writer, infoTransform.Info);

            switch (value)
            {
                case GridTransform grid:
                    writer.WriteNumber("gridSizeX", grid.GridSizeX);
                    writer.WriteNumber("gridSizeY", grid.GridSizeY);
                    writer.WritePropertyName("mappedBounds");
                    GridRectangleSerialization.Write(writer, grid.MappedBounds);
                    break;
                case MeshTransform mesh:
                    writer.WritePropertyName("triangleIndicies");
                    JsonSerializer.Serialize(writer, mesh.TriangleIndicies, options);
                    break;
            }

            writer.WriteEndObject();
        }

        private static void WriteMapPoints(Utf8JsonWriter writer, MappingGridVector2[] mapPoints)
        {
            writer.WriteStartArray();
            foreach (MappingGridVector2 point in mapPoints)
                MappingGridVector2JsonConverter.WritePoint(writer, point);
            writer.WriteEndArray();
        }
    }

    internal static class GridVector2Serialization
    {
        internal static void Write(Utf8JsonWriter writer, in GridVector2 value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteEndObject();
        }

        internal static GridVector2 Read(JsonElement element)
        {
            double x = element.TryGetProperty("x", out JsonElement xEl) ? xEl.GetDouble()
                : element.GetProperty("X").GetDouble();
            double y = element.TryGetProperty("y", out JsonElement yEl) ? yEl.GetDouble()
                : element.GetProperty("Y").GetDouble();
            return new GridVector2(x, y);
        }

        internal static GridVector2 Read(ref Utf8JsonReader reader)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            return Read(doc.RootElement);
        }
    }

    internal sealed class GridVector2JsonConverter : JsonConverter<GridVector2>
    {
        public override GridVector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            GridVector2Serialization.Read(ref reader);

        public override void Write(Utf8JsonWriter writer, GridVector2 value, JsonSerializerOptions options) =>
            GridVector2Serialization.Write(writer, in value);
    }

    internal static class GridRectangleSerialization
    {
        internal static void Write(Utf8JsonWriter writer, in GridRectangle value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("left", value.Left);
            writer.WriteNumber("right", value.Right);
            writer.WriteNumber("bottom", value.Bottom);
            writer.WriteNumber("top", value.Top);
            writer.WriteEndObject();
        }

        internal static GridRectangle Read(JsonElement element)
        {
            return new GridRectangle(
                element.GetProperty("left").GetDouble(),
                element.GetProperty("right").GetDouble(),
                element.GetProperty("bottom").GetDouble(),
                element.GetProperty("top").GetDouble());
        }
    }

    internal sealed class GridRectangleJsonConverter : JsonConverter<GridRectangle>
    {
        public override GridRectangle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            return GridRectangleSerialization.Read(doc.RootElement);
        }

        public override void Write(Utf8JsonWriter writer, GridRectangle value, JsonSerializerOptions options) =>
            GridRectangleSerialization.Write(writer, in value);
    }

    internal static class TransformBasicInfoSerialization
    {
        internal static void Write(Utf8JsonWriter writer, TransformBasicInfo info)
        {
            writer.WriteStartObject();
            switch (info)
            {
                case TileTransformInfo tile:
                    writer.WriteString("infoType", "tile");
                    writer.WriteString("tileFileName", tile.TileFileName);
                    writer.WriteNumber("tileNumber", tile.TileNumber);
                    writer.WriteNumber("imageWidth", tile.ImageWidth);
                    writer.WriteNumber("imageHeight", tile.ImageHeight);
                    writer.WriteString("lastModified", tile.LastModified.ToString("O"));
                    break;
                case StosTransformInfo stos:
                    writer.WriteString("infoType", "stos");
                    writer.WriteNumber("controlSection", stos.ControlSection);
                    writer.WriteNumber("mappedSection", stos.MappedSection);
                    writer.WriteString("lastModified", stos.LastModified.ToString("O"));
                    break;
                default:
                    writer.WriteString("infoType", "basic");
                    writer.WriteString("lastModified", info.LastModified.ToString("O"));
                    break;
            }
            writer.WriteEndObject();
        }

        internal static TransformBasicInfo Read(JsonElement element)
        {
            string infoType = element.TryGetProperty("infoType", out JsonElement typeEl)
                ? typeEl.GetString()
                : "basic";
            DateTime lastModified = element.TryGetProperty("lastModified", out JsonElement lmEl)
                ? DateTime.Parse(lmEl.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind)
                : DateTime.MinValue;

            return infoType switch
            {
                "tile" => new TileTransformInfo(
                    element.GetProperty("tileFileName").GetString(),
                    element.GetProperty("tileNumber").GetInt32(),
                    lastModified,
                    element.GetProperty("imageWidth").GetDouble(),
                    element.GetProperty("imageHeight").GetDouble()),
                "stos" => new StosTransformInfo(
                    element.GetProperty("controlSection").GetInt32(),
                    element.GetProperty("mappedSection").GetInt32(),
                    lastModified),
                _ => new TransformBasicInfo(lastModified),
            };
        }
    }

    internal sealed class TransformBasicInfoJsonConverter : JsonConverter<TransformBasicInfo>
    {
        public override TransformBasicInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            return TransformBasicInfoSerialization.Read(doc.RootElement);
        }

        public override void Write(Utf8JsonWriter writer, TransformBasicInfo value, JsonSerializerOptions options) =>
            TransformBasicInfoSerialization.Write(writer, value);
    }

    internal sealed class MappingGridVector2JsonConverter : JsonConverter<MappingGridVector2>
    {
        public override MappingGridVector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected start of object for MappingGridVector2");

            GridVector2 control = default;
            GridVector2 mapped = default;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected property name");

                string name = reader.GetString();
                reader.Read();
                if (string.Equals(name, "controlPoint", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "control", StringComparison.OrdinalIgnoreCase))
                {
                    control = GridVector2Serialization.Read(ref reader);
                }
                else if (string.Equals(name, "mappedPoint", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(name, "mapped", StringComparison.OrdinalIgnoreCase))
                {
                    mapped = GridVector2Serialization.Read(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }

            return new MappingGridVector2(control, mapped);
        }

        public override void Write(Utf8JsonWriter writer, MappingGridVector2 value, JsonSerializerOptions options) =>
            WritePoint(writer, value);

        internal static void WritePoint(Utf8JsonWriter writer, MappingGridVector2 value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("controlPoint");
            GridVector2Serialization.Write(writer, value.ControlPoint);
            writer.WritePropertyName("mappedPoint");
            GridVector2Serialization.Write(writer, value.MappedPoint);
            writer.WriteEndObject();
        }
    }
}

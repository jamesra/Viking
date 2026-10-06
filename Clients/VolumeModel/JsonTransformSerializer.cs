using Geometry;
using Geometry.Transforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VolumeModel
{
    /// <summary>
    /// Reads and writes the on-disk transform caches: parsed mosaic tiles (TilesToSectionMapping) and warped tiles
    /// (SectionToVolumeMapping). Files older than <c>Global.OldestValidCachedTransform</c> must be rebuilt.
    /// </summary>
    /// <remarks>
    /// <para>Format version 2, written by hand with <see cref="Utf8JsonWriter"/> so nothing depends on reflection over the
    /// transform classes (the version 1 reflection serializer wrote <c>{}</c> for every transform):</para>
    /// <code>{"format":"viking-transform-cache","version":2,"transforms":[
    ///   {"type":"grid","info":{...},"gridSizeX":n,"gridSizeY":n,"mappedBounds":[left,right,bottom,top],"points":[cx,cy,mx,my,...]},
    ///   {"type":"mesh","info":{...},"points":[...]},
    ///   {"type":"rbf","info":{...},"points":[...]} ]}</code>
    /// <para>Doubles are written in round-trip form and points in <c>MapPoints</c> order, and each transform is rebuilt
    /// with the constructor that built it, so a loaded transform maps exactly like the one that was saved. Mesh
    /// triangulations and RBF weights are recomputed on first use, as they are for a freshly built transform.</para>
    /// <para>Only <see cref="GridTransform"/>, <see cref="MeshTransform"/> and <see cref="RBFTransform"/> are supported;
    /// writing anything else throws <see cref="NotSupportedException"/>. Reading anything other than version 2, including
    /// version 1 files, throws <see cref="JsonException"/>; callers treat that as a stale cache and rebuild it.</para>
    /// <para>Stateless and thread safe. Callers own the streams.</para>
    /// </remarks>
    public static class JsonTransformSerializer
    {
        private const string FormatName = "viking-transform-cache";
        private const int FormatVersion = 2;

        private const string GridType = "grid";
        private const string MeshType = "mesh";
        private const string RbfType = "rbf";

        /// <summary>Writes one transform as a cache file with a single entry.</summary>
        public static void Serialize(Stream stream, ITransform transform)
        {
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));
            SerializeArray(stream, [transform]);
        }

        /// <summary>Reads a cache file written by <see cref="Serialize"/> and returns its only transform.</summary>
        public static ITransform Deserialize(Stream stream)
        {
            ITransform[] transforms = DeserializeArray(stream);
            if (transforms.Length != 1)
                throw new JsonException($"Expected one transform, found {transforms.Length}");
            return transforms[0];
        }

        /// <summary>Writes <paramref name="transforms"/> in order. Throws <see cref="NotSupportedException"/> for an unsupported transform type before anything is written.</summary>
        public static void SerializeArray(Stream stream, ITransform[] transforms)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (transforms is null)
                throw new ArgumentNullException(nameof(transforms));

            foreach (ITransform t in transforms)
                _ = TypeName(t);

            using Utf8JsonWriter writer = new(stream);
            writer.WriteStartObject();
            writer.WriteString("format", FormatName);
            writer.WriteNumber("version", FormatVersion);
            writer.WriteStartArray("transforms");
            foreach (ITransform t in transforms)
                WriteTransform(writer, t);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }

        /// <summary>Reads every transform in a version 2 cache file, in the order they were written.</summary>
        public static ITransform[] DeserializeArray(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            byte[] bytes;
            using (MemoryStream buffer = new())
            {
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }

            Utf8JsonReader reader = new(bytes);
            Expect(ref reader, JsonTokenType.StartObject);

            string format = null;
            int version = 0;
            List<ITransform> transforms = null;
            while (Next(ref reader) == JsonTokenType.PropertyName)
            {
                string name = reader.GetString();
                reader.Read();
                switch (name)
                {
                    case "format": format = reader.GetString(); break;
                    case "version": version = reader.GetInt32(); break;
                    case "transforms":
                        if (format != FormatName || version != FormatVersion)
                            throw new JsonException($"Unsupported transform cache format '{format}' version {version}");
                        transforms = ReadTransforms(ref reader);
                        break;
                    default: reader.Skip(); break;
                }
            }

            if (format != FormatName || version != FormatVersion || transforms is null)
                throw new JsonException($"Not a version {FormatVersion} transform cache");

            return [.. transforms];
        }

        private static string TypeName(ITransform t) => t switch
        {
            null => throw new NotSupportedException("Null transform in cache"),
            GridTransform => GridType,
            MeshTransform => MeshType,
            RBFTransform => RbfType,
            _ => throw new NotSupportedException($"Unsupported transform type for cache: {t.GetType().Name}"),
        };

        private static void WriteTransform(Utf8JsonWriter writer, ITransform t)
        {
            ReferencePointBasedTransform points = (ReferencePointBasedTransform)t;
            writer.WriteStartObject();
            writer.WriteString("type", TypeName(t));
            writer.WritePropertyName("info");
            WriteInfo(writer, ((ITransformInfo)t).Info);

            if (t is GridTransform grid)
            {
                writer.WriteNumber("gridSizeX", grid.GridSizeX);
                writer.WriteNumber("gridSizeY", grid.GridSizeY);
                writer.WriteStartArray("mappedBounds");
                writer.WriteNumberValue(grid.MappedBounds.Left);
                writer.WriteNumberValue(grid.MappedBounds.Right);
                writer.WriteNumberValue(grid.MappedBounds.Bottom);
                writer.WriteNumberValue(grid.MappedBounds.Top);
                writer.WriteEndArray();
            }

            writer.WriteStartArray("points");
            foreach (MappingVector2 p in points.MapPoints)
            {
                writer.WriteNumberValue(p.ControlPoint.X);
                writer.WriteNumberValue(p.ControlPoint.Y);
                writer.WriteNumberValue(p.MappedPoint.X);
                writer.WriteNumberValue(p.MappedPoint.Y);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteInfo(Utf8JsonWriter writer, TransformBasicInfo info)
        {
            writer.WriteStartObject();
            switch (info)
            {
                case TileTransformInfo tile:
                    writer.WriteString("kind", "tile");
                    writer.WriteString("tileFileName", tile.TileFileName);
                    writer.WriteNumber("tileNumber", tile.TileNumber);
                    writer.WriteNumber("imageWidth", tile.ImageWidth);
                    writer.WriteNumber("imageHeight", tile.ImageHeight);
                    break;
                case StosTransformInfo stos:
                    writer.WriteString("kind", "stos");
                    writer.WriteNumber("controlSection", stos.ControlSection);
                    writer.WriteNumber("mappedSection", stos.MappedSection);
                    break;
                default:
                    writer.WriteString("kind", "basic");
                    break;
            }

            //ToBinary keeps the DateTimeKind, which cache validity checks depend on
            writer.WriteNumber("lastModified", (info ?? new TransformBasicInfo()).LastModified.ToBinary());
            writer.WriteEndObject();
        }

        private static List<ITransform> ReadTransforms(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected the transforms array");

            List<ITransform> transforms = [];
            while (Next(ref reader) == JsonTokenType.StartObject)
                transforms.Add(ReadTransform(ref reader));
            return transforms;
        }

        private static ITransform ReadTransform(ref Utf8JsonReader reader)
        {
            string type = null;
            TransformBasicInfo info = null;
            int gridSizeX = 0, gridSizeY = 0;
            Rectangle? mappedBounds = null;
            MappingVector2[] points = null;

            while (Next(ref reader) == JsonTokenType.PropertyName)
            {
                string name = reader.GetString();
                reader.Read();
                switch (name)
                {
                    case "type": type = reader.GetString(); break;
                    case "info": info = ReadInfo(ref reader); break;
                    case "gridSizeX": gridSizeX = reader.GetInt32(); break;
                    case "gridSizeY": gridSizeY = reader.GetInt32(); break;
                    case "mappedBounds":
                        double[] b = ReadDoubles(ref reader);
                        if (b.Length != 4)
                            throw new JsonException("mappedBounds needs 4 values");
                        mappedBounds = new Rectangle(b[0], b[1], b[2], b[3]);
                        break;
                    case "points": points = ReadPoints(ref reader); break;
                    default: reader.Skip(); break;
                }
            }

            if (points is null || info is null)
                throw new JsonException("Transform entry needs points and info");

            return type switch
            {
                GridType when mappedBounds.HasValue => new GridTransform(points, mappedBounds.Value, gridSizeX, gridSizeY, info),
                MeshType => new MeshTransform(points, info),
                RbfType => new RBFTransform(points, info),
                _ => throw new JsonException($"Unknown or incomplete transform entry of type '{type}'"),
            };
        }

        private static TransformBasicInfo ReadInfo(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected the info object");

            string kind = "basic", tileFileName = null;
            int tileNumber = 0, controlSection = 0, mappedSection = 0;
            double imageWidth = 0, imageHeight = 0;
            long lastModified = DateTime.MinValue.ToBinary();

            while (Next(ref reader) == JsonTokenType.PropertyName)
            {
                string name = reader.GetString();
                reader.Read();
                switch (name)
                {
                    case "kind": kind = reader.GetString(); break;
                    case "tileFileName": tileFileName = reader.GetString(); break;
                    case "tileNumber": tileNumber = reader.GetInt32(); break;
                    case "imageWidth": imageWidth = reader.GetDouble(); break;
                    case "imageHeight": imageHeight = reader.GetDouble(); break;
                    case "controlSection": controlSection = reader.GetInt32(); break;
                    case "mappedSection": mappedSection = reader.GetInt32(); break;
                    case "lastModified": lastModified = reader.GetInt64(); break;
                    default: reader.Skip(); break;
                }
            }

            DateTime modified = DateTime.FromBinary(lastModified);
            return kind switch
            {
                "tile" => new TileTransformInfo(tileFileName, tileNumber, modified, imageWidth, imageHeight),
                "stos" => new StosTransformInfo(controlSection, mappedSection, modified),
                _ => new TransformBasicInfo(modified),
            };
        }

        private static MappingVector2[] ReadPoints(ref Utf8JsonReader reader)
        {
            double[] values = ReadDoubles(ref reader);
            if (values.Length % 4 != 0)
                throw new JsonException("points must hold groups of four values");

            MappingVector2[] points = new MappingVector2[values.Length / 4];
            for (int i = 0; i < points.Length; i++)
            {
                int v = i * 4;
                points[i] = new MappingVector2(new Vector2(values[v], values[v + 1]), new Vector2(values[v + 2], values[v + 3]));
            }
            return points;
        }

        private static double[] ReadDoubles(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected an array of numbers");

            List<double> values = [];
            while (Next(ref reader) == JsonTokenType.Number)
                values.Add(reader.GetDouble());

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("Expected only numbers in the array");
            return [.. values];
        }

        private static JsonTokenType Next(ref Utf8JsonReader reader) =>
            reader.Read() ? reader.TokenType : throw new JsonException("Unexpected end of transform cache");

        private static void Expect(ref Utf8JsonReader reader, JsonTokenType token)
        {
            if (Next(ref reader) != token)
                throw new JsonException($"Expected {token}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// Four result sets from [dbo].[SelectSectionAnnotationsInMosaicBounds].
    /// Location links are attached onto Location navigations so ToProtobufMessage fills Location.Links.
    /// </summary>
    public sealed class SectionAnnotationsInMosaicBoundsResult
    {
        public List<Structure> Structures { get; } = new();

        public List<StructureLink> StructureLinks { get; } = new();

        public List<Location> Locations { get; } = new();

        public List<LocationLink> LocationLinks { get; } = new();
    }

    public partial class AnnotationContextProcedures
    {
        static readonly SqlServerBytesWriter GeometryWriter = new();
        static readonly SqlServerBytesReader GeometryReader = new();

        /// <summary>
        /// Executes the WCF mosaic-region stored procedure with a SQL geometry bbox.
        /// Reads all four result sets via ADO.NET so circle CurvePolygons are not rewritten
        /// by SqlServerCircleShapeCommandInterceptor. QueryDate null is a full cell load.
        /// </summary>
        public async Task<SectionAnnotationsInMosaicBoundsResult> SelectSectionAnnotationsInMosaicBoundsFullAsync(
            double z,
            NtsGeometry bbox,
            double minRadius,
            DateTime? queryDate,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(bbox);

            var result = new SectionAnnotationsInMosaicBoundsResult();
            var connection = _context.Database.GetDbConnection();
            var openedHere = false;
            if (connection.State != ConnectionState.Open)
            {
                await _context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                openedHere = true;
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[SelectSectionAnnotationsInMosaicBounds]";
                command.CommandType = CommandType.StoredProcedure;
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                if (_context.Database.GetCommandTimeout() is int timeout)
                    command.CommandTimeout = timeout;

                AddParameter(command, "@Z", SqlDbType.Float, z);
                AddGeometryParameter(command, "@BBox", bbox);
                AddParameter(command, "@MinRadius", SqlDbType.Float, minRadius);
                AddParameter(command, "@QueryDate", SqlDbType.DateTime, queryDate.HasValue ? queryDate.Value : DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    result.Structures.Add(ReadStructure(reader));

                if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        result.StructureLinks.Add(ReadStructureLink(reader));
                }

                if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        result.Locations.Add(ReadLocation(reader));
                }

                if (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        result.LocationLinks.Add(ReadLocationLink(reader));
                }
            }
            finally
            {
                if (openedHere)
                    await _context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }

            AppendLinksToLocations(result.Locations, result.LocationLinks);
            return result;
        }

        static void AppendLinksToLocations(IReadOnlyList<Location> locations, IReadOnlyList<LocationLink> links)
        {
            if (locations.Count == 0 || links.Count == 0)
                return;

            var byId = new Dictionary<long, Location>(locations.Count);
            foreach (var location in locations)
                byId[location.Id] = location;

            foreach (var link in links)
            {
                if (byId.TryGetValue(link.A, out var a))
                    a.LocationLinkANavigations.Add(link);
                if (byId.TryGetValue(link.B, out var b))
                    b.LocationLinkBNavigations.Add(link);
            }
        }

        static void AddParameter(DbCommand command, string name, SqlDbType type, object value)
        {
            var parameter = new SqlParameter(name, type) { Value = value ?? DBNull.Value };
            command.Parameters.Add(parameter);
        }

        static void AddGeometryParameter(DbCommand command, string name, NtsGeometry geometry)
        {
            var parameter = new SqlParameter(name, SqlDbType.Udt)
            {
                UdtTypeName = "geometry",
                Value = GeometryWriter.Write(geometry)
            };
            command.Parameters.Add(parameter);
        }

        static Structure ReadStructure(DbDataReader reader)
        {
            return new Structure
            {
                Id = reader.GetInt64(reader.GetOrdinal("ID")),
                TypeId = reader.GetInt64(reader.GetOrdinal("TypeID")),
                Notes = GetStringOrNull(reader, "Notes"),
                Verified = reader.GetBoolean(reader.GetOrdinal("Verified")),
                Tags = GetStringOrNull(reader, "Tags"),
                Confidence = reader.GetDouble(reader.GetOrdinal("Confidence")),
                Version = GetBytes(reader, "Version") ?? Array.Empty<byte>(),
                ParentId = GetNullableInt64(reader, "ParentID"),
                Created = reader.GetDateTime(reader.GetOrdinal("Created")),
                Label = GetStringOrNull(reader, "Label"),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                LastModified = reader.GetDateTime(reader.GetOrdinal("LastModified"))
            };
        }

        static StructureLink ReadStructureLink(DbDataReader reader)
        {
            return new StructureLink
            {
                SourceId = reader.GetInt64(reader.GetOrdinal("SourceID")),
                TargetId = reader.GetInt64(reader.GetOrdinal("TargetID")),
                Bidirectional = reader.GetBoolean(reader.GetOrdinal("Bidirectional")),
                Tags = GetStringOrNull(reader, "Tags"),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                Created = reader.GetDateTime(reader.GetOrdinal("Created")),
                LastModified = reader.GetDateTime(reader.GetOrdinal("LastModified"))
            };
        }

        static Location ReadLocation(DbDataReader reader)
        {
            var typeCode = reader.GetInt16(reader.GetOrdinal("TypeCode"));
            var x = reader.GetDouble(reader.GetOrdinal("X"));
            var y = reader.GetDouble(reader.GetOrdinal("Y"));
            var volumeX = reader.GetDouble(reader.GetOrdinal("VolumeX"));
            var volumeY = reader.GetDouble(reader.GetOrdinal("VolumeY"));
            var mosaicShape = typeCode == SqlServerCircleShapeCommandInterceptor.CircleTypeCode
                ? (NtsGeometry)new Point(x, y)
                : ReadGeometry(reader, "MosaicShape") ?? new Point(x, y);
            var volumeShape = typeCode == SqlServerCircleShapeCommandInterceptor.CircleTypeCode
                ? (NtsGeometry)new Point(volumeX, volumeY)
                : ReadGeometry(reader, "VolumeShape") ?? new Point(volumeX, volumeY);

            return new Location
            {
                Id = reader.GetInt64(reader.GetOrdinal("ID")),
                ParentId = reader.GetInt64(reader.GetOrdinal("ParentID")),
                Z = reader.GetInt64(reader.GetOrdinal("Z")),
                Closed = reader.GetBoolean(reader.GetOrdinal("Closed")),
                Version = GetBytes(reader, "Version") ?? Array.Empty<byte>(),
                Overlay = GetBytes(reader, "Overlay"),
                Tags = GetStringOrNull(reader, "Tags"),
                Terminal = reader.GetBoolean(reader.GetOrdinal("Terminal")),
                OffEdge = reader.GetBoolean(reader.GetOrdinal("OffEdge")),
                TypeCode = typeCode,
                LastModified = reader.GetDateTime(reader.GetOrdinal("LastModified")),
                Created = reader.GetDateTime(reader.GetOrdinal("Created")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                MosaicShape = mosaicShape,
                VolumeShape = volumeShape,
                X = x,
                Y = y,
                VolumeX = volumeX,
                VolumeY = volumeY,
                Width = GetNullableDouble(reader, "Width"),
                Radius = reader.GetDouble(reader.GetOrdinal("Radius"))
            };
        }

        static LocationLink ReadLocationLink(DbDataReader reader)
        {
            return new LocationLink
            {
                A = reader.GetInt64(reader.GetOrdinal("A")),
                B = reader.GetInt64(reader.GetOrdinal("B")),
                Username = reader.GetString(reader.GetOrdinal("Username")),
                Created = reader.GetDateTime(reader.GetOrdinal("Created"))
            };
        }

        static NtsGeometry ReadGeometry(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
                return null;

            try
            {
                var raw = reader.GetValue(ordinal);
                byte[] bytes = raw switch
                {
                    byte[] b => b,
                    SqlBytes sqlBytes => sqlBytes.Value,
                    _ => null
                };
                if (bytes == null || bytes.Length == 0)
                    return null;
                return GeometryReader.Read(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string GetStringOrNull(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        static long? GetNullableInt64(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        static double? GetNullableDouble(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
        }

        static byte[] GetBytes(DbDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
                return null;
            return reader.GetFieldValue<byte[]>(ordinal);
        }
    }
}

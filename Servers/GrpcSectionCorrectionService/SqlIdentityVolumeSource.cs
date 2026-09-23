using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Lists Identity Resource rows with ResourceTypeId Volume. Reads Name + Endpoint (VikingXML).
    /// AnnotationEndpoint is selected when the column exists; otherwise left empty.
    /// </summary>
    public sealed class SqlIdentityVolumeSource : IIdentityVolumeSource
    {
        readonly string _connectionString;

        public SqlIdentityVolumeSource(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                return [];

            List<IdentityVolumeRow> rows = [];
            await using SqlConnection connection = new(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            bool hasAnnotation = await ColumnExistsAsync(connection, "Resource", "AnnotationEndpoint", cancellationToken)
                .ConfigureAwait(false);
            string sql = hasAnnotation
                ? "SELECT Name, Endpoint, AnnotationEndpoint FROM Resource WHERE ResourceTypeId = N'Volume'"
                : "SELECT Name, Endpoint FROM Resource WHERE ResourceTypeId = N'Volume'";

            await using SqlCommand command = new(sql, connection);
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string name = reader.IsDBNull(0) ? null : reader.GetString(0);
                string endpoint = reader.IsDBNull(1) ? null : reader.GetString(1);
                string annotation = hasAnnotation && !reader.IsDBNull(2) ? reader.GetString(2) : null;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                rows.Add(new IdentityVolumeRow(name, endpoint, annotation));
            }

            return rows;
        }

        static async Task<bool> ColumnExistsAsync(
            SqlConnection connection,
            string table,
            string column,
            CancellationToken cancellationToken)
        {
            const string sql =
                "SELECT 1 FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id " +
                "WHERE t.name = @table AND c.name = @column";
            await using SqlCommand command = new(sql, connection);
            command.Parameters.AddWithValue("@table", table);
            command.Parameters.AddWithValue("@column", column);
            object value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is not null && value is not DBNull;
        }
    }
}

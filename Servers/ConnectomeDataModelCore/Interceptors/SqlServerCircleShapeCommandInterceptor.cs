using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Viking.DataModel.Annotation;

/// <summary>
/// Viking stores circles as SQL Server CurvePolygon. NetTopologySuite cannot deserialize
/// that type, so SELECT/OUTPUT of MosaicShape/VolumeShape is replaced with a centroid
/// POINT when TypeCode is Circle (1). Callers reconstruct the circle from X/Y/Radius.
/// A POINT is used instead of NULL because MosaicShape/VolumeShape are required NTS
/// properties. Writes are left unchanged.
/// </summary>
public sealed class SqlServerCircleShapeCommandInterceptor : DbCommandInterceptor
{
    public const short CircleTypeCode = 1;

    public static readonly SqlServerCircleShapeCommandInterceptor Instance = new();

    private static readonly Regex QualifiedShapeColumn = new(
        @"(?<!ELSE )\[(?<qual>[^\]]+)\]\.\[(?<col>MosaicShape|VolumeShape)\](?!\s*=)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private SqlServerCircleShapeCommandInterceptor()
    {
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        SubstituteCircleShapeColumns(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        SubstituteCircleShapeColumns(command);
        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    /// <summary>
    /// Rewrites qualified MosaicShape/VolumeShape reads to a centroid POINT when TypeCode is Circle.
    /// Used by tests and by the interceptor before the command runs.
    /// </summary>
    public static string NullCircleShapeColumns(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return sql;

        return QualifiedShapeColumn.Replace(sql, match =>
        {
            var qual = match.Groups["qual"].Value;
            var col = match.Groups["col"].Value;
            var x = col == "VolumeShape" ? "VolumeX" : "X";
            var y = col == "VolumeShape" ? "VolumeY" : "Y";
            return $"(CASE WHEN [{qual}].[TypeCode] = 1 THEN geometry::Point([{qual}].[{x}], [{qual}].[{y}], 0) ELSE [{qual}].[{col}] END)";
        });
    }

    private static void SubstituteCircleShapeColumns(DbCommand command)
    {
        command.CommandText = NullCircleShapeColumns(command.CommandText);
    }
}

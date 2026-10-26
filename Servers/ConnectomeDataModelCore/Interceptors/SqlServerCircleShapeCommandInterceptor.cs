using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Viking.DataModel.Annotation;

/// <summary>
/// Viking stores circles as SQL Server CurvePolygon. NetTopologySuite cannot deserialize
/// that type, so SELECT/OUTPUT of MosaicShape/VolumeShape is replaced with the circle's
/// east rim POINT when TypeCode is Circle (1): (X + r, Y) for the mosaic shape and
/// (VolumeX + r, VolumeY) for the volume shape, r being that shape's own radius
/// (sqrt(area / pi), as the Radius column computes it for the mosaic shape). Callers rebuild
/// each circle from its centre and the distance to the rim point, so the volume circle keeps
/// its own radius. Writes are left unchanged.
/// </summary>
public sealed class SqlServerCircleShapeCommandInterceptor : DbCommandInterceptor
{
    public const short CircleTypeCode = 1;

    public static readonly SqlServerCircleShapeCommandInterceptor Instance = new();

    private static readonly Regex QualifiedShapeColumn = new(
        @"(?<!ELSE )\[(?<qual>[^\]]+)\]\.\[(?<col>MosaicShape|VolumeShape)\](?!\s*=)(?!\.)",
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
    /// Rewrites qualified MosaicShape/VolumeShape reads to the east rim POINT when TypeCode is Circle.
    /// A column followed by a method call (<c>.STArea()</c>) is left alone, so the rewrite is idempotent.
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
            return $"(CASE WHEN [{qual}].[TypeCode] = 1 THEN geometry::Point([{qual}].[{x}] + SQRT([{qual}].[{col}].STArea() / PI()), [{qual}].[{y}], 0) ELSE [{qual}].[{col}] END)";
        });
    }

    private static void SubstituteCircleShapeColumns(DbCommand command)
    {
        command.CommandText = NullCircleShapeColumns(command.CommandText);
    }
}

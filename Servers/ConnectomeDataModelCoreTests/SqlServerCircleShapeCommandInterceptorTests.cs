using Viking.DataModel.Annotation;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    public class SqlServerCircleShapeCommandInterceptorTests
    {
        [Fact]
        public void NullCircleShapeColumns_RewritesQualifiedSelectColumns()
        {
            const string sql =
                "SELECT [l].[ID], [l].[MosaicShape], [l].[VolumeShape] FROM [Location] AS [l]";

            var rewritten = SqlServerCircleShapeCommandInterceptor.NullCircleShapeColumns(sql);

            Assert.Equal(
                "SELECT [l].[ID], (CASE WHEN [l].[TypeCode] = 1 THEN geometry::Point([l].[X], [l].[Y], 0) ELSE [l].[MosaicShape] END), (CASE WHEN [l].[TypeCode] = 1 THEN geometry::Point([l].[VolumeX], [l].[VolumeY], 0) ELSE [l].[VolumeShape] END) FROM [Location] AS [l]",
                rewritten);
        }

        [Fact]
        public void NullCircleShapeColumns_RewritesOutputInsertedColumns()
        {
            const string sql = "OUTPUT [INSERTED].[MosaicShape], [INSERTED].[VolumeShape]";

            var rewritten = SqlServerCircleShapeCommandInterceptor.NullCircleShapeColumns(sql);

            Assert.Equal(
                "OUTPUT (CASE WHEN [INSERTED].[TypeCode] = 1 THEN geometry::Point([INSERTED].[X], [INSERTED].[Y], 0) ELSE [INSERTED].[MosaicShape] END), (CASE WHEN [INSERTED].[TypeCode] = 1 THEN geometry::Point([INSERTED].[VolumeX], [INSERTED].[VolumeY], 0) ELSE [INSERTED].[VolumeShape] END)",
                rewritten);
        }

        [Fact]
        public void NullCircleShapeColumns_LeavesUnqualifiedWriteColumnsAlone()
        {
            const string sql = "UPDATE [Location] SET [MosaicShape] = @p0, [VolumeShape] = @p1 WHERE [ID] = @p2";

            var rewritten = SqlServerCircleShapeCommandInterceptor.NullCircleShapeColumns(sql);

            Assert.Equal(sql, rewritten);
        }

        [Fact]
        public void NullCircleShapeColumns_IsIdempotent()
        {
            const string sql =
                "SELECT [l].[ID], [l].[MosaicShape], [l].[VolumeShape] FROM [Location] AS [l]";

            var once = SqlServerCircleShapeCommandInterceptor.NullCircleShapeColumns(sql);
            var twice = SqlServerCircleShapeCommandInterceptor.NullCircleShapeColumns(once);

            Assert.Equal(once, twice);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// My custom overrides to the AnnotationContext model
    /// </summary>
    public partial class AnnotationContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(SqlServerCircleShapeCommandInterceptor.Instance);
        }

        /// <summary>
        /// Writes circle CURVEPOLYGON WKT through SQL because NTS cannot persist CurvePolygon.
        /// Called after SaveChanges so identity ID is assigned. Reloads computed X/Y/Radius.
        /// </summary>
        public async Task PersistCircleShapesAsync(
            Location location,
            string mosaicWkt,
            string volumeWkt,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mosaicWkt);
            ArgumentException.ThrowIfNullOrWhiteSpace(volumeWkt);

            await Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Location] SET [MosaicShape] = geometry::STGeomFromText({mosaicWkt}, 0), [VolumeShape] = geometry::STGeomFromText({volumeWkt}, 0) WHERE [ID] = {location.Id}",
                cancellationToken);

            await Entry(location).ReloadAsync(cancellationToken);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // Generated TVF/result types — must be keyless. This method lives in
            // AnnotationContext.Functions.cs but was never wired into OnModelCreating.
            OnModelCreatingGeneratedFunctions(modelBuilder);

            // SQL has UNIQUE(SourceID, TargetID) but no PK; EF needs a key to track inserts.
            modelBuilder.Entity<StructureLink>(entity =>
            {
                entity.HasKey(e => new { e.SourceId, e.TargetId });
            });

            modelBuilder.Entity<Location>(entity =>
            {
                entity.Property(e => e.VolumeShape)
                    .HasColumnType("geometry");

                entity.Property(e => e.MosaicShape)
                    .HasColumnType("geometry");
            });
        }
    }
}

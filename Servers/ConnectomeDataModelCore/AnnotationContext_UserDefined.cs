using Microsoft.EntityFrameworkCore;
using EntityFrameworkExtras.EFCore;
using NetTopologySuite.Geometries;
using Viking.DataModel.Annotation.ValueConverters;
using Viking.DataModel.Annotation.UDT;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// My custom overrides to the AnnotationContext model
    /// </summary>
    public partial class AnnotationContext
    { 
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Location>(entity =>
            {
                var geometry_converter = new CurvePolygonConverter<Geometry, Geometry>();

                entity.Property(e => e.VolumeShape)
                    .HasConversion(geometry_converter);

                entity.Property(e => e.MosaicShape)
                    .HasConversion(geometry_converter);

                //entity.HasIndex(e => e.VolumeShape, "VolumeShape_Index");
                //entity.HasIndex(e => e.MosaicShape, "MosaicShape_Index");
            });

            modelBuilder.Entity<integer_list>(entity =>
            {
                entity.IsMemoryOptimized();
            });

            modelBuilder.Entity<udtLinks>(entity =>
            {
                entity.HasKey(e => new { e.SourceID, e.TargetID });
                entity.IsMemoryOptimized();
            });

            modelBuilder.Entity<udtParentChildIDMap>(entity =>
            {
                entity.HasKey(e => new { e.ParentID, e.ID });
                entity.IsMemoryOptimized();
            });
        }
         
        //Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter CurvePolyConverter;
    } 
}

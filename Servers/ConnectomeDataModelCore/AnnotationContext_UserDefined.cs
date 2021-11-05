using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Viking.DataModel.Annotation.ValueConverters;

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
            });
        }
        //Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter CurvePolyConverter;
    }
}

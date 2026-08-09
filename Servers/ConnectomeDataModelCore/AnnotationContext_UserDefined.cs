using Microsoft.EntityFrameworkCore;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// My custom overrides to the AnnotationContext model
    /// </summary>
    public partial class AnnotationContext
    {
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

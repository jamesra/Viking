using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Viking.DataModel.Annotation
{
    public partial class AnnotationContext
    {
        /// <summary>
        /// Locations whose own structure has at least <paramref name="minLocations"/> rows.
        /// Lossy prefilter for residual-field admission; does not encode Catmull-Rom.
        /// </summary>
        public IQueryable<Location> ResidualFieldCandidateLocations(int minLocations = 3)
        {
            int min = minLocations < 1 ? 3 : minLocations;
            return Locations.FromSqlInterpolated($"SELECT * FROM dbo.ResidualFieldCandidateLocations({min})");
        }
    }
}

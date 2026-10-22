using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Answers "which annotation contexts does this segmentation service read tiles for" for the
    /// AssignedAnnotationContexts endpoint: every context reached from the service's local resources.
    /// </summary>
    public static class SegmentationServiceAssignments
    {
        /// <summary>
        /// Lists the contexts local to the segmentation service named <paramref name="serviceName"/>, ordered by name.
        /// </summary>
        /// <param name="context">Identity database.</param>
        /// <param name="serviceName">The <c>SegmentationService</c> resource name (matched case-insensitively).</param>
        /// <param name="cancellationToken">Cancels the query.</param>
        /// <returns>The contexts, or null when no segmentation service has that name (callers answer 404).</returns>
        public static async Task<List<AssignedAnnotationContext>> ListAsync(
            ApplicationDbContext context, string serviceName, CancellationToken cancellationToken = default)
        {
            var service = await context.SegmentationServices
                .FirstOrDefaultAsync(s => s.Name == serviceName, cancellationToken);
            if (service == null)
                return null;

            var ids = (await context.LocalAnnotationContextsAsync(service.Id)).Select(l => l.Context.Id).ToList();
            var contexts = await context.WithCatalog()
                .Where(c => ids.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return contexts.Select(AssignedAnnotationContext.From).ToList();
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// One annotation context assigned to a correction service, with what the service needs to run the
    /// corrections math: the VikingXML (for stos transforms) and the annotation database to read.
    /// </summary>
    public class AssignedAnnotationContext
    {
        public long AnnotationContextId { get; set; }

        /// <summary>Annotation context name.</summary>
        public string Name { get; set; }

        /// <summary>VikingXML URL of the context; the transforms are loaded from it.</summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// StosGroup to correct. Null means the VikingXML default group; "None" means no slice-to-slice warp
        /// and therefore nothing to correct.
        /// </summary>
        public string RegistrationName { get; set; }

        /// <summary>Annotation database of the default annotation server (<see cref="VolumeMetadata.CorrectionDatabaseName"/>). Part of the (database, StosGroup) key.</summary>
        public string AnnotationDatabaseName { get; set; }

        /// <summary>Service root of the default annotation server.</summary>
        public string BaseUrl { get; set; }

        public string AnnotationEndpoint { get; set; }

        public string ODataEndpoint { get; set; }
    }

    /// <summary>
    /// Answers "which annotation contexts are assigned to this correction service" for the
    /// AssignedAnnotationContexts endpoint.
    /// </summary>
    public static class CorrectionServiceAssignments
    {
        /// <summary>
        /// Lists the contexts assigned to the correction service named <paramref name="serviceName"/>, ordered by name.
        /// </summary>
        /// <param name="context">Identity database.</param>
        /// <param name="serviceName">The <c>CorrectionService</c> resource name (matched case-insensitively).</param>
        /// <param name="cancellationToken">Cancels the query.</param>
        /// <returns>The assignments, or null when no correction service has that name (callers answer 404).</returns>
        public static async Task<List<AssignedAnnotationContext>> ListAsync(
            ApplicationDbContext context, string serviceName, CancellationToken cancellationToken = default)
        {
            var service = await context.CorrectionServices
                .FirstOrDefaultAsync(s => s.Name == serviceName, cancellationToken);
            if (service == null)
                return null;

            var contexts = await context.WithCatalog()
                .Where(c => c.CorrectionServiceId == service.Id)
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return contexts.Select(c =>
            {
                var server = VolumeMetadata.DefaultServer(c);
                return new AssignedAnnotationContext
                {
                    AnnotationContextId = c.Id,
                    Name = c.Name,
                    Endpoint = c.Endpoint?.ToString(),
                    RegistrationName = c.RegistrationName,
                    AnnotationDatabaseName = VolumeMetadata.CorrectionDatabaseName(server),
                    BaseUrl = server?.BaseUrl,
                    AnnotationEndpoint = AnnotationServiceUrls.AnnotationUrl(server),
                    ODataEndpoint = AnnotationServiceUrls.ODataUrl(server)
                };
            }).ToList();
        }
    }
}

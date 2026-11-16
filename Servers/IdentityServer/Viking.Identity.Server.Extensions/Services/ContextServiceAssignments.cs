using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// One annotation context assigned to a correction or segmentation service, with what the service needs: the
    /// VikingXML (stos transforms for corrections, tilesets for segmentation) and the annotation database to read.
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

        /// <summary>
        /// The default annotation server's own default context (<see cref="AnnotationServer.DefaultAnnotationContextId"/>):
        /// the context services work in when several read that database. Null when the database names none.
        /// </summary>
        public long? DatabaseDefaultContextId { get; set; }

        /// <summary>The assignment row for <paramref name="c"/>, loaded with <see cref="VolumeMetadata.WithCatalog"/>.</summary>
        public static AssignedAnnotationContext From(AnnotationContext c)
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
                ODataEndpoint = AnnotationServiceUrls.ODataUrl(server),
                DatabaseDefaultContextId = server?.DefaultAnnotationContextId
            };
        }
    }

    /// <summary>
    /// Answers "which annotation contexts does this service serve" for the AssignedAnnotationContexts endpoint: the
    /// contexts reached from the service's assigned resources (for a correction service, those it owns).
    /// </summary>
    public static class ContextServiceAssignments
    {
        /// <summary>
        /// Lists the contexts served by the <typeparamref name="TService"/> named <paramref name="serviceName"/>, ordered by name.
        /// </summary>
        /// <param name="context">Identity database.</param>
        /// <param name="serviceName">The service's resource name (matched case-insensitively).</param>
        /// <param name="cancellationToken">Cancels the query.</param>
        /// <returns>The contexts, or null when no service of that kind has that name (callers answer 404).</returns>
        public static async Task<List<AssignedAnnotationContext>> ListAsync<TService>(
            ApplicationDbContext context, string serviceName, CancellationToken cancellationToken = default)
            where TService : ContextService
        {
            var service = await context.Resource.OfType<TService>()
                .FirstOrDefaultAsync(s => s.Name == serviceName, cancellationToken);
            if (service == null)
                return null;

            var served = service is CorrectionService
                ? await context.CorrectedAnnotationContextsAsync(service.Id)
                : await context.ServedAnnotationContextsAsync(service.Id);
            var ids = served.Select(s => s.Context.Id).ToList();
            var contexts = await context.WithCatalog()
                .Where(c => ids.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return contexts.Select(AssignedAnnotationContext.From).ToList();
        }
    }
}

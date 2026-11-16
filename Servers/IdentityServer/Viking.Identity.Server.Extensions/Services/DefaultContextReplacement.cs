using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>A context another context's holder could take as its default instead.</summary>
    public record ContextChoice(long Id, string Name, string Endpoint);

    /// <summary>
    /// A connectome or annotation database that names the context being deleted as its default, and the contexts it could
    /// name instead: the connectome's own contexts, or the contexts linked to the database.
    /// </summary>
    public record DefaultContextHolder(long Id, string Name, string ResourceTypeId, IReadOnlyList<ContextChoice> Alternatives);

    /// <summary>
    /// Deleting an annotation context that a connectome or an annotation database names as its default would break the
    /// Restrict foreign key on <c>DefaultAnnotationContextId</c>. The administrator chooses each holder's new default
    /// ("No Default" or one of its other contexts) on the delete page, and <see cref="ApplyAsync"/> sets them before the delete.
    /// </summary>
    public static class DefaultContextReplacement
    {
        /// <summary>The form value that means "No Default".</summary>
        public const string NoDefault = "none";

        /// <summary>Every connectome and annotation database whose default is <paramref name="contextId"/>.</summary>
        public static async Task<List<DefaultContextHolder>> HoldersAsync(ApplicationDbContext db, long contextId)
        {
            var holders = new List<DefaultContextHolder>();

            var connectomes = await db.Connectomes
                .Where(c => c.DefaultAnnotationContextId == contextId)
                .Include(c => c.AnnotationContexts)
                .ToListAsync();
            foreach (var connectome in connectomes)
            {
                var others = connectome.AnnotationContexts
                    .Where(c => c.Id != contextId)
                    .OrderBy(c => c.Name).ThenBy(c => c.Id)
                    .Select(c => new ContextChoice(c.Id, c.Name, c.Endpoint?.ToString()))
                    .ToList();
                holders.Add(new DefaultContextHolder(connectome.Id, connectome.Name, nameof(Connectome), others));
            }

            var servers = await db.AnnotationServers
                .Where(s => s.DefaultAnnotationContextId == contextId)
                .ToListAsync();
            foreach (var server in servers)
            {
                var linked = await db.AnnotationContextServers
                    .Where(l => l.AnnotationServerId == server.Id && l.AnnotationContextId != contextId)
                    .Select(l => l.AnnotationContext)
                    .ToListAsync();
                var others = linked
                    .OrderBy(c => c.Name).ThenBy(c => c.Id)
                    .Select(c => new ContextChoice(c.Id, c.Name, c.Endpoint?.ToString()))
                    .ToList();
                holders.Add(new DefaultContextHolder(server.Id, server.Name, nameof(AnnotationServer), others));
            }

            return holders;
        }

        /// <summary>
        /// Sets each holder's new default from <paramref name="choices"/> (holder id to <see cref="NoDefault"/> or a
        /// context id), without saving. Returns the problems instead, and changes nothing, when a holder has no choice or
        /// its choice is not one of its alternatives.
        /// </summary>
        public static async Task<List<string>> ApplyAsync(ApplicationDbContext db, long contextId, IReadOnlyDictionary<long, string> choices)
        {
            var holders = await HoldersAsync(db, contextId);
            var errors = new List<string>();
            var picked = new Dictionary<DefaultContextHolder, long?>();
            foreach (var holder in holders)
            {
                choices.TryGetValue(holder.Id, out var choice);
                choice = choice?.Trim();
                if (string.IsNullOrEmpty(choice))
                {
                    errors.Add($"Choose a new default context for {holder.Name}, or No Default.");
                }
                else if (choice == NoDefault)
                {
                    picked[holder] = null;
                }
                else if (long.TryParse(choice, out var id) && holder.Alternatives.Any(a => a.Id == id))
                {
                    picked[holder] = id;
                }
                else
                {
                    errors.Add($"{choice} is not one of the contexts {holder.Name} can take as its default.");
                }
            }

            if (errors.Count > 0)
                return errors;

            foreach (var (holder, id) in picked)
            {
                if (holder.ResourceTypeId == nameof(Connectome))
                    (await db.Connectomes.FindAsync(holder.Id)).DefaultAnnotationContextId = id;
                else
                    (await db.AnnotationServers.FindAsync(holder.Id)).DefaultAnnotationContextId = id;
            }

            return errors;
        }
    }
}

using System;
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
    /// Maintains <see cref="VolumeAnnotationServer"/> rows and keeps
    /// <see cref="Volume.AnnotationServerId"/> aligned with the default link.
    /// Called by catalog sync and the Volumes Edit action.
    /// </summary>
    public static class VolumeAnnotationServerLinks
    {
        /// <summary>
        /// Ensures <paramref name="server"/> is linked to <paramref name="volume"/>.
        /// Sets it as default only when the volume has no default yet (or when
        /// <paramref name="forceDefault"/> is true). Does not save.
        /// </summary>
        public static async Task EnsureLinkAsync(
            ApplicationDbContext context,
            Volume volume,
            AnnotationServer server,
            bool forceDefault = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(volume);
            ArgumentNullException.ThrowIfNull(server);

            await LoadLinksAsync(context, volume, cancellationToken);

            var link = volume.AnnotationServerLinks
                .FirstOrDefault(l => l.AnnotationServerId == server.Id
                    || (server.Id == 0 && ReferenceEquals(l.AnnotationServer, server)));

            if (link == null)
            {
                link = new VolumeAnnotationServer
                {
                    Volume = volume,
                    VolumeId = volume.Id,
                    AnnotationServer = server,
                    AnnotationServerId = server.Id
                };
                volume.AnnotationServerLinks.Add(link);
                context.VolumeAnnotationServers.Add(link);
            }

            bool hasDefault = volume.AnnotationServerLinks.Any(l => l.IsDefault);
            if (forceDefault || !hasDefault)
                SetDefault(volume, link);

            SyncDenormalizedDefault(volume);
        }

        /// <summary>
        /// Replaces the volume's annotation-server links with <paramref name="serverIds"/>.
        /// <paramref name="defaultServerId"/> must be in that set when non-null; when null and
        /// the set is non-empty, the first id becomes the default. Does not save.
        /// </summary>
        public static async Task ReplaceLinksAsync(
            ApplicationDbContext context,
            Volume volume,
            IReadOnlyList<long> serverIds,
            long? defaultServerId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(volume);

            await LoadLinksAsync(context, volume, cancellationToken);

            var desired = (serverIds ?? Array.Empty<long>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (desired.Count == 0)
            {
                foreach (var existing in volume.AnnotationServerLinks.ToList())
                    context.VolumeAnnotationServers.Remove(existing);
                volume.AnnotationServerLinks.Clear();
                volume.AnnotationServerId = null;
                volume.AnnotationServer = null;
                return;
            }

            if (!defaultServerId.HasValue || !desired.Contains(defaultServerId.Value))
                defaultServerId = desired[0];

            var toRemove = volume.AnnotationServerLinks
                .Where(l => !desired.Contains(l.AnnotationServerId))
                .ToList();
            foreach (var link in toRemove)
            {
                volume.AnnotationServerLinks.Remove(link);
                context.VolumeAnnotationServers.Remove(link);
            }

            foreach (var serverId in desired)
            {
                var link = volume.AnnotationServerLinks.FirstOrDefault(l => l.AnnotationServerId == serverId);
                if (link == null)
                {
                    link = new VolumeAnnotationServer
                    {
                        VolumeId = volume.Id,
                        AnnotationServerId = serverId,
                        IsDefault = serverId == defaultServerId.Value
                    };
                    volume.AnnotationServerLinks.Add(link);
                    context.VolumeAnnotationServers.Add(link);
                }
                else
                {
                    link.IsDefault = serverId == defaultServerId.Value;
                }
            }

            SyncDenormalizedDefault(volume);
        }

        /// <summary>
        /// Aligns <see cref="Volume.AnnotationServerId"/> with the IsDefault link (or null).
        /// </summary>
        public static void SyncDenormalizedDefault(Volume volume)
        {
            ArgumentNullException.ThrowIfNull(volume);

            var defaultLink = volume.AnnotationServerLinks?.FirstOrDefault(l => l.IsDefault)
                ?? volume.AnnotationServerLinks?.FirstOrDefault();

            if (defaultLink == null)
            {
                volume.AnnotationServerId = null;
                volume.AnnotationServer = null;
                return;
            }

            if (!defaultLink.IsDefault)
                defaultLink.IsDefault = true;

            if (defaultLink.AnnotationServer != null)
            {
                volume.AnnotationServer = defaultLink.AnnotationServer;
                if (defaultLink.AnnotationServer.Id != 0)
                    volume.AnnotationServerId = defaultLink.AnnotationServer.Id;
            }
            else if (defaultLink.AnnotationServerId != 0)
            {
                volume.AnnotationServerId = defaultLink.AnnotationServerId;
            }
        }

        private static void SetDefault(Volume volume, VolumeAnnotationServer link)
        {
            foreach (var other in volume.AnnotationServerLinks)
                other.IsDefault = ReferenceEquals(other, link);
            link.IsDefault = true;
        }

        private static async Task LoadLinksAsync(ApplicationDbContext context, Volume volume, CancellationToken cancellationToken)
        {
            var entry = context.Entry(volume);
            if (entry.State == EntityState.Detached)
                return;

            var collection = entry.Collection(v => v.AnnotationServerLinks);
            if (!collection.IsLoaded)
                await collection.LoadAsync(cancellationToken);
        }
    }
}

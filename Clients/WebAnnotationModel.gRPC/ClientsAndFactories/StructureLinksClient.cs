using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using WebAnnotationModel.gRPC;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class StructureLinksClientExtensions
    {
        public static IServiceCollection AddGrpcStructureLinkRepository(this IServiceCollection service)
        {
            service.AddSingleton<IServerAnnotationsClientFactory<IServerAnnotationsClient<StructureLinkKey, IStructureLink, StructureLinkObj, IStructureLink>>, StructureLinksClientFactory>();
            return service;
        }
    }
}

namespace WebAnnotationModel.gRPC
{
    public class StructureLinksClientFactory : IServerAnnotationsClientFactory<IServerAnnotationsClient<StructureLinkKey, IStructureLink, StructureLinkObj, IStructureLink>>
    {
        private readonly IGrpcChannelManager _channelManager;
        private readonly GrpcRepositorySettings _config;

        public StructureLinksClientFactory(IGrpcChannelManager channelManager, IOptions<GrpcRepositorySettings> config)
        {
            _channelManager = channelManager;
            _config = config.Value;
        }

        public IServerAnnotationsClient<StructureLinkKey, IStructureLink, StructureLinkObj, IStructureLink> GetOrCreate()
        {
            return new StructureLinksClient(_channelManager.GetOrCreate(_config.Endpoint));
        }
    }

    /// <summary>
    /// Lightweight client for structure-link RPCs, which live on the AnnotateStructures service alongside
    /// the structure CRUD RPCs. Kept independent of StructuresClient so it does not depend on the
    /// (currently unregistered) structure-creation converters.
    /// </summary>
    internal class StructureLinksClient : IServerAnnotationsClient<StructureLinkKey, IStructureLink, StructureLinkObj, IStructureLink>
    {
        private readonly AnnotateStructures.AnnotateStructuresClient Client;

        public StructureLinksClient(GrpcChannel channel)
        {
            Client = new AnnotateStructures.AnnotateStructuresClient(channel);
        }

        private static StructureLink ToProto(IStructureLink link)
        {
            if (link is StructureLink concrete)
                return concrete;

            return new StructureLink
            {
                SourceId = (long)link.SourceID,
                TargetId = (long)link.TargetID,
                Bidirectional = !link.Directional
            };
        }

        private static StructureLink ToProto(StructureLinkObj link) => new StructureLink
        {
            SourceId = link.SourceID,
            TargetId = link.TargetID,
            Bidirectional = link.Bidirectional
        };

        public async Task<IStructureLink> Create(StructureLinkObj obj, CancellationToken token)
        {
            var request = new CreateStructureLinkRequest { NewLink = ToProto(obj) };
            var response = await Client.CreateStructureLinkAsync(request, cancellationToken: token);
            return response.Result;
        }

        /// <summary>
        /// The AnnotateStructures service has no dedicated delete RPC for structure links today
        /// (they are removed as a side effect of deleting a structure). Report "not found" rather
        /// than throwing so callers that speculatively try to delete do not crash.
        /// </summary>
        public Task<StructureLinkKey?> Delete(StructureLinkKey key, CancellationToken token)
        {
            return Task.FromResult<StructureLinkKey?>(null);
        }

        public async Task<IStructureLink> GetAsync(StructureLinkKey key, CancellationToken token)
        {
            var links = await GetLinksForStructureAsync(key.SourceID, token);
            return links.FirstOrDefault(l =>
                ((long)l.SourceID == key.SourceID && (long)l.TargetID == key.TargetID) ||
                ((long)l.SourceID == key.TargetID && (long)l.TargetID == key.SourceID));
        }

        /// <summary>
        /// All structure links where <paramref name="structureId"/> is source or target.
        /// </summary>
        public async Task<IStructureLink[]> GetLinksForStructureAsync(long structureId, CancellationToken token)
        {
            var request = new GetLinkedStructuresRequest { Id = structureId };
            var response = await Client.GetLinkedStructuresAsync(request, cancellationToken: token);
            return response.Results.Cast<IStructureLink>().ToArray();
        }

        public async Task<IList<IStructureLink>> GetAsync(IEnumerable<StructureLinkKey> keys, CancellationToken token)
        {
            var results = new List<IStructureLink>();
            foreach (var key in keys)
            {
                var link = await GetAsync(key, token);
                if (link != null)
                    results.Add(link);
            }

            return results;
        }

        public Task<UpdateResults<StructureLinkKey, IStructureLink>> UpdateAsync(IStructureLink obj, CancellationToken token)
        {
            return UpdateAsync(new[] { obj }, token);
        }

        public async Task<UpdateResults<StructureLinkKey, IStructureLink>> UpdateAsync(IEnumerable<IStructureLink> objs, CancellationToken token)
        {
            var request = new UpdateStructureLinksRequest();
            request.Objs.AddRange(objs.Select(ToProto));

            await Client.UpdateLinksAsync(request, cancellationToken: token);

            //UpdateStructureLinksResponse carries no results, so echo back what was sent as "updated".
            return new UpdateResults<StructureLinkKey, IStructureLink>(updated: objs.ToArray());
        }
    }
}

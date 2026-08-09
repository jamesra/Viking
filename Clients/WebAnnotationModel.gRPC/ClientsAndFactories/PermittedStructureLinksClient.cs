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
    public static class PermittedStructureLinksClientExtensions
    {
        public static IServiceCollection AddGrpcPermittedStructureLinkRepository(this IServiceCollection service)
        {
            service.AddSingleton<IServerAnnotationsClientFactory<IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>>, PermittedStructureLinksClientFactory>();
            return service;
        }
    }
}

namespace WebAnnotationModel.gRPC
{
    public class PermittedStructureLinksClientFactory : IServerAnnotationsClientFactory<IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>>
    {
        private readonly IGrpcChannelManager _channelManager;
        private readonly GrpcRepositorySettings _config;

        public PermittedStructureLinksClientFactory(IGrpcChannelManager channelManager, IOptions<GrpcRepositorySettings> config)
        {
            _channelManager = channelManager;
            _config = config.Value;
        }

        public IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink> GetOrCreate()
        {
            return new PermittedStructureLinksClient(_channelManager.GetOrCreate(_config.Endpoint));
        }
    }

    internal class PermittedStructureLinksClient : IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>
    {
        private readonly Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLinks.PermittedStructureLinksClient Client;

        public PermittedStructureLinksClient(GrpcChannel channel)
        {
            Client = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLinks.PermittedStructureLinksClient(channel);
        }

        private static PermittedStructureLink ToProto(PermittedStructureLinkObj src)
        {
            var obj = new PermittedStructureLink
            {
                SourceTypeId = src.SourceTypeID,
                TargetTypeId = src.TargetTypeID,
                Bidirectional = src.Bidirectional
            };
            ((IChangeAction)obj).DBAction = src.DBAction;
            return obj;
        }

        public async Task<IPermittedStructureLink> Create(PermittedStructureLinkObj obj, CancellationToken token)
        {
            var request = new CreatePermittedStructureLinkRequest { NewObj = ToProto(obj) };
            var response = await Client.CreatePermittedStructureLinkAsync(request, cancellationToken: token);
            return response.Result;
        }

        public Task<PermittedStructureLinkKey?> Delete(PermittedStructureLinkKey key, CancellationToken token)
        {
            //Deletes flow through UpdateAsync using DBACTION.DELETE, matching the base store's Save() pathway.
            return Task.FromResult<PermittedStructureLinkKey?>(null);
        }

        public async Task<IPermittedStructureLink> GetAsync(PermittedStructureLinkKey key, CancellationToken token)
        {
            var all = await GetAllAsync(token);
            return all.FirstOrDefault(l =>
                (l.SourceTypeId == key.SourceTypeID && l.TargetTypeId == key.TargetTypeID) ||
                (l.SourceTypeId == key.TargetTypeID && l.TargetTypeId == key.SourceTypeID));
        }

        public async Task<IList<IPermittedStructureLink>> GetAsync(IEnumerable<PermittedStructureLinkKey> keys, CancellationToken token)
        {
            var all = await GetAllAsync(token);
            var keySet = keys.ToList();
            return all.Where(l => keySet.Any(key =>
                (l.SourceTypeId == key.SourceTypeID && l.TargetTypeId == key.TargetTypeID) ||
                (l.SourceTypeId == key.TargetTypeID && l.TargetTypeId == key.SourceTypeID)))
                .Cast<IPermittedStructureLink>().ToList();
        }

        private async Task<List<PermittedStructureLink>> GetAllAsync(CancellationToken token)
        {
            var response = await Client.GetPermittedStructureLinksAsync(new GetPermittedStructureLinksRequest(), cancellationToken: token);
            return response.PermittedLinks.ToList();
        }

        public Task<UpdateResults<PermittedStructureLinkKey, IPermittedStructureLink>> UpdateAsync(IPermittedStructureLink obj, CancellationToken token)
        {
            return UpdateAsync(new[] { obj }, token);
        }

        public async Task<UpdateResults<PermittedStructureLinkKey, IPermittedStructureLink>> UpdateAsync(IEnumerable<IPermittedStructureLink> objs, CancellationToken token)
        {
            var request = new UpdatePermittedStructureLinksRequest();
            request.Changes.AddRange(objs.OfType<PermittedStructureLink>().Select(o => (PermittedStructureLinkChange)o).Where(c => c != null));

            var response = await Client.UpdatePermittedStructureLinksAsync(request, cancellationToken: token);

            var updated = response.Changes.Where(c => c.Sucess && c.Result != null).Select(c => (IPermittedStructureLink)c.Result).ToArray();
            return new UpdateResults<PermittedStructureLinkKey, IPermittedStructureLink>(updated: updated);
        }
    }
}

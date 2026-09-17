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

        public async Task<PermittedStructureLinkKey?> Delete(PermittedStructureLinkKey key, CancellationToken token)
        {
            var proto = new PermittedStructureLink
            {
                SourceTypeId = key.SourceTypeID,
                TargetTypeId = key.TargetTypeID,
                Bidirectional = key.Bidirectional
            };
            ((IChangeAction)proto).DBAction = DBACTION.DELETE;

            var results = await UpdateAsync(proto, token).ConfigureAwait(false);
            if (results.DeletedIDs != null && results.DeletedIDs.Length > 0)
                return key;
            return null;
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

        internal async Task<List<PermittedStructureLink>> GetAllAsync(CancellationToken token)
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
            foreach (var obj in objs)
            {
                var change = ToChange(obj);
                if (change != null)
                    request.Changes.Add(change);
            }

            if (request.Changes.Count == 0)
                return new UpdateResults<PermittedStructureLinkKey, IPermittedStructureLink>();

            var response = await Client.UpdatePermittedStructureLinksAsync(request, cancellationToken: token);

            var updated = new List<IPermittedStructureLink>();
            var deleted = new List<PermittedStructureLinkKey>();
            foreach (var change in response.Changes)
            {
                if (!change.Sucess)
                    continue;
                if (change.Action == DBAction.Delete && change.Result != null)
                    deleted.Add(new PermittedStructureLinkKey(change.Result.SourceTypeId, change.Result.TargetTypeId, change.Result.Bidirectional));
                else if (change.Result != null)
                    updated.Add(change.Result);
            }

            return new UpdateResults<PermittedStructureLinkKey, IPermittedStructureLink>(
                updated: updated.ToArray(), deleted: deleted.ToArray());
        }

        private static PermittedStructureLinkChange ToChange(IPermittedStructureLink obj)
        {
            if (obj is PermittedStructureLink concrete)
                return (PermittedStructureLinkChange)concrete;

            if (obj is PermittedStructureLinkObj clientObj)
                return (PermittedStructureLinkChange)ToProto(clientObj);

            var proto = new PermittedStructureLink
            {
                SourceTypeId = (long)obj.SourceTypeID,
                TargetTypeId = (long)obj.TargetTypeID,
                Bidirectional = !obj.Directional
            };
            if (obj is IChangeAction changeAction)
                ((IChangeAction)proto).DBAction = changeAction.DBAction;
            return (PermittedStructureLinkChange)proto;
        }
    }
}

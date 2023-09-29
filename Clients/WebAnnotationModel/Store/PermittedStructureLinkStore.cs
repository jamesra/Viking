using AnnotationService.Types;
using System;
using System.Collections.Generic;
using System.ServiceModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{

    public class PermittedStructureLinkStore : StoreBaseWithKey<AnnotatePermittedStructureLinksClient, IAnnotatePermittedStructureLinks, PermittedStructureLinkKey, PermittedStructureLinkObj, PermittedStructureLink>
    {
        public PermittedStructureLinkStore()
        {
            channelFactory =
                new ChannelFactory<IAnnotatePermittedStructureLinks>("Annotation.Service.Interfaces.IAnnotatePermittedStructureLinks-Binary");

            channelFactory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            channelFactory.Credentials.UserName.Password = State.UserCredentials.Password;
        }

        public override void Init()
        {
            return;
        } 

        protected override PermittedStructureLinkKey[] ProxyUpdate(IAnnotatePermittedStructureLinks proxy, PermittedStructureLink[] linkObjs)
        {
            proxy.UpdatePermittedStructureLinks(linkObjs);
            return new PermittedStructureLinkKey[0];
        }

        protected override PermittedStructureLink ProxyGetByID(IAnnotatePermittedStructureLinks proxy, PermittedStructureLinkKey ID)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetByIDs(IAnnotatePermittedStructureLinks proxy, PermittedStructureLinkKey[] IDs)
        {
            throw new NotImplementedException();
        }

        public override System.Collections.Concurrent.ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> GetLocalObjectsForSection(long SectionNumber)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySection(IAnnotatePermittedStructureLinks proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(IAnnotatePermittedStructureLinks proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotatePermittedStructureLinks, PermittedStructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySectionRegion(IAnnotatePermittedStructureLinks proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out PermittedStructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }


        protected override IAsyncResult ProxyBeginGetBySection(IAnnotatePermittedStructureLinks proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotatePermittedStructureLinks, PermittedStructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public PermittedStructureLinkObj Create(PermittedStructureLinkObj link)
        {
            using(var proxy = CreateProxy())
            {
                var client = (IAnnotatePermittedStructureLinks)proxy;
                PermittedStructureLink dblink = client.CreatePermittedStructureLink(link.GetData());
                PermittedStructureLinkObj created_link = new PermittedStructureLinkObj(dblink);
                Add(created_link);
                return created_link;
            }
        }

        protected override ChangeInventory<PermittedStructureLinkObj> InternalAdd(PermittedStructureLinkObj[] newObjs)
        {
            List<PermittedStructureLinkObj> ValidObjs = new List<PermittedStructureLinkObj>(newObjs.Length);

            foreach (PermittedStructureLinkObj link in newObjs)
            {

                StructureTypeObj SourceObj = Store.StructureTypes.GetObjectByID(link.SourceTypeID, false);
                StructureTypeObj TargetObj = Store.StructureTypes.GetObjectByID(link.TargetTypeID, false);

                SourceObj?.TryAddPermittedLink(link);

                if (TargetObj != null && TargetObj != SourceObj)
                    TargetObj.TryAddPermittedLink(link);

                ValidObjs.Add(link);
            }

            return base.InternalAdd(ValidObjs.ToArray());
        }

        protected override List<PermittedStructureLinkObj> InternalDelete(PermittedStructureLinkKey[] linkKeys)
        {
            foreach (PermittedStructureLinkKey key in linkKeys)
            {
                /*
                PermittedStructureLinkObj link = Store.StructureLinks.GetObjectByID(key, false);
                if (link == null)
                    continue; 
                */

                StructureTypeObj SourceObj = Store.StructureTypes.GetObjectByID(key.SourceTypeID, false);
                StructureTypeObj TargetObj = Store.StructureTypes.GetObjectByID(key.TargetTypeID, false);

                SourceObj?.TryRemovePermittedLink(key);

                TargetObj?.TryRemovePermittedLink(key);
            }

            return base.InternalDelete(linkKeys);
        }

    }
}

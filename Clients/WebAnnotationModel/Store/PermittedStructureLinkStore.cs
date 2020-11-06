using AnnotationService.Types;
using System;
using System.Collections.Generic;
using WebAnnotationModel.Objects;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{

    public class PermittedStructureLinkStore : StoreBaseWithKey<AnnotatePermittedStructureLinksClient, IAnnotatePermittedStructureLinks, PermittedStructureLinkKey, PermittedStructureLinkObj, PermittedStructureLink>
    {
        public PermittedStructureLinkStore()
        {
        }

        public override void Init()
        {
            return;
        }

        protected override AnnotatePermittedStructureLinksClient CreateProxy()
        {
            AnnotatePermittedStructureLinksClient proxy = new Service.AnnotatePermittedStructureLinksClient("Annotation.Service.Interfaces.IAnnotatePermittedStructureLinks-Binary", State.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = State.UserCredentials.Password;
            return proxy;
        }

        protected override PermittedStructureLinkKey[] ProxyUpdate(AnnotatePermittedStructureLinksClient proxy, PermittedStructureLink[] linkObjs)
        {
            proxy.UpdatePermittedStructureLinks(linkObjs);
            return new PermittedStructureLinkKey[0];
        }

        protected override PermittedStructureLink ProxyGetByID(AnnotatePermittedStructureLinksClient proxy, PermittedStructureLinkKey ID)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetByIDs(AnnotatePermittedStructureLinksClient proxy, PermittedStructureLinkKey[] IDs)
        {
            throw new NotImplementedException();
        }

        public override System.Collections.Concurrent.ConcurrentDictionary<PermittedStructureLinkKey, PermittedStructureLinkObj> GetLocalObjectsForSection(long SectionNumber)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySection(AnnotatePermittedStructureLinksClient proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(AnnotatePermittedStructureLinksClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<AnnotatePermittedStructureLinksClient, PermittedStructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySectionRegion(AnnotatePermittedStructureLinksClient proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out PermittedStructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }


        protected override IAsyncResult ProxyBeginGetBySection(AnnotatePermittedStructureLinksClient proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override PermittedStructureLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out PermittedStructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<AnnotatePermittedStructureLinksClient, PermittedStructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public PermittedStructureLinkObj Create(PermittedStructureLinkObj link)
        {
            AnnotatePermittedStructureLinksClient proxy = null;
            try
            {
                proxy = CreateProxy();
                PermittedStructureLink dblink = proxy.CreatePermittedStructureLink(link.GetData());
                PermittedStructureLinkObj created_link = new PermittedStructureLinkObj(dblink);
                Add(created_link);
                return created_link;
            }
            finally
            {
                if (proxy != null)
                {
                    proxy.Close();
                }
            }
        }

        protected override ChangeInventory<PermittedStructureLinkObj> InternalAdd(PermittedStructureLinkObj[] newObjs)
        {
            List<PermittedStructureLinkObj> ValidObjs = new List<PermittedStructureLinkObj>(newObjs.Length);

            foreach (PermittedStructureLinkObj link in newObjs)
            {

                StructureTypeObj SourceObj = Store.StructureTypes.GetObjectByID(link.SourceTypeID, false);
                StructureTypeObj TargetObj = Store.StructureTypes.GetObjectByID(link.TargetTypeID, false);

                if (SourceObj != null)
                    SourceObj.TryAddPermittedLink(link);

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

                if (SourceObj != null)
                    SourceObj.TryRemovePermittedLink(key);

                if (TargetObj != null)
                    TargetObj.TryRemovePermittedLink(key);
            }

            return base.InternalDelete(linkKeys);
        }

    }
}

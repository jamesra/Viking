using AnnotationService.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceModel;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{

    public class StructureLinkStore : StoreBaseWithKey<AnnotateStructuresClient, IAnnotateStructures, StructureLinkKey, StructureLinkObj, StructureLink>
    {
        public StructureLinkStore()
        {
            channelFactory =
                new ChannelFactory<IAnnotateStructures>("Annotation.Service.Interfaces.IAnnotateStructures-Binary");

            channelFactory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            channelFactory.Credentials.UserName.Password = State.UserCredentials.Password;
        }

        public override void Init()
        {
            return;
        }

        protected override StructureLinkKey[] ProxyUpdate(IAnnotateStructures proxy, StructureLink[] linkObjs)
        {
            proxy.UpdateStructureLinks(linkObjs);
            return new StructureLinkKey[0];
        }

        protected override StructureLink ProxyGetByID(IAnnotateStructures proxy, StructureLinkKey ID)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetByIDs(IAnnotateStructures proxy, StructureLinkKey[] IDs)
        {
            throw new NotImplementedException();
        }

        public override System.Collections.Concurrent.ConcurrentDictionary<StructureLinkKey, StructureLinkObj> GetLocalObjectsForSection(long SectionNumber)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySection(IAnnotateStructures proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out StructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(IAnnotateStructures proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out StructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotateStructures, StructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySectionRegion(IAnnotateStructures proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out StructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }


        protected override IAsyncResult ProxyBeginGetBySection(IAnnotateStructures proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out StructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<IAnnotateStructures, StructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public StructureLinkObj Create(StructureLinkObj link)
        {
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();
                StructureLink dblink = ((IAnnotateStructures)proxy).CreateStructureLink(link.GetData());
                StructureLinkObj created_link = new StructureLinkObj(dblink);
                Add(created_link);
                return created_link;
            }
            finally
            {
                proxy?.Close();
            }
        }

        protected override ChangeInventory<StructureLinkObj> InternalAdd(StructureLinkObj[] newObjs)
        {
            List<StructureLinkObj> ValidObjs = new List<StructureLinkObj>(newObjs.Length);

            foreach (StructureLinkObj link in newObjs)
            {
                Debug.Assert(link.SourceID != link.TargetID, "Trying to link structure to itself");
                if (link.SourceID == link.TargetID)
                    continue;

                StructureObj SourceObj = Store.Structures.GetObjectByID(link.SourceID, false);
                StructureObj TargetObj = Store.Structures.GetObjectByID(link.TargetID, false);

                SourceObj?.AddLink(link);

                TargetObj?.AddLink(link);

                ValidObjs.Add(link);
            }

            return base.InternalAdd(ValidObjs.ToArray());
        }

        protected override List<StructureLinkObj> InternalDelete(StructureLinkKey[] linkKeys)
        {
            foreach (StructureLinkKey key in linkKeys)
            {
                /*
                StructureLinkObj link = Store.StructureLinks.GetObjectByID(key, false);
                if (link == null)
                    continue; 
                */

                StructureObj SourceObj = Store.Structures.GetObjectByID(key.SourceID, false);
                StructureObj TargetObj = Store.Structures.GetObjectByID(key.TargetID, false);

                SourceObj?.RemoveLink(key);

                TargetObj?.RemoveLink(key);
            }

            return base.InternalDelete(linkKeys);
        }

    }
}

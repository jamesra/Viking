using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

using System.Diagnostics;
using System.ServiceModel;

using WebAnnotationModel.Service;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    
    public class StructureLinkStore : StoreBaseWithKey<AnnotateStructuresClient, IAnnotateStructures, StructureLinkKey, StructureLinkObj, StructureLink>
    {
        public StructureLinkStore()
        {

        }

        public override void Init()
        {
            return; 
        }

        protected override AnnotateStructuresClient CreateProxy()
        {
            AnnotateStructuresClient proxy = new Service.AnnotateStructuresClient("Annotation.Service.Interfaces.IAnnotateStructures-Binary", State.EndpointAddress);
            proxy.ClientCredentials.UserName.UserName = State.UserCredentials.UserName;
            proxy.ClientCredentials.UserName.Password = State.UserCredentials.Password;
            return proxy;
        }

        protected override StructureLinkKey[] ProxyUpdate(AnnotateStructuresClient proxy, StructureLink[] linkObjs)
        {
            proxy.UpdateStructureLinks(linkObjs);
            return new StructureLinkKey[0];
        }
        
        protected override StructureLink ProxyGetByID(AnnotateStructuresClient proxy, StructureLinkKey ID)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetByIDs(AnnotateStructuresClient proxy, StructureLinkKey[] IDs)
        {
            throw new NotImplementedException();
        }

        public override System.Collections.Concurrent.ConcurrentDictionary<StructureLinkKey, StructureLinkObj> GetLocalObjectsForSection(long SectionNumber)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySection(AnnotateStructuresClient proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out StructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }

        protected override IAsyncResult ProxyBeginGetBySectionRegion(AnnotateStructuresClient proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        } 

        protected override StructureLink[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out StructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<StructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySectionRegion(AnnotateStructuresClient proxy,
                                                             long SectionNumber,
                                                             BoundingRectangle BBox,
                                                             double MinRadius,
                                                             DateTime LastQuery,
                                                             out long TicksAtQueryExecute,
                                                             out StructureLinkKey[] DeletedLocations)
        {
            throw new NotImplementedException();
        }


        protected override IAsyncResult ProxyBeginGetBySection(AnnotateStructuresClient proxy, long SectionNumber, DateTime LastQuery, AsyncCallback callback, object asynchState)
        {
            throw new NotImplementedException();
        }

        protected override StructureLink[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out StructureLinkKey[] DeletedLocations, GetObjectBySectionCallbackState<StructureLinkObj> state, IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public StructureLinkObj Create(StructureLinkObj link)
        {
            AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                StructureLink dblink = proxy.CreateStructureLink(link.GetData());
                StructureLinkObj created_link = new StructureLinkObj(dblink);
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

                if (SourceObj != null)
                    SourceObj.AddLink(link);

                if (TargetObj != null)
                    TargetObj.AddLink(link);

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

                if (SourceObj != null)
                    SourceObj.RemoveLink(key);

                if (TargetObj != null)
                    TargetObj.RemoveLink(key);
            }

            return base.InternalDelete(linkKeys);
        }

    }
}

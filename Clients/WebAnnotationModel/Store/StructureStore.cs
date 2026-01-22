using AnnotationService.Types;
using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using Viking.AnnotationServiceTypes;
using WebAnnotationModel.Service;

namespace WebAnnotationModel
{
    public class CreateStructureAndLocationRetval
    {
        public readonly StructureObj structure;
        public readonly LocationObj location;

        internal CreateStructureAndLocationRetval(StructureObj s, LocationObj l)
        {
            structure = s;
            location = l;
        }
    }

    public class StructureStore : StoreBaseWithIndexKeyAndParent<AnnotateStructuresClient, IAnnotateStructures, long, LongIndexGenerator, StructureObj, Structure>, IRegionQuery<long, StructureObj>
    {
        public StructureStore()
        {
            channelFactory =
                new ChannelFactory<IAnnotateStructures>("Annotation.Service.Interfaces.IAnnotateStructures-Binary");

            channelFactory.Credentials.UserName.UserName = State.UserCredentials.UserName;
            channelFactory.Credentials.UserName.Password = State.UserCredentials.Password;
        }

        #region Proxy

        protected override long[] ProxyUpdate(IAnnotateStructures proxy, Structure[] objects) => proxy.UpdateStructures(objects);

        protected override Structure ProxyGetByID(IAnnotateStructures proxy, long ID) => proxy.GetStructureByID(ID, false);

        protected override Structure[] ProxyGetByIDs(IAnnotateStructures proxy, long[] IDs)
        {
            Structure[] structures = proxy.GetStructuresByIDs(IDs, false);
            if (structures != null)
                return [.. structures];

            return [];
        }


        /// <summary>
        /// Get the location ID's for branches that are incomplete
        /// </summary>
        /// <returns></returns>
        public long[] GetUnfinishedBranches(long structureID)
        {
            var proxy = CreateProxy();
            {
                long[] ids = ((IAnnotateStructures)proxy).GetUnfinishedLocations(structureID);
                return ids;
            }
        }

        /// <summary>
        /// Get the location ID's and positions for branches that are incomplete
        /// </summary>
        /// <returns></returns>
        public LocationPositionOnly[] GetUnfinishedBranchesWithPosition(long structureID)
        {
            var proxy = CreateProxy();
            {
                return ((IAnnotateStructures)proxy).GetUnfinishedLocationsWithPosition(structureID);
            }
        }

        public override ConcurrentDictionary<long, StructureObj> GetLocalObjectsForSection(long SectionNumber) => new ConcurrentDictionary<long, StructureObj>();

        protected override Structure[] ProxyGetBySection(IAnnotateStructures proxy, long SectionNumber, DateTime LastQuery, out long TicksAtQueryExecute, out long[] DeletedLocations) => proxy.GetStructuresForSection(out TicksAtQueryExecute, out DeletedLocations, SectionNumber, LastQuery.Ticks);

        protected override Structure[] ProxyGetBySectionRegion(IAnnotateStructures proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, out long TicksAtQueryExecute, out long[] DeletedLocations) => proxy.GetStructuresForSectionInMosaicRegion(out TicksAtQueryExecute, out DeletedLocations, SectionNumber, BBox, MinRadius, LastQuery.Ticks);

        protected override IAsyncResult ProxyBeginGetBySectionRegion(IAnnotateStructures proxy, long SectionNumber, BoundingRectangle BBox, double MinRadius, DateTime LastQuery, AsyncCallback callback, object asynchState) => proxy.BeginGetStructuresForSectionInMosaicRegion(SectionNumber, BBox, MinRadius, LastQuery.Ticks, callback, asynchState);

        protected override Structure[] ProxyGetBySectionRegionCallback(out long TicksAtQueryExecute, out long[] DeletedObjects, GetObjectBySectionCallbackState<IAnnotateStructures, StructureObj> state, IAsyncResult result) => state.Proxy.EndGetStructuresForSectionInMosaicRegion(out TicksAtQueryExecute, out DeletedObjects, result);

        /// <summary>
        /// This currently always returns the empty result because its main purpose is to populate the cache so locations can determine thier type
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="SectionNumber"></param>
        /// <param name="LastQuery"></param>
        /// <param name="callback"></param>
        /// <param name="asynchState"></param>
        /// <returns></returns>
        protected override IAsyncResult ProxyBeginGetBySection(IAnnotateStructures proxy,
                                                                                            long SectionNumber,
                                                                                            DateTime LastQuery,
                                                                                            AsyncCallback callback,
                                                                                            object asynchState)
        {
            Debug.WriteLine("Get Structures for section: ", SectionNumber.ToString());

            return proxy.BeginGetStructuresForSection(SectionNumber, LastQuery.Ticks, callback, asynchState);
        }

        protected override Structure[] ProxyGetBySectionCallback(out long TicksAtQueryExecute, out long[] DeletedIDs, GetObjectBySectionCallbackState<IAnnotateStructures, StructureObj> state, IAsyncResult result) => state.Proxy.EndGetStructuresForSection(out TicksAtQueryExecute, out DeletedIDs, result);

        public override bool RemoveSection(int SectionNumber) =>
            //Section store never deletes structures, but we return true so queries in flight can be aborted
            true;

        #endregion

        public override void Init()
        {

#if DEBUG
            //            GetAllStructures(); 
#else
            //            GetAllStructures(); 
#endif

        }

        public void GetAllStructures()
        {
            Trace.WriteLine("GetAllStructures, Begin", "WebAnnotation");

            Structure[] structures = [];

            try
            {
                IClientChannel proxy = CreateProxy();
                {
                    //Cache all the structures at startup
                    structures = ((IAnnotateStructures)proxy).GetStructures();
                }
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return;
            }

            ChangeInventory<StructureObj> inventory = ParseQuery(structures, [], null);
            CallOnCollectionChanged(inventory);

            Trace.WriteLine("GetAllStructures, End", "WebAnnotation");
        }

        public void CheckForOrphan(long ID)
        {
            StructureObj obj = GetObjectByID(ID);
            if (obj is null)
                return;

            long numLocs = 0;
            try
            {
                IClientChannel proxy = CreateProxy();
                {

                    numLocs = ((IAnnotateStructures)proxy).NumberOfLocationsForStructure(ID);
                }
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                return;
            }

            if (numLocs == 0)
            {
                /*
                string name; 
                if(obj.Type != null)
                    name = obj.Type.Name + " " + obj.ID.ToString();
                else
                    name = obj.ID.ToString();
                */
                /*
                System.Windows.Forms.DialogResult result = System.Windows.Forms.MessageBox.Show("Structure " + name + " has no locations, do you wish to delete from the database?", 
                                                            "Continue Delete?",
                                                            System.Windows.Forms.MessageBoxButtons.YesNo);

                //Delete on yes
                if (result == System.Windows.Forms.DialogResult.Yes)
                {*/
                try
                {
                    //TODO
                    Remove(obj);
                    Save();
                    Trace.WriteLine("Removing childless structure: " + obj.ToString(), "WebAnnotation");

                }
                catch (FaultException)
                {
                    //             System.Windows.Forms.MessageBox.Show("Delete failed.  Structure may have had child location added or already been deleted. Exception: " + e.ToString(), "Survivable error"); 
                    throw;
                }
                //  }
            }
        }

        public StructureObj Create(StructureObj newStruct, LocationObj newLocation, out LocationObj created_loc)
        {

            created_loc = null;
            try
            {
                var proxy = CreateProxy();
                {

                    CreateStructureRetval retval = ((IAnnotateStructures)proxy).CreateStructure(newStruct.GetData(), newLocation.GetData());

                    //We should not insert created objects into the store before they are created on the server
                    Debug.Assert(this.GetObjectByID(newStruct.ID, false) is null);

                    StructureObj created_struct = new(retval.structure);

                    ChangeInventory<StructureObj> inventory = InternalAdd(created_struct);
                    created_loc = new LocationObj(retval.location);

                    CallOnCollectionChangedForAdd(new StructureObj[] { created_struct });
                    Store.Locations.AddFromFriend([created_loc]);

                    return created_struct;
                }
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                StructureObj deletedObj = InternalDelete(newStruct.ID);
                if (deletedObj != null)
                    CallOnCollectionChangedForDelete(new StructureObj[] { deletedObj });

                return null;
            }
        }

        public override bool Remove(StructureObj obj)
        {
            obj.DBAction = DBACTION.DELETE;

            return true;
        }

        public ICollection<StructureObj> GetChildStructuresForStructure(long ID)
        {
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();

                Structure data = ((IAnnotateStructures)proxy).GetStructureByID(ID, true);
                if (data is not null && data.ChildIDs is not null)
                {
                    if (data.ChildIDs?.Length > 0)
                    {
                        ICollection<StructureObj> list_structures = this.GetObjectsByIDs(data.ChildIDs, true);
                        ChangeInventory<StructureObj> inventory = InternalAdd([.. list_structures]);
                        CallOnCollectionChanged(inventory);
                        return inventory.ObjectsInStore;
                    }
                }
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
            }

            return new StructureObj[0];
        }

        public long Merge(long KeepID, long MergeID)
        {
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();

                KeepID = ((IAnnotateStructures)proxy).Merge(KeepID, MergeID);

                LocationObj[] locations = Store.Locations.GetLocalObjectsForStructure(MergeID);
                Store.Locations.Refresh([.. locations.Select(l => l.ID)]);

                this.ForgetLocally(MergeID);

                return 0;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                throw;
            }
        }

        public long SplitAtLocationLink(long KeepLocID, long SplitLocID)
        {
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();

                long SplitStructureID = ((IAnnotateStructures)proxy).SplitAtLocationLink(KeepLocID, SplitLocID);

                LocationObj keepLoc = Store.Locations.GetObjectByID(KeepLocID);
                LocationObj[] locations = Store.Locations.GetLocalObjectsForStructure(keepLoc.ParentID.Value);
                Store.Locations.Refresh([.. locations.Select(l => l.ID)]);

                LocationObj[] SplitLocations = Store.Locations.GetLocalObjectsForStructure(SplitStructureID);
                Store.Locations.Refresh([.. SplitLocations.Select(l => l.ID)]);

                Store.LocationLinks.ForgetLocally(new LocationLinkKey(KeepLocID, SplitLocID));

                return SplitStructureID;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                throw;
            }
        }


        public ICollection<StructureObj> GetStructuresOfType(long StructureTypeID)
        {
            Structure[] data = null;
            IClientChannel proxy = null;
            try
            {
                proxy = CreateProxy();

                data = ((IAnnotateStructures)proxy).GetStructuresOfType(StructureTypeID);
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                data = null;
            }

            if (null == data)
                return new StructureObj[0];

            List<StructureObj> listStructures = new(data.Length);
            foreach (Structure s in data)
            {
                Debug.Assert(s != null);

                StructureObj newObj = new(s);
                listStructures.Add(newObj);
            }

            ChangeInventory<StructureObj> output = InternalAdd([.. listStructures]); //Add might return an existing object, which we should use instead
            CallOnCollectionChanged(output);
            return output.ObjectsInStore;
        }

        public ICollection<StructureObj> GetObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc)
        {
            ICollection<LocationObj> known_locations = Store.Locations.GetObjectsInRegion(SectionNumber, bounds, MinRadius, LastQueryUtc);

            return [.. known_locations.Select(l => l.Parent).Distinct()];
        }

        public MixedLocalAndRemoteQueryResults<long, StructureObj> GetObjectsInRegionAsync(long SectionNumber, GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc, Action<ICollection<StructureObj>> OnLoadedCallback)
        {
            MixedLocalAndRemoteQueryResults<long, LocationObj> locResults = Store.Locations.GetObjectsInRegionAsync(SectionNumber,
                                                                                                                    bounds,
                                                                                                                    MinRadius,
                                                                                                                    LastQueryUtc,
                                                                                                                    (locs) => OnLoadedCallback([.. locs.Select(l => l.Parent)]));
            ICollection<LocationObj> known_locations = Store.Locations.GetObjectsInRegion(SectionNumber, bounds, MinRadius, LastQueryUtc);

            ICollection<StructureObj> known_structs = [.. known_locations.Select(l => l.Parent)];

            return new MixedLocalAndRemoteQueryResults<long, StructureObj>(locResults.ServerRequestResult, known_structs);
        }

        public ICollection<StructureObj> GetLocalObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius) => [.. Store.Locations.GetLocalObjectsInRegion(SectionNumber, bounds, MinRadius).Select(l => l.Parent).Distinct()];
    }
}

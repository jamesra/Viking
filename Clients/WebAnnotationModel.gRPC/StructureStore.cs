using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    public readonly struct CreateStructureResult
    {
        public readonly StructureObj Structure;
        public readonly LocationObj Location;
          
        public CreateStructureResult(StructureObj structure, LocationObj location)
        {
            Structure = structure;
            Location = location;
        }
    }


    public class StructureStore : StoreBaseWithKeyAndParent<long, StructureObj, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>, IRegionQuery<long, StructureObj>, IStructureStore
    {
        private readonly IStructureLinkStore StructureLinkStore;
        private readonly IServerAnnotationsClientFactory<IStructureRepository> StructureClientFactory;
        private readonly IObjectConverter<ILocation, LocationObj> ServerLocationObjToObjConverter;

        public StructureStore( 
                 IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>> clientFactory,
                  IServerAnnotationsClientFactory<IStructureRepository> structureClientFactory,
                 IStoreServerQueryResultsHandler<long, StructureObj, IStructure> serverQueryResultsHandler,
                  IObjectConverter<StructureObj, IStructure> objToServerObjConverter,
                IObjectConverter<IStructure, StructureObj> serverObjToObjConverter,
                IObjectUpdater<StructureObj, IStructure> objUpdater,
                IObjectConverter<ILocation, LocationObj> serverLocationObjToObjConverter)
            : base(clientFactory, serverQueryResultsHandler, objToServerObjConverter, serverObjToObjConverter)
        {
            StructureClientFactory = structureClientFactory;
            ServerLocationObjToObjConverter = serverLocationObjToObjConverter;
        }

        /// <summary>
        /// Get the location ID's and positions for branches that are incomplete
        /// </summary>
        /// <returns></returns>
        public void GetUnfinishedBranchesWithPosition(long structureID)
        {
            /*using (AnnotateStructures.AnnotateStructuresClient proxy = CreateProxy())
            {
                return proxy.GetUnfinishedLocationsWithPosition(structureID);
            }*/
            throw new NotImplementedException();
        }
           
        protected override Task Init()
        {
            
#if DEBUG
            //            GetAllStructures(); 
#else
//            GetAllStructures(); 
#endif
            return Task.CompletedTask;
        }

        public async Task<ICollection<StructureObj>> GetAll()
        {
            Trace.WriteLine("GetAllStructures, Begin", "WebAnnotation");

            var client = StructureClientFactory.GetOrCreate();
            var result = await client.GetAll();
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(DateTime.UtcNow, result, Array.Empty<long>()));
            CallOnCollectionChanged(changes);
            return changes.ObjectsInStore;
        }

        public async Task TryRemoveIfOrphan(long ID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var numLocs = await client.NumberOfLocations(ID);
            
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
                    await client.Delete(ID, CancellationToken.None);
                    Trace.WriteLine($"Removing childless structure: {ID}", "WebAnnotation");
                }
                catch (Exception)
                {
                    //             System.Windows.Forms.MessageBox.Show("Delete failed.  Structure may have had child location added or already been deleted. Exception: " + e.ToString(), "Survivable error"); 
                    throw;
                }
                //  }
            }
        }

        public async Task<CreateStructureResult> Create(StructureObj newStruct, LocationObj newLocation)
        {
            var client = StructureClientFactory.GetOrCreate();

            var serverResult = await client.Create(new CreateStructureRequestParameter(newStruct, newLocation), CancellationToken.None);
            
            var obj = await Add(ServerObjConverter.Convert(serverResult.Structure));
            var result = new CreateStructureResult(obj,
                ServerLocationObjToObjConverter.Convert(serverResult.Location));
             
            return result;

            /*
            AnnotateStructures.AnnotateStructuresClient proxy = null;

            created_loc = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                CreateStructureRetval retval = proxy.CreateStructure(newStruct.GetData(), newLocation.GetData());

                //We should not insert created objects into the store before they are created on the server
                Debug.Assert(this.GetObjectByID(newStruct.ID, false) == null);

                StructureObj created_struct = new StructureObj(retval.structure);

                ChangeInventory<StructureObj> inventory = InternalAdd(created_struct);
                created_loc = new LocationObj(retval.location);

                CallOnCollectionChangedForAdd(new StructureObj[] { created_struct });
                Store.Locations.AddFromFriend(new LocationObj[] { created_loc });

                return created_struct;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                StructureObj deletedObj = InternalDelete(newStruct.ID);
                if (deletedObj != null)
                    CallOnCollectionChangedForDelete(new StructureObj[] { deletedObj });

                return null;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }
            */
        }

        public override async Task<bool> Remove(StructureObj obj)
        {
            obj.DBAction = DBACTION.DELETE;
            return true;
        }

        public async Task<ICollection<StructureObj>> GetChildStructures(long ID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var result = await client.GetChildStructures(ID);
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(DateTime.UtcNow, result, Array.Empty<long>()));
            CallOnCollectionChanged(changes);
            return changes.ObjectsInStore;
        }

        public async Task<long> Merge(long KeepID, long MergeID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var response = await client.MergeStructures(KeepID, MergeID);

            throw new NotImplementedException("Need to update the affected locations");
            return response;

            /*
            AnnotateStructures.AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                KeepID = proxy.Merge(KeepID, MergeID);

                LocationObj[] locations = Store.Locations.GetLocalObjectsForStructure(MergeID);
                Store.Locations.Refresh(locations.Select(l => l.ID).ToArray());

                this.ForgetLocally(MergeID);

                return 0;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                throw;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }

            return 0;
            */
        }

        public async Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var splitStructureID = await client.SplitStructureAtLocationLink(KeepLocID, SplitLocID);

            throw new NotImplementedException();

            return splitStructureID;
            /*
            AnnotateStructures.AnnotateStructuresClient proxy = null;
            try
            {
                proxy = CreateProxy();
                proxy.Open();

                long SplitStructureID = proxy.SplitAtLocationLink(KeepLocID, SplitLocID);

                LocationObj keepLoc = Store.Locations.GetObjectByID(KeepLocID);
                LocationObj[] locations = Store.Locations.GetLocalObjectsForStructure(keepLoc.ParentID.Value);
                Store.Locations.Refresh(locations.Select(l => l.ID).ToArray());

                LocationObj[] SplitLocations = Store.Locations.GetLocalObjectsForStructure(SplitStructureID);
                Store.Locations.Refresh(SplitLocations.Select(l => l.ID).ToArray());

                Store.LocationLinks.ForgetLocally(new LocationLinkKey(KeepLocID, SplitLocID));

                return SplitStructureID;
            }
            catch (Exception e)
            {
                ShowStandardExceptionMessage(e);
                throw;
            }
            finally
            {
                if (proxy != null)
                    proxy.Close();
            }
            */
        }


        public async Task<ICollection<StructureObj>> GetStructuresOfType(long StructureTypeID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var result = await client.GetStructuresOfType(StructureTypeID);
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(DateTime.UtcNow, result, Array.Empty<long>()));
            CallOnCollectionChanged(changes);
            return changes.ObjectsInStore;
        }

        public Task<ICollection<StructureObj>> GetLocalObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<StructureObj>> GetServerObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc, out DateTime queryCompletedTime)
        {
            throw new NotImplementedException();
        }

        public Task<StructureLinkObj> GetLinksForStructure(bool AskServer)
        {
            throw new NotImplementedException();
        }
          
        /*
        public ICollection<StructureObj> GetServerObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc)
        {
            ICollection<LocationObj> known_locations = Store.Locations.GetObjectsInRegion(SectionNumber, bounds, MinRadius, LastQueryUtc);

            return known_locations.Select(l => l.Parent).Distinct().ToList();
        }

        
        public MixedLocalAndRemoteQueryResults<long, StructureObj> GetObjectsInRegionAsync(long SectionNumber, GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc, Action<ICollection<StructureObj>> OnLoadedCallback)
        {
            MixedLocalAndRemoteQueryResults<long, StructureObj> results;

            MixedLocalAndRemoteQueryResults<long, LocationObj> locResults = Store.Locations.GetObjectsInRegionAsync(SectionNumber,
                                                                                                                    bounds,
                                                                                                                    MinRadius,
                                                                                                                    LastQueryUtc,
                                                                                                                    (locs) => OnLoadedCallback(locs.Select(l => l.Parent).ToList()));
            ICollection<LocationObj> known_locations = Store.Locations.GetObjectsInRegion(SectionNumber, bounds, MinRadius, LastQueryUtc);

            ICollection<StructureObj> known_structs = known_locations.Select(l => l.Parent).ToList();

            return new MixedLocalAndRemoteQueryResults<long, StructureObj>(locResults.ServerRequestResult, known_structs);
        }
        

        public ICollection<StructureObj> GetLocalObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius)
        {
            return Store.Locations.GetLocalObjectsInRegion(SectionNumber, bounds, MinRadius).Select(l => l.Parent).Distinct().ToList();
        }*/
    }
}

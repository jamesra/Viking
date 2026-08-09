using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;
using ProtoLocationPositionOnly = Viking.AnnotationServiceTypes.gRPC.V1.Protos.LocationPositionOnly;

namespace WebAnnotationModel.gRPC
{
    public class StructureStore : StoreBaseWithKeyAndParent<long, StructureObj, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>, IRegionQuery<long, StructureObj>, IStructureStore
    {
        private readonly IStructureLinkStore StructureLinkStore;
        private readonly IServerAnnotationsClientFactory<IStructureRepository> StructureClientFactory;
        private readonly IObjectConverter<ILocation, LocationObj> ServerLocationObjToObjConverter;

        public StructureStore( 
                 IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>> clientFactory,
                  IServerAnnotationsClientFactory<IStructureRepository> structureClientFactory,
                  IObjectConverter<StructureObj, IStructure> objToServerObjConverter,
                IObjectConverter<IStructure, StructureObj> serverObjToObjConverter,
                IObjectUpdater<StructureObj, IStructure> objUpdater,
                IObjectConverter<ILocation, LocationObj> serverLocationObjToObjConverter,
                IStructureLinkStore structureLinkStore)
            : base(clientFactory, null, objToServerObjConverter, serverObjToObjConverter)
        {
            StructureClientFactory = structureClientFactory;
            ServerLocationObjToObjConverter = serverLocationObjToObjConverter;
            StructureLinkStore = structureLinkStore;
        }

        /// <summary>
        /// Get the location ID's for branches that are incomplete
        /// </summary>
        /// <returns></returns>
        public long[] GetUnfinishedBranches(long structureID)
        {
            var client = StructureClientFactory.GetOrCreate();
            return client.GetUnfinishedLocations(structureID).Result;
        }

        /// <summary>
        /// Get unfinished branch tips with mosaic position and radius from the server.
        /// </summary>
        public WebAnnotationModel.LocationPositionOnly[] GetUnfinishedBranchesWithPosition(long structureID)
        {
            var client = StructureClientFactory.GetOrCreate();
            ProtoLocationPositionOnly[] tips = client.GetUnfinishedLocationsWithPosition(structureID).Result;
            return tips.Select(t =>
            {
                var z = t.Position?.HasZ == true ? t.Position.Z : 0;
                var pos = t.Position == null
                    ? default
                    : new GridVector3(t.Position.X, t.Position.Y, z);
                return new WebAnnotationModel.LocationPositionOnly(t.Id, pos, t.Radius);
            }).ToArray();
        }

        /// <summary>
        /// Fire-and-forget request to delete the structure on the server if it has no locations.
        /// </summary>
        public Task CheckForOrphan(long ID) => TryRemoveIfOrphan(ID);

        /// <summary>
        /// Synchronous convenience alias for GetChildStructures.
        /// </summary>
        public ICollection<StructureObj> GetChildStructuresForStructure(long ID) => GetChildStructures(ID).Result;
           
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
            var queryTime = DateTime.UtcNow;
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(queryTime, result, Array.Empty<long>()));
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
            await OnServerObjectsLoaded(result, queryTime);
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

        public async Task<(StructureObj Structure, LocationObj Location)> Create(StructureObj newStruct, LocationObj newLocation)
        {
            var client = StructureClientFactory.GetOrCreate();

            var serverResult = await client.Create(new CreateStructureRequestParameter(newStruct, newLocation), CancellationToken.None);
            
            var obj = await Add(ServerObjConverter.Convert(serverResult.Structure));
            var result = (obj, ServerLocationObjToObjConverter.Convert(serverResult.Location));
             
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

        public override Task<bool> Remove(StructureObj obj)
        {
            // Match LocationStore / StoreBaseWithKey: mark DELETE, drop from the local index,
            // and queue for Save() so DeepDeleteStructure runs on the next flush.
            return base.Remove(obj);
        }

        public async Task<ICollection<StructureObj>> GetChildStructures(long ID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var result = await client.GetChildStructures(ID);
            var queryTime = DateTime.UtcNow;
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(queryTime, result, Array.Empty<long>()));
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
            await OnServerObjectsLoaded(result, queryTime);
            return changes.ObjectsInStore;
        }

        public async Task<long> Merge(long KeepID, long MergeID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var keptId = await client.MergeStructures(KeepID, MergeID);

            LocationObj[] mergedLocations = Store.Locations.GetLocalObjectsForStructure(MergeID);
            if (mergedLocations.Length > 0)
            {
                await Store.Locations.Refresh(mergedLocations.Select(l => l.ID).ToArray(), CancellationToken.None);
            }

            ForgetLocally(MergeID);
            await Refresh(KeepID, CancellationToken.None);

            return keptId;
        }

        public async Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var splitStructureID = await client.SplitStructureAtLocationLink(KeepLocID, SplitLocID);

            LocationObj keepLoc = await Store.Locations.GetObjectByID(KeepLocID, true, false, CancellationToken.None);
            if (keepLoc?.ParentID != null)
            {
                var keepLocations = await Store.Locations.GetStructureLocations(keepLoc.ParentID.Value, QueryTargets.Server);
                await Store.Locations.Refresh(keepLocations.Select(l => l.ID).ToArray(), CancellationToken.None);
            }

            var splitLocations = await Store.Locations.GetStructureLocations(splitStructureID, QueryTargets.Server);
            await Store.Locations.Refresh(splitLocations.Select(l => l.ID).ToArray(), CancellationToken.None);

            Store.LocationLinks.ForgetLocally(new LocationLinkKey(KeepLocID, SplitLocID));

            return splitStructureID;
        }

        /// <summary>
        /// Synchronous convenience wrapper over SplitStructureAtLocationLink for legacy UI call sites.
        /// </summary>
        public long SplitAtLocationLink(long KeepLocID, long SplitLocID) => SplitStructureAtLocationLink(KeepLocID, SplitLocID).Result;


        public async Task<ICollection<StructureObj>> GetStructuresOfType(long StructureTypeID)
        {
            var client = StructureClientFactory.GetOrCreate();
            var result = await client.GetStructuresOfType(StructureTypeID);
            var queryTime = DateTime.UtcNow;
            var changes = await ServerQueryResultsHandler.ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(queryTime, result, Array.Empty<long>()));
            await CallOnCollectionChanged(changes).ConfigureAwait(false);
            await OnServerObjectsLoaded(result, queryTime);
            return changes.ObjectsInStore;
        }

        public Task<ICollection<StructureObj>> GetLocalObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius)
        {
            var structures = Store.Locations.GetLocalObjectsForSection(SectionNumber).Values
                .Where(l => l.Radius >= MinRadius && bounds.Contains(l.Position) && l.Parent != null)
                .Select(l => l.Parent)
                .Distinct()
                .ToList();
            return Task.FromResult<ICollection<StructureObj>>(structures);
        }

        public Task<ICollection<StructureObj>> GetServerObjectsInRegion(long SectionNumber, GridRectangle bounds, double MinRadius, DateTime? LastQueryUtc, out DateTime queryCompletedTime)
        {
            var client = StructureClientFactory.GetOrCreate();
            string regionWkt = ToWktPolygon(bounds);
            var update = ((IServerSpatialAnnotationsClient<long, IStructure>)client)
                .GetAsync(SectionNumber, regionWkt, MinRadius, LastQueryUtc, CancellationToken.None)
                .GetAwaiter().GetResult();

            queryCompletedTime = update.QueryTime;
            var changes = ServerQueryResultsHandler
                .ProcessServerUpdate(new ServerUpdate<long, IStructure[]>(update.QueryTime, update.NewOrUpdated, update.DeletedIDs))
                .GetAwaiter().GetResult();
            CallOnCollectionChanged(changes).GetAwaiter().GetResult();
            OnServerObjectsLoaded(update.NewOrUpdated, update.QueryTime).GetAwaiter().GetResult();
            return Task.FromResult<ICollection<StructureObj>>(changes.ObjectsInStore);
        }

        /// <summary>
        /// Hydrate StructureLinkStore from Structure.Links embedded on section/region/by-ID responses.
        /// </summary>
        protected override Task OnServerObjectsLoaded(IEnumerable<IStructure> objs, DateTime queryTime)
        {
            return StructureLinkStore.MergeServerLinksAsync(
                objs.Where(s => s != null).SelectMany(s => s.Links ?? Array.Empty<IStructureLink>()),
                queryTime);
        }

        /// <summary>
        /// Legacy interface member without a structure ID; returns null. Prefer StructureLinks.GetLinks(structureId).
        /// </summary>
        public Task<StructureLinkObj> GetLinksForStructure(bool AskServer) =>
            Task.FromResult<StructureLinkObj>(null);

        private static string ToWktPolygon(GridRectangle bounds)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(ci,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel
{
    /// <summary>
    /// The client side store for server objects in Viking
    /// </summary>
    public interface IStore<OBJECT> : INotifyCollectionChanged
    { 
        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        Task<OBJECT> Add(OBJECT obj);

        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        Task<ICollection<OBJECT>> Add(ICollection<OBJECT> obj);

        /// <summary>
        /// Remove the passed object from the local store and server.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        Task<bool> Remove(OBJECT obj);

        /// <summary>
        /// Push every locally changed (added/updated/deleted) object in the store to the server.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> Save(CancellationToken token);

        /// <summary>
        /// Synchronous convenience overload equivalent to Save(CancellationToken.None).
        /// </summary>
        Task<bool> Save();

        /// <summary>
        /// Fired when objects are added, removed or replaced in the store.
        /// This mirrors <see cref="INotifyCollectionChanged.CollectionChanged"/> but is exposed
        /// directly on the interface so callers do not need to cast to subscribe.
        /// </summary>
        event NotifyCollectionChangedEventHandler OnCollectionChanged;
    }

    /// <summary>
    /// A store where all objects in the store have a unique ID
    /// </summary>
    /// <typeparam name="KEY">The type of the unique ID</typeparam>
    /// <typeparam name="OBJECT">The object being stored</typeparam>
    public interface IStoreWithKey<KEY, OBJECT> : IStore<OBJECT>
        where KEY : struct, IEquatable<KEY>
    {
        Task<OBJECT> GetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added);

        bool Contains(KEY key);

        Task<OBJECT> Remove(KEY key);

        Task<OBJECT> GetObjectByID(KEY ID, CancellationToken token);

        /// <summary>
        /// Synchronous convenience accessor equivalent to GetObjectByID(ID, AskServer: true, ForceRefreshFromServer: false, CancellationToken.None).
        /// Kept for the large amount of legacy UI call sites that expect synchronous access.
        /// </summary>
        OBJECT this[KEY index] { get; }

        /// <summary>
        /// Synchronous convenience overload equivalent to this[ID].
        /// </summary>
        OBJECT GetObjectByID(KEY ID);

        /// <summary>
        /// Synchronous convenience overload equivalent to GetObjectByID(ID, AskServer, ForceRefreshFromServer: false, CancellationToken.None).
        /// </summary>
        OBJECT GetObjectByID(KEY ID, bool AskServer);

        Task<OBJECT> GetObjectByID(KEY ID, bool AskServer, bool ForceRefreshFromServer, CancellationToken token);

        Task<List<OBJECT>> GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer, CancellationToken token);

        /// <summary>
        /// Synchronous convenience overload equivalent to GetObjectsByIDs(IDs, AskServer, CancellationToken.None).
        /// </summary>
        ICollection<OBJECT> GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer);

        /// <summary>
        /// Delete data for an object from the store and request the latest version from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<OBJECT> Refresh(KEY key, CancellationToken token);

        /// <summary>
        /// Delete data for an object from the store and request the latest version from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<IList<OBJECT>> Refresh(KEY[] keys, CancellationToken token);

        /// <summary>
        /// Forget the object on the client.  This will force a refresh from the
        /// server if the object is requested again
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        OBJECT ForgetLocally(KEY key);

        /// <summary>
        /// Forget the object on the client.  This will force a refresh from the
        /// server if the object is requested again
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        List<OBJECT> ForgetLocally(KEY[] keys);
    }
     
    /// <summary>
    /// A store with a hierarchical element where objects in the store may optionally have a parent object. i.e. a tree structure.
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    /// <typeparam name="OBJECT"></typeparam>
    public interface IStoreWithParent<KEY, OBJECT> : IStoreWithKey<KEY, OBJECT>, INotifyCollectionChanged
        where KEY : struct, IEquatable<KEY>
    {
        /// <summary>
        /// All objects in the store with no parent
        /// </summary>
        ReadOnlyObservableCollection<KEY> RootObjects { get; }
    }

    /// <summary>
    /// A store that can index its objects by section number
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    /// <typeparam name="OBJECT"></typeparam>
    internal interface ISectionIndexedStore<KEY, OBJECT>
    {
        Task<ConcurrentDictionary<KEY, OBJECT>> GetObjectsForSectionAsync(long SectionNumber, QueryTargets targets);

        void CancelExcessSectionQueries(int LoadingSectionLimit);
        /// <summary>
        /// This is called to instruct the store to eliminate objects from the oldest section query.
        /// This is done to save memory
        /// </summary>
        /// <param name="LoadedSectionLimit">Number of loaded sections we want in memory</param>
        /// <param name="LoadingSectionLimit">Number of sections we want to be actively loading</param>
        void FreeExcessSections(int LoadedSectionLimit, int LoadingSectionLimit);

        /// <summary>
        /// Free all resources and objects related to the section.
        /// </summary>
        /// <param name="SectionNumber"></param>
        /// <returns>True if the section resources were freed.</returns>
        bool RemoveSection(long SectionNumber);
    }

    /// <summary>
    /// Responsible for updating local resources with server objects
    /// Implemented to handle the interface between server objects loaded by requests and properly injecting them into IStore objects
    /// This version can handle results that have multiple object types embedded that need to be inserted into multiple stores
    /// </summary>
    internal interface IServerQuerySingleAddOrUpdateHandler<SERVER_OBJECT>
    {
        /// <summary>
        /// This method should not return until all objects are processed and inserted into the store
        /// </summary>
        /// <param name="queryTime">When the query executed on the server</param>
        /// <param name="obj"></param>
        /// <returns></returns>
        Task ProcessServerResult(DateTime queryTime, SERVER_OBJECT obj);
    }

    internal interface IServerQueryMultipleAddsOrUpdatesHandler<SERVER_OBJECT>
    {
        /// <summary>
        /// This method should not return until all objects are processed and inserted into the store
        /// </summary>
        /// /// <param name="queryTime">When the query executed on the server</param>
        /// <param name="obj"></param>
        /// <returns></returns>
        Task ProcessServerResults(DateTime queryTime, SERVER_OBJECT[] objs);
    }

    internal interface IServerQueryDeleteHandler<KEY>
    {
        Task ProcessServerDelete(KEY deletedID);

        Task ProcessServerDelete(KEY[] deletedIDs);
    }


    /// <summary>
    /// Implemented to handle the interface between server objects loaded by requests and properly injecting them into IStore objects
    /// </summary>
    public interface IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT>
    {  
        Task<ChangeInventory<OBJECT>> ProcessServerUpdate(ServerUpdate<KEY, SERVER_OBJECT> update);

        Task<ChangeInventory<OBJECT>> ProcessServerUpdate(ServerUpdate<KEY, SERVER_OBJECT[]> update);

        Task<ChangeInventory<OBJECT>> ProcessServerUpdate(SERVER_OBJECT[] addorupdateObjs, KEY[] deletedIds);
        
        /// <summary>
        /// Send notification that changes have been processed
        /// </summary>
        /// <param name="changes"></param>
        /// <returns></returns>
        Task EndBatch(ChangeInventory<OBJECT> changes);
    }

    /// <summary>
    /// Allows access to the local store to add/remove objects based on server updates.
    /// Events for these changes will not be triggered until CallOnCollectionChanged is
    /// invoked.  It is the users responsibilty to trigger CallOnCollectionChanged for
    /// any edits made with this interface
    /// </summary>
    public interface IStoreEditor<KEY, OBJECT>
    {
        bool TryAddObject(OBJECT newObj);
           
        bool TryGetObject(KEY ID, out OBJECT obj);

        OBJECT GetOrAdd(KEY key, Func<KEY, OBJECT> valueFactory);

        OBJECT TryRemoveObject(KEY key);
        
        /// <summary>
        /// Send notifications that all edits have been completed.
        /// </summary>
        /// <param name="changes"></param>
        /// <returns></returns>
        Task EndBatch(ChangeInventory<OBJECT> changes);
    }
     
    internal interface ISectionQueryLogger
    {
        void LogQuery(string Description, long SectionNumber, long numObjects, DateTime StartTime, DateTime QueryEndTime,
            DateTime ParseEndTime);
    }

    public interface IQueryLogger
    {
        void LogQuery(string Description, long numObjects, DateTime StartTime, DateTime QueryEndTime,
            DateTime ParseEndTime);
    }

    public interface ILocationStore : IStoreWithKey<long, LocationObj>
    {
        Task<LocationObj> GetLastModifiedLocation();

        Task<ICollection<LocationObj>> GetStructureLocations(long structureId, QueryTargets targets);

        /// <summary>
        /// Synchronous convenience wrapper over GetStructureLocations(structureID, QueryTargets.Server).
        /// </summary>
        ICollection<LocationObj> GetLocationsForStructure(long StructureID);

        /// <summary>
        /// Create a new location on the server.  Add the location to the local store.
        /// </summary>
        LocationObj Create(LocationObj new_location, long[] linked_locations = null);

        List<LocationObj> GetStructureLocationChangeLog(long structureid);

        /// <summary>
        /// Objects known locally to belong to the given section, without contacting the server.
        /// </summary>
        ConcurrentDictionary<long, LocationObj> GetLocalObjectsForSection(long SectionNumber);

        /// <summary>
        /// Objects known locally to belong to the given structure, without contacting the server.
        /// </summary>
        LocationObj[] GetLocalObjectsForStructure(long StructureID);

        /// <summary>
        /// Instruct the store to evict cached sections beyond the given limits to save memory.
        /// </summary>
        void FreeExcessSections(int LoadedSectionLimit, int LoadingSectionLimit);

        /// <summary>
        /// Check the local cache only, without contacting the server.
        /// </summary>
        bool TryGetValue(long ID, out LocationObj obj);
    }

    public interface ILocationLinkStore : IStoreWithKey<LocationLinkKey, LocationLinkObj>
    {
        /// <summary>
        /// Create a link between two locations on the server and add it to the local store.
        /// </summary>
        LocationLinkObj CreateLink(long A, long B);

        /// <summary>
        /// Delete a link between two locations on the server and remove it from the local store.
        /// </summary>
        bool DeleteLink(long A, long B);

        /// <summary>
        /// Load (or incrementally refresh) location links that touch <paramref name="section"/>,
        /// applying server-reported deletes. Optional on backends that do not support section sync.
        /// </summary>
        Task GetLinksForSectionAsync(long section, DateTime? modifiedAfter = null, CancellationToken token = default);

        /// <summary>
        /// Merge location links embedded on Location.Links (or equivalent) into the local store.
        /// </summary>
        Task MergeServerLinksAsync(IEnumerable<ILocationLink> links, DateTime? queryTime = null, CancellationToken token = default);
    }

    public interface IStructureStore : IStoreWithParent<long, StructureObj>
    {
        Task<StructureLinkObj> GetLinksForStructure(bool AskServer);

        Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID);

        /// <summary>
        /// Synchronous convenience overload equivalent to SplitStructureAtLocationLink(KeepLocID, SplitLocID).
        /// </summary>
        long SplitAtLocationLink(long KeepLocID, long SplitLocID);

        Task<ICollection<StructureObj>> GetStructuresOfType(long StructureTypeID);

        Task<ICollection<StructureObj>> GetAll();

        Task<ICollection<StructureObj>> GetChildStructures(long StructureID);

        /// <summary>
        /// Create a new structure and its first location on the server.  Adds both to their local stores.
        /// </summary>
        Task<(StructureObj Structure, LocationObj Location)> Create(StructureObj newStruct, LocationObj newLocation);

        Task<long> Merge(long KeepID, long MergeID);

        /// <summary>
        /// Fire-and-forget request to delete the structure on the server if it has no locations.
        /// </summary>
        Task CheckForOrphan(long ID);

        /// <summary>
        /// Synchronous convenience alias for GetChildStructures.
        /// </summary>
        ICollection<StructureObj> GetChildStructuresForStructure(long ID);

        /// <summary>
        /// Get the location IDs for branches that are incomplete.
        /// </summary>
        long[] GetUnfinishedBranches(long structureID);

        /// <summary>
        /// Get the location IDs and positions for branches that are incomplete.
        /// </summary>
        LocationPositionOnly[] GetUnfinishedBranchesWithPosition(long structureID);
    }

    /// <summary>
    /// Lightweight, transport-agnostic replacement for the old WCF-era AnnotationService.Types.LocationPositionOnly.
    /// </summary>
    public readonly struct LocationPositionOnly
    {
        public readonly long ID;
        public readonly Geometry.GridVector3 Position;
        public readonly double Radius;

        public LocationPositionOnly(long id, Geometry.GridVector3 position, double radius)
        {
            ID = id;
            Position = position;
            Radius = radius;
        }
    }
      
    public interface IStructureLinkStore : IStoreWithKey<StructureLinkKey, StructureLinkObj>
    {
        /// <summary>
        /// Return all links to the given structure
        /// </summary>
        /// <param name="structureId"></param>
        /// <returns></returns>
        Task<StructureLinkObj[]> GetLinks(long structureId);

        /// <summary>
        /// Merge structure links embedded on section/region Structure responses into the local store.
        /// </summary>
        Task MergeServerLinksAsync(IEnumerable<IStructureLink> links, DateTime? queryTime = null, CancellationToken token = default);

        /// <summary>
        /// Synchronous convenience wrapper over Add(obj).
        /// </summary>
        StructureLinkObj Create(StructureLinkObj obj);
    }

    public interface IStructureTypeStore : IStoreWithParent<long, StructureTypeObj>
    {
        Task<ICollection<StructureTypeObj>> GetAll();

        Task<StructureTypeObj> Create(StructureTypeObj new_type, CancellationToken token);

        /// <summary>
        /// Synchronous convenience overload equivalent to Create(new_type, CancellationToken.None).
        /// </summary>
        StructureTypeObj Create(StructureTypeObj new_type);
    }

    public interface IPermittedStructureLinkStore : IStoreWithKey<PermittedStructureLinkKey, PermittedStructureLinkObj>
    {

    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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

        /// <summary>
        /// Cache-only lookup. Throws if the key is not already in the store.
        /// Use when the object is known to be loaded (on-screen view, clicked annotation).
        /// Does not contact the server. For a possible miss, use <see cref="TryGetObjectByID"/>.
        /// To fetch if missing, use <see cref="GetObjectByID(KEY, CancellationToken)"/>.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The key is not in the local cache.</exception>
        OBJECT this[KEY key] { get; }

        /// <summary>
        /// Cache-only lookup. Does not contact the server.
        /// </summary>
        /// <returns><c>true</c> if the object was in the local cache; otherwise <c>false</c> and <paramref name="obj"/> is null.</returns>
        bool TryGetObjectByID(KEY key, [NotNullWhen(true)] out OBJECT obj);

        /// <summary>
        /// Cache-only bulk lookup. Does not contact the server.
        /// <paramref name="found"/> and <paramref name="missing"/> are in request order.
        /// </summary>
        /// <returns><c>true</c> if every key was in the cache (<paramref name="missing"/> empty).</returns>
        bool TryGetObjectsByIDs(ICollection<KEY> keys, out IReadOnlyList<OBJECT> found, out IReadOnlyList<KEY> missing);

        /// <summary>
        /// Returns the object from the local cache, or fetches it from the server if it is not cached.
        /// Does not replace an already-cached object; use <see cref="Refresh(KEY, CancellationToken)"/> for that.
        /// </summary>
        /// <returns>The cached or fetched object, or <c>null</c> if the server has no such key.</returns>
        /// <seealso cref="TryGetObjectByID"/>
        /// <seealso cref="Refresh(KEY, CancellationToken)"/>
        Task<OBJECT> GetObjectByID(KEY ID, CancellationToken token = default);

        /// <summary>
        /// Returns objects from the local cache, then fetches cache-miss keys from the server.
        /// Does not replace already-cached objects; use <see cref="Refresh(ICollection{KEY}, CancellationToken)"/> for that.
        /// <see cref="GetByIDResult{KEY, OBJECT}.Found"/> is hits plus newly fetched, in request order.
        /// <see cref="GetByIDResult{KEY, OBJECT}.Missing"/> is requested keys the server does not have (deleted or never existed)
        /// after this call — not a cache miss. Keys that were already cached are left as-is and listed in Found even if
        /// another client has since deleted them; use <see cref="Refresh(ICollection{KEY}, CancellationToken)"/> to discover that.
        /// If the by-ID RPC reports DeletedIds for requested keys, those keys are evicted and listed in Missing.
        /// </summary>
        /// <seealso cref="TryGetObjectsByIDs"/>
        Task<GetByIDResult<KEY, OBJECT>> GetObjectsByIDs(ICollection<KEY> IDs, CancellationToken token = default);

        /// <summary>
        /// Deletes the local row and reloads it from the server.
        /// </summary>
        /// <returns>The refreshed object, or <c>null</c> if the server no longer has this key.</returns>
        /// <seealso cref="GetObjectByID(KEY, CancellationToken)"/>
        Task<OBJECT> Refresh(KEY key, CancellationToken token = default);

        /// <summary>
        /// Deletes the local rows and reloads them from the server.
        /// <see cref="GetByIDResult{KEY, OBJECT}.Missing"/> is keys the server no longer has.
        /// </summary>
        /// <seealso cref="GetObjectsByIDs"/>
        Task<GetByIDResult<KEY, OBJECT>> Refresh(ICollection<KEY> keys, CancellationToken token = default);

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

        /// <summary>
        /// Loads all locations for a structure from the server and adds them to the local store.
        /// </summary>
        Task<ICollection<LocationObj>> GetStructureLocations(long structureId, QueryTargets targets);

        /// <summary>
        /// Creates a new location on the server and adds it to the local store.
        /// Optionally creates location links to <paramref name="linked_locations"/>.
        /// </summary>
        Task<LocationObj> Create(LocationObj new_location, long[] linked_locations = null);

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
    }

    public interface ILocationLinkStore : IStoreWithKey<LocationLinkKey, LocationLinkObj>
    {
        /// <summary>
        /// Creates a link between two locations on the server and adds it to the local store.
        /// </summary>
        Task<LocationLinkObj> CreateLink(long A, long B);

        /// <summary>
        /// Deletes a link between two locations on the server and removes it from the local store.
        /// </summary>
        Task<bool> DeleteLink(long A, long B);

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
        /// <summary>
        /// Legacy interface member without a structure ID; returns null. Prefer StructureLinks.GetLinks(structureId).
        /// </summary>
        Task<StructureLinkObj> GetLinksForStructure();

        /// <summary>
        /// Splits a structure at the location link between <paramref name="KeepLocID"/> and <paramref name="SplitLocID"/>.
        /// Contacts the server.
        /// </summary>
        Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID);

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
        /// Location IDs for incomplete branches, loaded from the server.
        /// </summary>
        Task<long[]> GetUnfinishedBranches(long structureID);

        /// <summary>
        /// Location IDs and positions for incomplete branches, loaded from the server.
        /// </summary>
        Task<LocationPositionOnly[]> GetUnfinishedBranchesWithPosition(long structureID);
    }

    /// <summary>
    /// Lightweight, transport-agnostic replacement for the old WCF-era AnnotationService.Types.LocationPositionOnly.
    /// </summary>
    public readonly struct LocationPositionOnly
    {
        public readonly long ID;
        public readonly Geometry.Vector3 Position;
        public readonly double Radius;

        public LocationPositionOnly(long id, Geometry.Vector3 position, double radius)
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
        /// Creates a structure link on the server and adds it to the local store.
        /// </summary>
        Task<StructureLinkObj> Create(StructureLinkObj obj);
    }

    public interface IStructureTypeStore : IStoreWithParent<long, StructureTypeObj>
    {
        Task<ICollection<StructureTypeObj>> GetAll();

        /// <summary>
        /// Creates a structure type on the server and adds it to the local store.
        /// </summary>
        Task<StructureTypeObj> Create(StructureTypeObj new_type, CancellationToken token = default);
    }

    public interface IPermittedStructureLinkStore : IStoreWithKey<PermittedStructureLinkKey, PermittedStructureLinkObj>
    {

    }
}

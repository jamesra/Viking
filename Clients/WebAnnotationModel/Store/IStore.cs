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

        //Task<OBJECT> this[KEY index] { get; }

        Task<OBJECT> GetObjectByID(KEY ID, bool AskServer, bool ForceRefreshFromServer, CancellationToken token);

        Task<List<OBJECT>> GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer, CancellationToken token);

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
    }

    public interface ILocationLinkStore : IStoreWithKey<LocationLinkKey, LocationLinkObj>
    {

    }

    public interface IStructureStore : IStoreWithParent<long, StructureObj>
    {
        Task<StructureLinkObj> GetLinksForStructure(bool AskServer);

        Task<long> SplitStructureAtLocationLink(long KeepLocID, long SplitLocID);

        Task<ICollection<StructureObj>> GetStructuresOfType(long StructureTypeID);

        Task<ICollection<StructureObj>> GetAll();

        Task<ICollection<StructureObj>> GetChildStructures(long StructureID); 
    }
      
    public interface IStructureLinkStore : IStoreWithKey<StructureLinkKey, StructureLinkObj>
    {
        /// <summary>
        /// Return all links to the given structure
        /// </summary>
        /// <param name="structureId"></param>
        /// <returns></returns>
        Task<StructureLinkObj[]> GetLinks(long structureId); 
    }

    public interface IStructureTypeStore : IStoreWithParent<long, StructureTypeObj>
    {
        Task<ICollection<StructureTypeObj>> GetAll();
    }

    public interface IPermittedStructureLinkStore : IStoreWithKey<PermittedStructureLinkKey, PermittedStructureLinkObj>
    {

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC
{
    public class StoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT> : IStoreServerQueryResultsHandler<KEY, OBJECT, SERVER_OBJECT>
        where KEY : struct, IEquatable<KEY>, IComparable<KEY> 
        where OBJECT : AnnotationModelObjBaseWithKey<KEY, SERVER_OBJECT>, IEquatable<OBJECT>, IDataObjectWithKey<KEY>
        where SERVER_OBJECT : IEquatable<SERVER_OBJECT>, IDataObjectWithKey<KEY>
    {
        private enum ProcessResult
        {
            Add, Update, Unchanged
        }

        private readonly IStoreEditor<KEY, OBJECT> _StoreEditor;
        private readonly IObjectConverter<SERVER_OBJECT, OBJECT> ServerObjConverter;
        private readonly IObjectUpdater<OBJECT, SERVER_OBJECT> ClientObjUpdater;

        public StoreServerQueryResultsHandler(IStoreEditor<KEY, OBJECT> storeBaseWithKey,
            IObjectConverter<SERVER_OBJECT, OBJECT> serverObjToObjConverter,
            IObjectUpdater<OBJECT, SERVER_OBJECT> objUpdater = null)
        {
            _StoreEditor = storeBaseWithKey;
            ServerObjConverter = serverObjToObjConverter;
            ClientObjUpdater = objUpdater;
        }
        public Task<ChangeInventory<OBJECT>> ProcessServerUpdate(ServerUpdate<KEY, SERVER_OBJECT> update)
        {
            return ProcessServerObjects(new SERVER_OBJECT[] {update.NewOrUpdated}, update.DeletedIDs);
        }

        public Task<ChangeInventory<OBJECT>> ProcessServerUpdate(ServerUpdate<KEY, SERVER_OBJECT[]> update)
        {
            return ProcessServerObjects( update.NewOrUpdated, update.DeletedIDs);
        }

        public Task<ChangeInventory<OBJECT>> ProcessServerUpdate(SERVER_OBJECT[] addorupdateObjs, KEY[] deletedIds)
        {
            return ProcessServerObjects(addorupdateObjs, deletedIds);
        }

        public async Task<ChangeInventory<OBJECT>> ProcessServerObjects(UpdateResults<KEY, SERVER_OBJECT> update)
        {
            return await ProcessServerObjects(update.AddedObjects.Union(update.UpdatedObjects).ToArray(),
                update.DeletedIDs);
        }

        public async Task<ChangeInventory<OBJECT>> ProcessServerObjects(SERVER_OBJECT[] addorupdateObjs, KEY[] deletedIds)
        {
            var inventory = new ChangeInventory<OBJECT>();
            inventory.DeletedObjects.AddRange(InternalDelete(deletedIds));
            inventory = await ProcessServerInsertsOrUpdates(addorupdateObjs, inventory);
            return inventory;
        } 

        private async Task<ChangeInventory<OBJECT>> ProcessServerInsertsOrUpdates(SERVER_OBJECT[] serverObjs, ChangeInventory<OBJECT> inventory=null)
        {
            if (inventory is null)
                inventory = new ChangeInventory<OBJECT>();
              
            var results = new ProcessResult[serverObjs.Length];
            var tasks = new Task<OBJECT>[serverObjs.Length];

            async Task<OBJECT> GetOrAddTask(SERVER_OBJECT u, int i)
            {
                var added = false;
                var co = _StoreEditor.GetOrAdd(u.ID, (k) =>
                {
                    added = true;
                    var addedObj = ServerObjConverter.Convert(u); 
                    return addedObj;
                });
                
                if (added)
                {
                    results[i] = ProcessResult.Add;
                    return co;
                }

                if(ClientObjUpdater != null && await ClientObjUpdater.Update(co, u))
                    results[i] = ProcessResult.Update;
                else
                    results[i] = ProcessResult.Unchanged;

                return co;
            }

            for (int i = 0; i < serverObjs.Length; i++)
            {
                var o = serverObjs[i];
                var j = i;
                tasks[i] = Task.Run(async () => await GetOrAddTask(o, j));
            }
             
            for (int i = 0; i < serverObjs.Length; i++)
            {
                switch (results[i])
                {
                    case ProcessResult.Add:
                        inventory.AddedObjects.Add(await tasks[i]);
                        break;
                    case ProcessResult.Update:
                        inventory.UpdatedObjects.Add(await tasks[i]);
                        break;
                    case ProcessResult.Unchanged:
                        inventory.UnchangedObjects.Add(await tasks[i]);
                        break;
                    default:
                        throw new NotImplementedException($"Unexpected result {results[i]}");
                }
            }
             

            /*
             //Simpler original version
            foreach (var u in serverObjs)
            {
                bool added = false;
                var clientObj = IDToObject.GetOrAdd(u.ID, (k) =>
                {
                    added = true;
                    return ServerObjConverter.Convert(u);
                });

                if (added)
                    inventory.AddedObjects.Add(clientObj);
                else
                {
                    if(ClientObjUpdater.Update(clientObj, u))
                        inventory.UpdatedObjects.Add(clientObj);
                    else
                        inventory.UnchangedObjects.Add(clientObj);
                } 
            }
            */

            return inventory;
        }

        /// <summary>
        /// Delete the specified keys.  Return the objects removed from the store.
        /// </summary>
        /// <param name="Keys"></param>
        /// <returns>A list of removed objects or null if no object was found</returns>
        protected virtual List<OBJECT> InternalDelete(KEY[] Keys)
        {
            List<OBJECT> listDeleted = new List<OBJECT>(Keys.Length);

            for (int iObj = 0; iObj < Keys.Length; iObj++)
            {
                KEY Key = Keys[iObj];
                OBJECT removedObj = _StoreEditor.TryRemoveObject(Key);
                listDeleted.Add(removedObj);
            }

            //CallOnCollectionChangedForDelete(listDeleted);

            return listDeleted;
        }

        public Task EndBatch(ChangeInventory<OBJECT> changes)
        {
            return _StoreEditor.EndBatch(changes);
        }
    }
}
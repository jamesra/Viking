using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAnnotationModel.Store
{
    /// <summary>
    /// The client side store for server objects in Viking
    /// </summary>
    public interface IStore<OBJECT> : INotifyCollectionChanged
    {
        OBJECT Add(OBJECT obj);

        OBJECT Add(ICollection<OBJECT> obj);

        OBJECT Remove(OBJECT obj); 
    }

    public interface IStoreWithUniqueKey<KEY, OBJECT> : IStore<OBJECT>, INotifyCollectionChanged
        where KEY : struct, IEquatable<KEY>
    {
        OBJECT GetOrAdd(KEY key, Func<KEY, OBJECT> createFunc, out bool added);

        bool Contains(KEY key);

        OBJECT Remove(KEY key);

        OBJECT GetObjectByID(KEY ID);

        OBJECT this[KEY index] { get; }

        OBJECT GetObjectByID(KEY ID, bool AskServer, bool ForceRefreshFromServer = false);

        List<OBJECT> GetObjectsByIDs(ICollection<KEY> IDs, bool AskServer);

        /// <summary>
        /// Delete data for an object from the store and request the latest version from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        OBJECT Refresh(KEY key);

        /// <summary>
        /// Delete data for an object from the store and request the latest version from the server
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        OBJECT Refresh(KEY[] keys);

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
        /// <param name="key"></param>
        /// <returns></returns>
        OBJECT ForgetLocally(KEY[] key);
    }

    public interface IStoreWithParent<KEY, OBJECT> : IStore<OBJECT>, INotifyCollectionChanged
        where KEY : struct, IEquatable<KEY>
    {
        /// <summary>
        /// All objects in the store with no parent
        /// </summary>
        KEY[] RootObjects { get; }

        /// <summary>
        /// Notifies about changes to the RootObjects collection
        /// </summary>
        event PropertyChangedEventHandler PropertyChanged;
    }
}

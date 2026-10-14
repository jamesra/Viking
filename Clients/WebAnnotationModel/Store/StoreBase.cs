using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading;
using WebAnnotationModel.Objects;

namespace WebAnnotationModel
{
    /// <summary>
    /// Returned type used by queries which return a local cache immediately and a handle on the server request for updates
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    /// <typeparam name="OBJECT"></typeparam>
    public class MixedLocalAndRemoteQueryResults<KEY, OBJECT>(IAsyncResult result, ICollection<OBJECT> known_objects)
    {
        public readonly IAsyncResult ServerRequestResult = result;
        public readonly ICollection<OBJECT> KnownObjects = known_objects;
    }

    /// <summary>
    /// Groups the objects according to how the store saw them during an operation.  Used to 
    /// store up all changes so a single set of collection changed events can be sent
    /// </summary>
    /// <typeparam name="OBJECT"></typeparam>
    public class ChangeInventory<OBJECT>
    {
        /// <summary>
        /// Objects freshly added to the store
        /// </summary>
        public List<OBJECT> AddedObjects;

        /// <summary>
        /// Objects that existed in the store but had some properties updated
        /// </summary>
        public List<OBJECT> UpdatedObjects;

        /// <summary>
        /// Objects we deleted from the store
        /// </summary>
        public List<OBJECT> DeletedObjects;

        /// <summary>
        /// An object that was in the store but was removed and replaced with a new object
        /// </summary>
        public List<OBJECT> OldObjectsReplaced;

        /// <summary>
        /// Objects that are now in the store and replaced an object that existed previously, common when server sends a new ID on update
        /// </summary>
        public List<OBJECT> NewObjectReplacements;

        /// <summary>
        /// Objects that were found already existing in the store and required no updates
        /// </summary>
        public List<OBJECT> UnchangedObjects;

        public ChangeInventory()
        {
            AddedObjects = [];
            UpdatedObjects = [];
            DeletedObjects = [];
            OldObjectsReplaced = [];
            NewObjectReplacements = [];
            UnchangedObjects = [];

        }

        public ChangeInventory(int numObjects)
        {
            AddedObjects = new List<OBJECT>(numObjects);
            UpdatedObjects = new List<OBJECT>(numObjects);
            DeletedObjects = new List<OBJECT>(numObjects);
            OldObjectsReplaced = new List<OBJECT>(numObjects);
            NewObjectReplacements = new List<OBJECT>(numObjects);
            UnchangedObjects = new List<OBJECT>(numObjects);
        }

        /// <summary>
        /// Return a concatenation of all affected objects minus objects that were deleted
        /// </summary>
        /// <returns></returns>
        public List<OBJECT> ObjectsInStore
        {
            get
            {
                List<OBJECT> listObjects = new(AddedObjects.Count + UpdatedObjects.Count + NewObjectReplacements.Count);
                listObjects.AddRange(AddedObjects);
                listObjects.AddRange(UpdatedObjects);
                listObjects.AddRange(NewObjectReplacements);
                listObjects.AddRange(UnchangedObjects);
                return listObjects;
            }
        }

        /// <summary>
        /// Add all elements from another inventory to our own
        /// </summary>
        /// <param name="inventory"></param>
        public void Add(ChangeInventory<OBJECT> inventory)
        {
            AddedObjects.AddRange(inventory.AddedObjects);
            UpdatedObjects.AddRange(inventory.UpdatedObjects);
            DeletedObjects.AddRange(inventory.DeletedObjects);
            OldObjectsReplaced.AddRange(inventory.OldObjectsReplaced);
            NewObjectReplacements.AddRange(inventory.NewObjectReplacements);
            UnchangedObjects.AddRange(inventory.UnchangedObjects);
        }
    }

    public class ThreadLocalProxyFactory<IService>(ChannelFactory<IService> factory)
    {
        private readonly ChannelFactory<IService> _factory = factory;

        static ThreadLocalProxyFactory()
        {
            // Subscribe to the ProcessExit event
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => DisposeChannel();
        }

        private static readonly ThreadLocal<IClientChannel> Channel = new();

        public IClientChannel GetProxy()
        {
            IClientChannel channel = Channel.Value as IClientChannel;

            if (channel is not null && channel.State == CommunicationState.Faulted)
            {
                // Abort the faulted channel
                channel.Abort();
                channel = null;
            }

            if (channel is null ||
                (channel.State == CommunicationState.Closed ||
                 channel.State == CommunicationState.Closing))
            {
                // Create a new channel and open it
                Channel.Value = this._factory.CreateChannel(State.EndpointAddress) as IClientChannel;
                Channel.Value?.Open();
            }

            return (IClientChannel)Channel.Value;
        }

        static void DisposeChannel()
        {
            if (Channel.IsValueCreated)
            {
                if (Channel.Value is IClientChannel channel)
                {
                    try
                    {
                        channel.Close();
                    }
                    catch
                    {
                        channel.Abort();
                    }
                }
                Channel.Dispose();
            }
        }
    }

    /// <summary>
    /// Idle WCF channels lent to one asynchronous call at a time, so concurrent requests do not each pay to open a
    /// channel but no call ever shares a channel with another.
    /// </summary>
    /// <remarks>
    /// Thread safe. A rented channel belongs to the renter until it is handed back with <see cref="Return"/> or
    /// <see cref="Discard"/>, which makes it safe to <see cref="Discard"/> (abort) it from a cancellation callback
    /// while the call is in flight. Never touch a channel after handing it back. Return only a channel whose last call
    /// completed normally; anything else, including a call that timed out, is discarded because its state is unknown.
    /// </remarks>
    /// <typeparam name="TChannel">The channel type, normally <see cref="IClientChannel"/>.</typeparam>
    public sealed class ChannelPool<TChannel> where TChannel : class, ICommunicationObject
    {
        private readonly Func<TChannel> _create;
        private readonly int _maxIdle;
        private readonly ConcurrentStack<TChannel> _idle = new();
        private int _idleCount;

        /// <param name="create">Makes a new, unopened channel.</param>
        /// <param name="maxIdle">Most idle channels kept; extra returned channels are closed.</param>
        public ChannelPool(Func<TChannel> create, int maxIdle = 16)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _maxIdle = maxIdle;
        }

        /// <summary>Number of idle channels waiting to be rented.</summary>
        public int IdleCount => Volatile.Read(ref _idleCount);

        /// <summary>
        /// An open channel nobody else is using. Idle channels that have closed or faulted since they were returned
        /// are dropped.
        /// </summary>
        public TChannel Rent()
        {
            while (_idle.TryPop(out TChannel channel))
            {
                Interlocked.Decrement(ref _idleCount);
                if (channel.State == CommunicationState.Opened)
                    return channel;

                Discard(channel);
            }

            TChannel fresh = _create();
            try
            {
                fresh.Open();
            }
            catch
            {
                Discard(fresh);
                throw;
            }

            return fresh;
        }

        /// <summary>Hands back a channel whose call finished normally so the next call can reuse it.</summary>
        public void Return(TChannel channel)
        {
            if (channel is null)
                return;

            if (channel.State != CommunicationState.Opened || Volatile.Read(ref _idleCount) >= _maxIdle)
            {
                Close(channel);
                return;
            }

            _idle.Push(channel);
            Interlocked.Increment(ref _idleCount);
        }

        /// <summary>Aborts a channel that must not be reused. Safe while a call on it is in flight.</summary>
        public static void Discard(TChannel channel)
        {
            if (channel is null)
                return;

            try
            {
                channel.Abort();
            }
            catch (Exception)
            {
                // Already closed or faulted.
            }
        }

        private static void Close(TChannel channel)
        {
            try
            {
                channel.Close();
            }
            catch (Exception)
            {
                Discard(channel);
            }
        }
    }

    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT> : INotifyCollectionChanged
        where INTERFACE : class
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : AnnotationService.Types.DataObject, new()
        where OBJECT : WCFObjBase<WCFOBJECT>, new()
    {

        protected ChannelFactory<INTERFACE> channelFactory;
        protected ThreadLocalProxyFactory<INTERFACE> proxyFactory;

        //Perform any required initialization
        public abstract void Init();

        /// <summary>
        /// The calling thread's own channel, for synchronous calls. Reused across calls and never closed by the
        /// caller. Do not start an asynchronous call on it: its completion or cancellation would close or abort a
        /// channel the thread's later synchronous calls depend on. Use <see cref="RentAsyncProxy"/> for those.
        /// </summary>
        protected virtual IClientChannel CreateProxy()
        {
            proxyFactory ??= new ThreadLocalProxyFactory<INTERFACE>(channelFactory);

            //return (IClientChannel)channelFactory.CreateChannel(State.EndpointAddress);
            return proxyFactory.GetProxy();
        }

        private ChannelPool<IClientChannel> asyncChannels;

        /// <summary>
        /// A channel for one asynchronous (Begin/End) call, exclusive until released with
        /// <see cref="ReleaseAsyncProxy"/>. Because nothing else uses it, it may be aborted to cancel the call.
        /// </summary>
        protected IClientChannel RentAsyncProxy() =>
            LazyInitializer.EnsureInitialized(ref asyncChannels,
                () => new ChannelPool<IClientChannel>(() => (IClientChannel)channelFactory.CreateChannel(State.EndpointAddress)))
            .Rent();

        /// <summary>
        /// Ends the use of a channel from <see cref="RentAsyncProxy"/>. A channel whose call completed normally goes
        /// back to the pool; any other is aborted. Does nothing for null. Call it once, after cancellation hooks for
        /// the call have been disposed.
        /// </summary>
        /// <param name="completedNormally">True only when the End call returned without an exception.</param>
        protected void ReleaseAsyncProxy(IClientChannel proxy, bool completedNormally)
        {
            if (proxy is null)
                return;

            if (completedNormally && asyncChannels is not null)
                asyncChannels.Return(proxy);
            else
                ChannelPool<IClientChannel>.Discard(proxy);
        }

        #region Public Creation/Removal methods


        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract OBJECT Add(OBJECT obj);


        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item should already exist on the store
        /// Collection change notification events will be sent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract ICollection<OBJECT> Add(ICollection<OBJECT> objs);


        /// <summary>
        /// Remove the passed object from the local store and server.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract bool Remove(OBJECT obj);

        #endregion

        #region Events

        protected void InvokeEventAction(Action a, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
#if DEBUG
            //System.Diagnostics.Trace.WriteLine(string.Format("{0}.{1} Invoking Event Action", this.GetType().FullName, memberName));
#endif
            if (State.UseAsynchEvents)
            {
                System.Threading.Tasks.Task.Run(a);
            }
            else
            {
                a.Invoke();
            }
        }

        internal void CallOnCollectionChanged(ChangeInventory<OBJECT> inventory)
        {
            //Action a = new Action(() =>
            //    {
            CallOnCollectionChangedForDelete(inventory.DeletedObjects);
            CallOnCollectionChangedForReplace(inventory.OldObjectsReplaced, inventory.NewObjectReplacements);
            //CallOnCollectionChangedForReplace(inventory.UpdatedObjects, inventory.UpdatedObjects);                
            CallOnCollectionChangedForAdd(inventory.AddedObjects);
            //    });
            //InvokeEventAction(a); 

        }

        /// <summary>
        /// This is fired when all objects retrieved from a call to the database have been added/updated/removed
        /// It needs to be called on the main UI thread
        /// </summary>
      //  public event OnAllUpdatesCompletedEventHandler OnAllUpdatesCompleted; 

        protected void CallOnCollectionChangedForAdd(ICollection<OBJECT> listAddedObj)
        {
            //InternalUpdate will send its own notification for the updated objects
            if (listAddedObj.Count > 0)
            {
                Action a = new(() =>
                {
                    OBJECT[] listCopy = new OBJECT[listAddedObj.Count];
                    listAddedObj.CopyTo(listCopy, 0);
                    //CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listAddedObj));
                    CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
                });

                InvokeEventAction(a);
            }
        }

        protected void CallOnCollectionChangedForDelete(ICollection<OBJECT> listObj)
        {
            //InternalUpdate will send its own notification for the updated objects
            if (listObj.Count > 0)
            {
                Action a = new(() =>
                {
                    OBJECT[] listCopy = new OBJECT[listObj.Count];
                    listObj.CopyTo(listCopy, 0);
                    //CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listAddedObj));
                    CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, listCopy));
                });

                InvokeEventAction(a);
            }
        }


        protected void CallOnCollectionChangedForReplace(ICollection<OBJECT> listOldObjects, ICollection<OBJECT> listNewObjects)
        {
            Debug.Assert(listOldObjects.Count == listNewObjects.Count);
            if (listNewObjects.Count > 0)
            {
                Action a = new(() =>
                {
                    OBJECT[] listOldObjectsCopy = new OBJECT[listOldObjects.Count];
                    OBJECT[] listNewObjectsCopy = new OBJECT[listNewObjects.Count];
                    listOldObjects.CopyTo(listOldObjectsCopy, 0);
                    listNewObjects.CopyTo(listNewObjectsCopy, 0);
                    NotifyCollectionChangedEventArgs e = new(NotifyCollectionChangedAction.Replace,
                                                                                              listNewObjectsCopy, listOldObjectsCopy);
                    CallOnCollectionChanged(e);
                });

                InvokeEventAction(a);
            }
        }


        private void CallOnCollectionChanged(NotifyCollectionChangedEventArgs e) => OnCollectionChanged?.Invoke(this, e);/*
             if (OnCollectionChanged != null)
            {
                OnCollectionChanged(this, e);
                //System.Threading.Tasks.Task.Factory.StartNew(() => OnCollectionChanged(this, e));
                //Action a = new Action(() => OnCollectionChanged(this, e));
                //a.BeginInvoke(null, null);

                //Because we are handling collection changes these events need to appear in order, however there are
                //too many cascading events...  RIght now the worst case is a location doesn't show in the UI as expected.
                //This can be fixed by implementing the replaced collection change action for delete instead of using
                //remove and then add.  When we separate the operation the order can be flipped.
                /*
                Action a = new Action(() => OnCollectionChanged(this, e));
                if (State.UseAsynchEvents)
                {
                    a.BeginInvoke(null, null);
                }
                else
                {
                    a.Invoke(); 
                }
                *///}

        /*
        protected void CallOnAllUpdatesCompleted(OnAllUpdatesCompletedEventArgs e)
        {            
            if (OnAllUpdatesCompleted != null)
            {
                OnAllUpdatesCompleted(this, e);
            }
        }*/


        #endregion

        protected void ShowStandardExceptionMessage(Exception e)
        {
            Trace.WriteLine(e.ToString());
            Trace.WriteLine(e.Message);
            //System.Windows.Forms.MessageBox.Show("An error occurred:\n" + e.Message, "WebAnnotation");
        }

        #region Proxy Calls


        #endregion

        #region INotifyCollectionChanged Members


        public event NotifyCollectionChangedEventHandler OnCollectionChanged;
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add => OnCollectionChanged += value;
            remove => OnCollectionChanged -= value;
        }

        #endregion


    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Windows;


namespace WebAnnotation.ViewModel
{
    class NotifyCollectionChangedEventManager : WeakEventManager
    {
        static int CleanupCountdown = 5000;

        static public NotifyCollectionChangedEventManager Current = new NotifyCollectionChangedEventManager();

        static NotifyCollectionChangedEventManager()
        {
            WeakEventManager.SetCurrentManager(typeof(INotifyCollectionChanged), Current);
        }

        ConcurrentDictionary<object, NotifyCollectionChangedEventHandler> ObjectToHandler = new ConcurrentDictionary<object, NotifyCollectionChangedEventHandler>();

        protected override void StartListening(object source)
        {
            //Check if we can subscribe to the source
            INotifyCollectionChanged INotify = source as INotifyCollectionChanged;
            System.Diagnostics.Debug.Assert(INotify != null, "Attempt to create weak subscription to object that does not support it");
            if (INotify == null)
                return;

            NotifyCollectionChangedEventHandler eventHandler = new NotifyCollectionChangedEventHandler(this.OnEvent);
            eventHandler = ObjectToHandler.GetOrAdd(source, eventHandler);

            INotify.CollectionChanged += eventHandler;

            if (CleanupCountdown == 0)
            {
                this.ScheduleCleanup();
                CleanupCountdown = 5000;
            }

            CleanupCountdown--;
        }

        protected override void StopListening(object source)
        {
            //Check if we can subscribe to the source
            INotifyCollectionChanged INotify = source as INotifyCollectionChanged;
            if (INotify == null)
                return;

            NotifyCollectionChangedEventHandler eventHandler = null;
            bool Removed = ObjectToHandler.TryRemove(source, out eventHandler);
            if (Removed)
            {
                INotify.CollectionChanged -= eventHandler;
            }
        }

        /// <summary>
        /// According to MSDN all public methods on WeakEventManager are thread safe
        /// </summary>
        /// <param name="source"></param>
        /// <param name="listener"></param>
        public static void AddListener(Object source, IWeakEventListener listener)
        {
            Current.ProtectedAddListener(source, listener);
        }

        /// <summary>
        /// According to MSDN all public methods on WeakEventManager are thread safe
        /// </summary>
        /// <param name="source"></param>
        /// <param name="listener"></param>
        public static void RemoveListener(Object source, IWeakEventListener listener)
        {
            Current.ProtectedRemoveListener(source, listener);
        }

        delegate void DeliverEventsDelegate(object o, NotifyCollectionChangedEventArgs e);
        protected void OnEvent(object source, NotifyCollectionChangedEventArgs e)
        {
            //I managed to avoid invoking events on the main thread by eliminating bugs in the IWeakEvenListener classes.  You get odd crashes if they return false.

            //DeliverEventsDelegate del = new DeliverEventsDelegate(this.DeliverEvent);
            //this.Dispatcher.BeginInvoke(del, new object[] { source, e });
            this.DeliverEvent(source, e);
        }

    }
}

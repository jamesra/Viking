using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Windows;


namespace WebAnnotation.ViewModel
{
    internal class NotifyCollectionChangedEventManager : WeakEventManager
    {
        private static int CleanupCountdown = 5000;

        public static NotifyCollectionChangedEventManager Current = new();

        static NotifyCollectionChangedEventManager()
        {
            WeakEventManager.SetCurrentManager(typeof(INotifyCollectionChanged), Current);
        }

        private readonly ConcurrentDictionary<object, NotifyCollectionChangedEventHandler> ObjectToHandler = new();

        protected override void StartListening(object source)
        {
            //Check if we can subscribe to the source
            INotifyCollectionChanged INotify = source as INotifyCollectionChanged;
            System.Diagnostics.Debug.Assert(INotify != null, "Attempt to create weak subscription to object that does not support it");
            if (INotify is null)
            {
                return;
            }

            NotifyCollectionChangedEventHandler eventHandler = new(OnEvent);
            eventHandler = ObjectToHandler.GetOrAdd(source, eventHandler);

            INotify.CollectionChanged += eventHandler;

            if (CleanupCountdown == 0)
            {
                ScheduleCleanup();
                CleanupCountdown = 5000;
            }

            CleanupCountdown--;
        }

        protected override void StopListening(object source)
        {
            //Check if we can subscribe to the source
            if (source is not INotifyCollectionChanged INotify)
            {
                return;
            }

            bool Removed = ObjectToHandler.TryRemove(source, out NotifyCollectionChangedEventHandler eventHandler);
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
        public static void AddListener(object source, IWeakEventListener listener) => Current.ProtectedAddListener(source, listener);

        /// <summary>
        /// According to MSDN all public methods on WeakEventManager are thread safe
        /// </summary>
        /// <param name="source"></param>
        /// <param name="listener"></param>
        public static void RemoveListener(object source, IWeakEventListener listener) => Current.ProtectedRemoveListener(source, listener);

        private delegate void DeliverEventsDelegate(object o, NotifyCollectionChangedEventArgs e);
        protected void OnEvent(object source, NotifyCollectionChangedEventArgs e) =>
            //I managed to avoid invoking events on the main thread by eliminating bugs in the IWeakEvenListener classes.  You get odd crashes if they return false.

            //DeliverEventsDelegate del = new DeliverEventsDelegate(this.DeliverEvent);
            //this.Dispatcher.BeginInvoke(del, new object[] { source, e });
            DeliverEvent(source, e);

    }
}

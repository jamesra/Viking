using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows;


namespace WebAnnotation.ViewModel
{
    internal class NotifyPropertyChangingEventManager : WeakEventManager
    {
        private static int CleanupCountdown = 5000;

        public static NotifyPropertyChangingEventManager Current = new();



        static NotifyPropertyChangingEventManager()
        {
            WeakEventManager.SetCurrentManager(typeof(INotifyPropertyChanging), Current);
        }

        private readonly ConcurrentDictionary<object, PropertyChangingEventHandler> ObjectToHandler = new();

        protected override void StartListening(object source)
        {
            //Check if we can subscribe to the source
            INotifyPropertyChanging INotify = source as INotifyPropertyChanging;
            System.Diagnostics.Debug.Assert(INotify != null, "Attempt to create weak subscription to object that does not support it");
            if (INotify is null)
            {
                return;
            }

            PropertyChangingEventHandler eventHandler = new(OnPropertyChanging);
            eventHandler = ObjectToHandler.GetOrAdd(source, eventHandler);

            INotify.PropertyChanging += eventHandler;

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
            if (source is not INotifyPropertyChanging INotify)
            {
                return;
            }

            bool Removed = ObjectToHandler.TryRemove(source, out PropertyChangingEventHandler eventHandler);
            if (Removed)
            {
                INotify.PropertyChanging -= eventHandler;
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

        private delegate void DeliverEventsDelegate(object o, PropertyChangingEventArgs e);
        protected void OnPropertyChanging(object source, PropertyChangingEventArgs e) =>
            //DeliverEventsDelegate del = new DeliverEventsDelegate(this.DeliverEvent);
            //this.Dispatcher.BeginInvoke(del, new object[] { source, e});
            DeliverEvent(source, e);
    }
}

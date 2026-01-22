using System.ComponentModel;
using System.Windows;


namespace WebAnnotation.ViewModel
{
    internal class NotifyPropertyChangedEventManager : WeakEventManager
    {
        private static int CleanupCountdown = 5000;
        public static NotifyPropertyChangedEventManager Current = new();
        private readonly PropertyChangedEventHandler eventHandler;

        static NotifyPropertyChangedEventManager()
        {
            WeakEventManager.SetCurrentManager(typeof(INotifyPropertyChanged), Current);
        }

        public NotifyPropertyChangedEventManager()
        {
            eventHandler = new PropertyChangedEventHandler(OnPropertyChanged);
        }

        //ConcurrentDictionary<object, PropertyChangedEventHandler> ObjectToHandler = new ConcurrentDictionary<object, PropertyChangedEventHandler>();

        protected override void StartListening(object source)
        {
            //Check if we can subscribe to the source
            INotifyPropertyChanged INotify = source as INotifyPropertyChanged;
            System.Diagnostics.Debug.Assert(INotify != null, "Attempt to create weak subscription to object that does not support it");
            if (INotify is null)
            {
                return;
            }

            INotify.PropertyChanged += eventHandler;

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
            if (source is not INotifyPropertyChanged INotify)
            {
                return;
            }

            //PropertyChangedEventHandler eventHandler = null;
            //bool Removed = ObjectToHandler.TryRemove(source, out eventHandler);
            //if (Removed)
            //{
            INotify.PropertyChanged -= eventHandler;
            //}


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

        private delegate void DeliverEventsDelegate(object o, PropertyChangedEventArgs e);

        protected void OnPropertyChanged(object source, PropertyChangedEventArgs e) =>
            //DeliverEventsDelegate del = new DeliverEventsDelegate(this.DeliverEvent);
            // this.Dispatcher.BeginInvoke(del, new object[] { source, e });
            DeliverEvent(source, e);
    }
}

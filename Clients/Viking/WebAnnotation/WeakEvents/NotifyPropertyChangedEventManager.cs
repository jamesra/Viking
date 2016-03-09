using System;
using System.Collections.Generic;
using System.Collections.Concurrent; 
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel; 


namespace WebAnnotation.ViewModel
{
    class NotifyPropertyChangedEventManager : WeakEventManager
    {
        static int CleanupCountdown = 5000; 
        static public NotifyPropertyChangedEventManager Current = new NotifyPropertyChangedEventManager();
        private PropertyChangedEventHandler eventHandler;

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
            if (INotify == null)
                return;
            
            INotify.PropertyChanged += eventHandler;

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
            INotifyPropertyChanged INotify = source as INotifyPropertyChanged;
            if (INotify == null)
                return;

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

        delegate void DeliverEventsDelegate(object o, PropertyChangedEventArgs e);

        protected void OnPropertyChanged(object source, PropertyChangedEventArgs e)
        {
           //DeliverEventsDelegate del = new DeliverEventsDelegate(this.DeliverEvent);
           // this.Dispatcher.BeginInvoke(del, new object[] { source, e });
           this.DeliverEvent(source, e);
        }
    }
}

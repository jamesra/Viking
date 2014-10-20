using System;
using System.Collections.Generic;
using System.Collections.Concurrent; 
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel; 


namespace WebAnnotation.ViewModel
{
    class NotifyPropertyChangingEventManager : WeakEventManager
    {
        static int CleanupCountdown = 5000; 

        static public NotifyPropertyChangingEventManager Current = new NotifyPropertyChangingEventManager();

        

        static NotifyPropertyChangingEventManager()
        {
            WeakEventManager.SetCurrentManager(typeof(INotifyPropertyChanging), Current);            
        }

        ConcurrentDictionary<object, PropertyChangingEventHandler> ObjectToHandler = new ConcurrentDictionary<object, PropertyChangingEventHandler>();
        
        protected override void StartListening(object source)
        {
            //Check if we can subscribe to the source
            INotifyPropertyChanging INotify = source as INotifyPropertyChanging;
            System.Diagnostics.Debug.Assert(INotify != null, "Attempt to create weak subscription to object that does not support it");
            if (INotify == null)
                return;

            PropertyChangingEventHandler eventHandler = new PropertyChangingEventHandler(this.OnPropertyChanging);
            eventHandler = ObjectToHandler.GetOrAdd(source, eventHandler);

            INotify.PropertyChanging += eventHandler;

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
            INotifyPropertyChanging INotify = source as INotifyPropertyChanging;
            if (INotify == null)
                return;

            PropertyChangingEventHandler eventHandler = null;
            bool Removed = ObjectToHandler.TryRemove(source, out eventHandler);
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

        delegate void DeliverEventsDelegate(object o, PropertyChangingEventArgs e);
        protected void OnPropertyChanging(object source, PropertyChangingEventArgs e)
        {
            //DeliverEventsDelegate del = new DeliverEventsDelegate(this.DeliverEvent);
            //this.Dispatcher.BeginInvoke(del, new object[] { source, e});
            DeliverEvent(source, e);
        }
    }
}

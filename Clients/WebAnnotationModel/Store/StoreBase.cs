using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using WebAnnotationModel.Service; 
using WebAnnotationModel.Objects;
using System.Diagnostics; 
using System.Collections.Concurrent;
using System.Collections.Specialized; 

namespace WebAnnotationModel
{
    /// <summary>
    /// Returned type used by queries which return a local cache immediately and a handle on the server request for updates
    /// </summary>
    /// <typeparam name="KEY"></typeparam>
    /// <typeparam name="OBJECT"></typeparam>
    public class MixedLocalAndRemoteQueryResults<KEY, OBJECT>
    {
        public readonly IAsyncResult ServerRequestResult = null;
        public readonly ConcurrentDictionary<KEY, OBJECT> KnownObjects = null; 

        public MixedLocalAndRemoteQueryResults(IAsyncResult result, ConcurrentDictionary<KEY, OBJECT> known_objects)
        {
            this.ServerRequestResult = result;
            this.KnownObjects = known_objects; 
        }
    }
    /// <summary>
    /// This base class implements the basic functionality to talk to a WCF Service
    /// </summary>
    public abstract class StoreBase<PROXY, INTERFACE, OBJECT, WCFOBJECT> : INotifyCollectionChanged
        where INTERFACE : class        
        where PROXY : System.ServiceModel.ClientBase<INTERFACE>
        where WCFOBJECT : DataObject, new()
        where OBJECT : WCFObjBase<WCFOBJECT>, new()
    {
        //Perform any required initialization
        public abstract void Init();

        protected abstract PROXY CreateProxy();

                
        #region Public Creation/Removal methods

        /// <summary>
        /// Create a local instance of a new item in the store
        /// This item is not sent to the server until save is 
        /// called.
        /// 
        /// Each store took a different set of parameters so I removed this, but it belongs here in spirit
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract OBJECT Add(OBJECT obj);

        /// <summary>
        /// Remove the passed object from the store. The item will be
        /// deleted from the server until save is called
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public abstract bool Remove(OBJECT obj); 

        #endregion

        #region Events

        /// <summary>
        /// This is fired when all objects retrieved from a call to the database have been added/updated/removed
        /// It needs to be called on the main UI thread
        /// </summary>
      //  public event OnAllUpdatesCompletedEventHandler OnAllUpdatesCompleted; 

        protected void CallOnCollectionChangedForAdd(List<OBJECT> listAddedObj)
        {
            //InternalUpdate will send its own notification for the updated objects
            if (listAddedObj.Count > 0)
            {                
                OBJECT[] listCopy = new OBJECT[listAddedObj.Count];
                listAddedObj.CopyTo(listCopy);
                //CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listAddedObj));
                CallOnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, listCopy));
            }
        }

        protected void CallOnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            lock (this)
            {
                if (OnCollectionChanged != null)
                {
                    //System.Threading.Tasks.Task.Factory.StartNew(() => OnCollectionChanged(this, e));
                    //Action a = new Action(() => OnCollectionChanged(this, e));
                    //a.BeginInvoke(null, null);

                    //Because we are handling collection changes these events need to appear in order, however there are
                    //too many cascading events...  RIght now the worst case is a location doesn't show in the UI as expected.
                    //This can be fixed by implementing the replaced collection change action for delete instead of using
                    //remove and then add.  When we seperate the operation the order can be flipped.
                    Action a = new Action(() => OnCollectionChanged(this, e));
                    a.BeginInvoke(null, null);
                    //OnCollectionChanged(this, e);
                }
            }
        }

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

        /// <summary>
        /// Update the server with the new values
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="objects"></param>
        /// <returns></returns>
        protected abstract long[] ProxyUpdate(PROXY proxy, WCFOBJECT[] objects);

        #endregion

        #region INotifyCollectionChanged Members


        public event NotifyCollectionChangedEventHandler OnCollectionChanged;
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add { OnCollectionChanged += value;  }
            remove { OnCollectionChanged -= value;  }
        }

        #endregion

        
    }
}

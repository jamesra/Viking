using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebAnnotationModel.Service;
using System.ComponentModel;
using System.Diagnostics;

namespace WebAnnotationModel.Objects
{
    /// <summary>
    /// Base of objects used to expose WCF objects, T is the WCF object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    abstract public class WCFObjBase<T> : INotifyPropertyChanged, INotifyPropertyChanging, ICloneable, IDisposable
        where T : DataObject, new()
    {
        protected T Data;

        internal T GetData()
        {
            return Data;
        }
        
        /// <summary>
        /// Called when the database is queried and the results might have new values for our object
        /// </summary>
        /// <param name="newdata"></param>
        internal virtual void Synch(T newdata)
        {
            bool ChangeEvent = false;
            if (this.Data != newdata)
            {
                ChangeEvent = true;
            }

            if (ChangeEvent)
                OnPropertyChanging(""); 

            this.Data = newdata;

            if (ChangeEvent)
                OnPropertyChanged("");
        }

        

        public DBACTION DBAction
        {
            get
            {
                return Data.DBAction;
            }
            set
            {
                if (Data.DBAction == DBACTION.INSERT && value == DBACTION.UPDATE)
                    return;

                OnPropertyChanging("DBAction"); 

                //Just a precaution. I haven't thought whether I could undelete an object
           //     Debug.Assert(false == (Data.DBAction == DBACTION.DELETE && value != DBACTION.DELETE));

                Data.DBAction = value;

                OnPropertyChanged("DBAction");
            }
        }

        protected void SetDBActionForChange()
        {
            DBAction = DBACTION.UPDATE;
        }

        /// <summary>
        /// Include this object in the next update to the database
        /// </summary>
        public void SubmitOnNextUpdate()
        {
            DBAction = DBACTION.UPDATE;
        }

        #region INotifyPropertyChanged Members

        protected void OnPropertyChanging(string property)
        {
            if (_PropertyChanging != null)
            {
                //We need to ensure these events are invoked on the main thread since UI controls listen to them and they can only 
                //change state on the main thread 
                _PropertyChanging.Invoke(this, new PropertyChangingEventArgs(property));
            }
        }

        private event PropertyChangingEventHandler _PropertyChanging;
        private int PropertyChangingSubCount = 0; 

        public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging
        {
            add { _PropertyChanging += value;
            PropertyChangingSubCount++;
  //          Trace.WriteLine("Add OnPropertyChanging: " + PropertyChangingSubCount.ToString() + ", " + value.ToString());
            }
            remove { _PropertyChanging -= value;
            PropertyChangingSubCount--;
            Debug.Assert(PropertyChangingSubCount >= 0, "If subscription count is negative the wrong object has events cancelled and there is a memory leak");
  //          Trace.WriteLine("Remove OnPropertyChanging: " + PropertyChangingSubCount.ToString() + ", " + value.ToString()); 
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        protected void OnPropertyChanged(string property)
        {
            if (_PropertyChanged != null)
            {
                //We need to ensure these events are invoked on the main thread since UI controls listen to them and they can only 
                //change state on the main thread 
                _PropertyChanged.Invoke(this, new PropertyChangedEventArgs(property));
            }
        }

        private event PropertyChangedEventHandler _PropertyChanged;
        private int PropertyChangedSubCount = 0;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged
        {
            add { _PropertyChanged += value;
                PropertyChangedSubCount++;
 //               Trace.WriteLine("Add OnPropertyChanged: " + PropertyChangedSubCount.ToString() + ", " + value.ToString());
                }
            remove { _PropertyChanged -= value; 
                PropertyChangedSubCount--;
                Debug.Assert(PropertyChangedSubCount >= 0, "If subscription count is negative the wrong object has events cancelled and there is a memory leak");
 //               Trace.WriteLine("Remove OnPropertyChanged: " + PropertyChangedSubCount.ToString() + ", " + value.ToString()); 
                }
        }

        #endregion

        public object Clone()
        {
            WCFObjBase<T> objClone = Activator.CreateInstance(this.GetType(), new object[] { this.Data } ) as WCFObjBase<T>;
            return objClone; 

        }

        public void Dispose()
        {
           // Trace.WriteLine("Disposing of object: " + this.ToString());
        }
    }
}

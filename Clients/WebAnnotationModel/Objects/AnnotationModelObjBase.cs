using Viking.AnnotationServiceTypes.Interfaces;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotationModel.Objects
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="SERVER_INTERFACE">Object sent to us by the server with data</typeparam>
    public abstract class AnnotationModelObjBase<SERVER_INTERFACE> : INotifyPropertyChanged, INotifyPropertyChanging, IChangeAction
    {  
        //bool _SynchCalled = false;
        
        /// <summary>
        /// Called by the store after the server is queried and the server results may have new values for this instance
        /// 
        /// The implementation is responsible for calling OnPropertyChanging("") if state was changed by the update
        /// </summary>
        /// <param name="newdata"></param>
        internal abstract Task Update(SERVER_INTERFACE newdata);
            /*
        {
            bool ChangeEvent = false;
            if (false == this.Data.Equals(newdata))
            {
                ChangeEvent = true;
            }

            if (ChangeEvent)
                OnPropertyChanging("");

            //this.Data = newdata;

            if (ChangeEvent)
                OnPropertyChanged("");
        }
        */

        protected Viking.AnnotationServiceTypes.Interfaces.DBACTION _DBAction = Viking.AnnotationServiceTypes.Interfaces.DBACTION.NONE;

        /// <summary>
        /// The action we would like to perform on the server if we submit this model to the store
        /// </summary>
        public Viking.AnnotationServiceTypes.Interfaces.DBACTION DBAction
        {
            get => _DBAction;
            set
            {
                if (_DBAction == Viking.AnnotationServiceTypes.Interfaces.DBACTION.INSERT && value == Viking.AnnotationServiceTypes.Interfaces.DBACTION.UPDATE)
                    return;

                OnPropertyChanging(nameof(DBAction));

                //Just a precaution. I haven't thought whether I could undelete an object
                //     Debug.Assert(false == (Data.DBAction == DBACTION.DELETE && value != DBACTION.DELETE));

                _DBAction = value;

                OnPropertyChanged(nameof(DBAction));
            }
        }

        protected void SetDBActionForChange()
        {
            DBAction = Viking.AnnotationServiceTypes.Interfaces.DBACTION.UPDATE;
        }

        /// <summary>
        /// Include this object in the next update to the database
        /// </summary>
        public void SubmitOnNextUpdate()
        {
            DBAction = Viking.AnnotationServiceTypes.Interfaces.DBACTION.UPDATE;
        }

        #region INotifyPropertyChanged Members

        protected void OnPropertyChanging(string property)
        {
            //We need to ensure these events are invoked on the main thread since UI controls listen to them and they can only 
            //change state on the main thread 
            _PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(property));
        }

        private event PropertyChangingEventHandler _PropertyChanging;
        private int PropertyChangingSubCount = 0;

        public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging
        {
            add
            {
                _PropertyChanging += value;
                PropertyChangingSubCount++;
                //          Trace.WriteLine("Add OnPropertyChanging: " + PropertyChangingSubCount.ToString() + ", " + value.ToString());
            }
            remove
            {
                _PropertyChanging -= value;
                PropertyChangingSubCount--;
                Debug.Assert(PropertyChangingSubCount >= 0, "If subscription count is negative the wrong object has events cancelled and there is a memory leak");
                //          Trace.WriteLine("Remove OnPropertyChanging: " + PropertyChangingSubCount.ToString() + ", " + value.ToString()); 
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        protected void OnPropertyChanged(string property)
        {
            //We need to ensure these events are invoked on the main thread since UI controls listen to them and they can only 
            //change state on the main thread 
            _PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property)); 
        }

        private event PropertyChangedEventHandler _PropertyChanged;
        private int PropertyChangedSubCount = 0;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                _PropertyChanged += value;
                PropertyChangedSubCount++;
                //               Trace.WriteLine("Add OnPropertyChanged: " + PropertyChangedSubCount.ToString() + ", " + value.ToString());
            }
            remove
            {
                _PropertyChanged -= value;
                PropertyChangedSubCount--;
                Debug.Assert(PropertyChangedSubCount >= 0, "If subscription count is negative the wrong object has events cancelled and there is a memory leak");
                //               Trace.WriteLine("Remove OnPropertyChanged: " + PropertyChangedSubCount.ToString() + ", " + value.ToString()); 
            }
        }

        #endregion 
    }
}

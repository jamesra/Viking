using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebAnnotation.Service;
using Viking.Common;
using System.Diagnostics;

namespace WebAnnotation.Objects
{
    /// <summary>
    /// Base of objects used to expose WCF objects, T is the WCF object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    abstract public class WCFObjBase<T> : Viking.Objects.UIObjBase
        where T : DataObject, new()
    {
        protected T Data;

        internal T GetData()
        {
            return Data;
        }

        //These are redundant, but I put them here so the base object can have its methods more protected by default
        public void FireBeforeSaveEvent()
        {
            base.CallBeforeSave();
        }

        public void FireAfterSaveEvent()
        {
            base.CallAfterSave();
        }

        public void FireBeforeDeleteEvent()
        {
            base.CallBeforeSave();
        }

        public void FireAfterDeleteEvent()
        {
            base.CallAfterSave();
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

            this.Data = newdata;

            if (ChangeEvent)
                ValueChangedEvent("");
        }

        

        internal DBACTION DBAction
        {
            get
            {
                return Data.DBAction;
            }
            set
            {
                if (Data.DBAction == DBACTION.INSERT && value == DBACTION.UPDATE)
                    return;

                //Just a precaution. I haven't thought whether I could undelete an object
                Debug.Assert(false == (Data.DBAction == DBACTION.DELETE && value != DBACTION.DELETE));

                Data.DBAction = value;
            }
        }

        protected void SetDBActionForChange()
        {
            DBAction = DBACTION.UPDATE;
            ValueChangedEvent("");
        }

    }
}

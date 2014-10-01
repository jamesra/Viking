using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization; 


namespace Annotation
{
    // I can't use straight inheritance because the relationships do not marshal.  So use interfaces instead

    /// <summary>
    /// A generic database object
    /// </summary>
    [DataContract]
    public abstract class DataObject
    {
        protected DBACTION _DBAction = DBACTION.NONE;

        [DataMember]
        public DBACTION DBAction
        {
            get { return _DBAction; }
            set { _DBAction = value; }
        }
    }

    /// <summary>
    /// A generic database object that exposes a key value
    /// </summary>
    [DataContract]
    public class DataObjectWithKey<T>  : DataObject where T : struct
    {
        protected T _ID; 

        [DataMember]
        public T ID
        {
            get { return _ID; }
            set { _ID = value; }
        }
    }

    /// <summary>
    /// A generic database object that exposes an ID value and Parent of
    /// the same type referring to a row in the same table
    /// </summary>
    [DataContract]
    public class DataObjectWithParent<IDTYPE> : DataObjectWithKey<IDTYPE> where IDTYPE : struct
    {
        protected Nullable<IDTYPE> _ParentID;

        [DataMember]
        public Nullable<IDTYPE> ParentID
        {
            get { if(_ParentID.HasValue)
                    return _ParentID;
                  else
                  {
                      return new Nullable<IDTYPE>();
                  }
            }
            set { _ParentID = value; }
        }
    }
}

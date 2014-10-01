using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebAnnotation.Service;
using Viking.Common;
using System.Diagnostics;

namespace WebAnnotation.Objects
{
    abstract public class WCFObjBaseWithKey<T> : WCFObjBase<T>
        where T : DataObjectWithKeyOflong, new()
    {
        public long ID
        {
            get { return Data.ID; }
        }

        public override string ToString()
        {
            if (Data != null)
                return Data.ID.ToString();

            return "Uninitialized " + base.ToString();
        }

        public override bool Equals(object obj)
        {
            WCFObjBaseWithKey<T> locObj = obj as WCFObjBaseWithKey<T>;
            if (locObj != null)
            {
                return this.ID == locObj.ID;
            }
            else
                return base.Equals(obj);
        }

        int? _HashCode;

        /// <summary>
        /// The ID for new objects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (_HashCode.HasValue)
                return _HashCode.Value;
            else
            {
                _HashCode = new int?((int)(ID % int.MaxValue));
                return _HashCode.Value;
            }

        }
    }
}

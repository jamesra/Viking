﻿using AnnotationService.Types;
using System;

namespace WebAnnotationModel.Objects
{
    abstract public class WCFObjBaseWithKey<KEY, T> : WCFObjBase<T>, IEquatable<WCFObjBaseWithKey<KEY, T>>
        where KEY : struct, IEquatable<KEY>
        where T : DataObject, new()
    {
        public abstract KEY ID { get; }


        public override string ToString()
        {
            if (Data != null)
                return ID.ToString();

            return "Uninitialized " + base.ToString();
        }

        public override bool Equals(object obj)
        {
            WCFObjBaseWithKey<KEY, T> locObj = obj as WCFObjBaseWithKey<KEY, T>;
            if (locObj != null)
            {
                return this.Equals(locObj);
            }
            else
                return base.Equals(obj);
        }

        public static bool operator ==(WCFObjBaseWithKey<KEY, T> A, WCFObjBaseWithKey<KEY, T> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return true;
            }

            if (A is object)
                return A.Equals(B);

            return false;
        }

        public static bool operator !=(WCFObjBaseWithKey<KEY, T> A, WCFObjBaseWithKey<KEY, T> B)
        {
            if (System.Object.ReferenceEquals(A, B))
            {
                return false;
            }

            if (A is object)
                return !A.Equals(B);

            return true;
        }

        protected abstract int GenerateHashCode();
        int? _HashCode;


        /// <summary>
        /// The ID for newo bjects can change from a negative number to the number in the database.
        /// In this case make sure we always return the same hash code.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (_HashCode.HasValue)
                return _HashCode.Value;
            else
            {
                _HashCode = new int?(GenerateHashCode());
                return _HashCode.Value;
            }
        }

        public bool Equals(WCFObjBaseWithKey<KEY, T> other)
        {
            if (other is null)
                return false;

            return this.ID.Equals(other.ID);
        }
    }
}

using System;
using System.Collections.Generic;

namespace WebAnnotationModel
{
    /// <summary>
    /// WCF-era async callback bag for a section query.
    /// </summary>
    public readonly struct GetObjectBySectionCallbackState<T> : IEquatable<GetObjectBySectionCallbackState<T>>
    { 
        public readonly long SectionNumber;

        /// <summary>
        /// Watermark sent with the request. Do not treat this as proof the query succeeded.
        /// </summary>
        public readonly DateTime LastQueryExecutedTime;
        public readonly DateTime StartTime;
        public readonly Action<ICollection<T>> OnLoadCompletedCallBack;

        public override string ToString()
        {
            return SectionNumber.ToString() + " : " + StartTime.TimeOfDay.ToString();
        }

        public bool Equals(GetObjectBySectionCallbackState<T> other)
        {
            return SectionNumber.Equals(other.SectionNumber);
        }

        public GetObjectBySectionCallbackState(long number, DateTime lastQueryExecutedTime, Action<ICollection<T>> LoadCompletedCallback)
        { 
            SectionNumber = number;
            StartTime = DateTime.UtcNow;
            this.LastQueryExecutedTime = lastQueryExecutedTime;
            this.OnLoadCompletedCallBack = LoadCompletedCallback;
        }

        public static int CompareByTime(GetObjectBySectionCallbackState<T> x, GetObjectBySectionCallbackState<T> y)
        {
            return x.StartTime.CompareTo(y.StartTime);
        }
    }
}

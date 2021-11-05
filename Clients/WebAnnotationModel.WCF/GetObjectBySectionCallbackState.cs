using System;
using System.Collections.Generic;

namespace WebAnnotationModel
{
    public class GetObjectBySectionCallbackState<T> : IEquatable<GetObjectBySectionCallbackState<T>>
    { 
        public readonly long SectionNumber;
        public readonly DateTime LastQueryExecutedTime;
        public readonly DateTime StartTime = DateTime.UtcNow;
        public readonly Action<ICollection<T>> OnLoadCompletedCallBack;

        public override string ToString()
        {
            return SectionNumber.ToString() + " : " + StartTime.TimeOfDay.ToString();
        }

        public bool Equals(GetObjectBySectionCallbackState<T> other)
        {
            if ((object)other is null)
                return false;

            return SectionNumber == other.SectionNumber;
        }

        public GetObjectBySectionCallbackState( long number, DateTime lastQueryExecutedTime, Action<ICollection<T>> LoadCompletedCallback)
        { 
            SectionNumber = number;
            this.LastQueryExecutedTime = lastQueryExecutedTime;
            this.OnLoadCompletedCallBack = LoadCompletedCallback;
        }
    }
}

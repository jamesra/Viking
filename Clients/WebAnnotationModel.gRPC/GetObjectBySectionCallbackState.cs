using System;
using System.Collections.Generic;
using System.Threading;

namespace WebAnnotationModel
{
    readonly struct TaskAndToken<T>
    {
        public readonly System.Threading.Tasks.Task<T> Task;
        public readonly CancellationTokenSource TokenSource;
        public readonly DateTime CreationTime;

        public TaskAndToken(System.Threading.Tasks.Task<T> task, CancellationTokenSource source)
        {
            Task = task;
            TokenSource = source;
            CreationTime = DateTime.UtcNow;
        }

        public static int CompareByTime(TaskAndToken<T> x, TaskAndToken<T> y)
        { 
            return x.CreationTime.CompareTo(y.CreationTime);
        }
    }

    readonly struct KeyedTaskAndToken<KEY, T>
    {
        public readonly KEY Key;
        public readonly System.Threading.Tasks.Task<T> Task;
        public readonly CancellationTokenSource TokenSource;
        public readonly DateTime CreationTime;

        public KeyedTaskAndToken(KEY k, System.Threading.Tasks.Task<T> task, CancellationTokenSource source)
        {
            Key = k;
            Task = task;
            TokenSource = source;
            CreationTime = DateTime.UtcNow;
        }

        public static int CompareByTime(TaskAndToken<T> x, TaskAndToken<T> y)
        {
            return x.CreationTime.CompareTo(y.CreationTime);
        }
    }

    public readonly struct GetObjectBySectionCallbackState<T> : IEquatable<GetObjectBySectionCallbackState<T>>
    { 
        public readonly long SectionNumber;
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

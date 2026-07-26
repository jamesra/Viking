using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WebAnnotationModel
{
    public class ConcurrentObservableSet<T> 
        where T : IEquatable<T>
    {
        protected readonly SemaphoreSlim LinkLock = new SemaphoreSlim(1);

        public readonly ObservableCollection<T> Observable;
        public readonly ReadOnlyObservableCollection<T> ReadOnlyObservable;

        public ConcurrentObservableSet()
        {
            Observable = new ObservableCollection<T>();
            ReadOnlyObservable = new ReadOnlyObservableCollection<T>(Observable);
        }

        public ConcurrentObservableSet(IEnumerable<T> collection)
        {
            Observable = new ObservableCollection<T>(collection);
            ReadOnlyObservable = new ReadOnlyObservableCollection<T>(Observable);
        }

        public async Task<T[]> CreateCopyAsync()
        {
            try
            {
                await LinkLock.WaitAsync();
                return Observable.ToArray();
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// This needs sorting out.  Do we need this as an observable collection or should 
        /// we fire our own collection changed events with Add/Remove link calls.
        /// </summary>
        public ReadOnlyObservableCollection<T> Links => ReadOnlyObservable;

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public async Task<bool> ContainsAsync(T ID)
        {
            try
            {
                await LinkLock.WaitAsync();
                return Observable.Contains(ID);
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        protected virtual bool Contains(T ID)
        {
            return Observable.Contains(ID);
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public virtual async Task<bool> AddAsync(T ID)
        { 
            try
            {
                await LinkLock.WaitAsync();
                if (this.Contains(ID))
                    return false;

                Observable.Add(ID);
                return true;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public virtual async Task<bool> UpdateAsync(T ID)
        {
            try
            {
                await LinkLock.WaitAsync();
                int i = this.Observable.IndexOf(ID);
                if(i < 0)
                    return false;

                T existing = Observable[i];
                Observable[i] = ID;
                return true;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Allows LocationLinkStore to adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        /// <returns>true if the item was added.  false if it was updated</returns>
        public virtual async Task<bool> AddOrUpdateAsync(T ID)
        {
            try
            {
                await LinkLock.WaitAsync();
                int i = this.Observable.IndexOf(ID);
                if (i < 0)
                {
                    Observable.Add(ID);
                    return true;
                }

                T existing = Observable[i];
                Observable[i] = ID;
                return false;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public virtual async Task<bool> RemoveAsync(T ID)
        { 
            try
            {
                await LinkLock.WaitAsync();
                if (!this.Contains(ID))
                    return false;

                Observable.Remove(ID);
                return true;
            }
            finally
            {
                LinkLock.Release();
            }
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public async Task ClearAsync()
        {
            try
            {
                await LinkLock.WaitAsync(); 
                Observable.Clear();
            }
            finally
            {
                LinkLock.Release();
            }
        }
    }
}
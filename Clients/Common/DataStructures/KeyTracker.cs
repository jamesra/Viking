using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.Common
{

    /// <summary>
    /// A thread-safe class that maintains a list of IDs
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class KeyTracker<T> where T : IComparable<T>
    {
        private readonly System.Threading.ReaderWriterLockSlim rwKnownLocationsLock = new System.Threading.ReaderWriterLockSlim();

        private readonly SortedSet<T> TrackedKeys = new SortedSet<T>();

        public IEnumerable<T> ValuesCopy()
        {
            try
            { 
                rwKnownLocationsLock.EnterReadLock();
                if (TrackedKeys.Count == 0)
                    return Array.Empty<T>();

                T[] copy = new T[TrackedKeys.Count];
                TrackedKeys.CopyTo(copy);
                return copy;
            }
            finally
            {
                rwKnownLocationsLock.ExitReadLock();
            }
        }

        public bool Contains(T ID)
        {
            try
            {
                rwKnownLocationsLock.EnterReadLock();
                return TrackedKeys.Contains(ID);
            }
            finally
            {
                rwKnownLocationsLock.ExitReadLock();
            }
        }

        public int Count
        {
            get
            {
                try
                {
                    rwKnownLocationsLock.EnterReadLock();
                    return TrackedKeys.Count;
                }
                finally
                {
                    rwKnownLocationsLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Add the ID.  If it is added execute the action.
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public bool TryAdd(T ID, Action a = null)
        {
            try
            {
                rwKnownLocationsLock.EnterUpgradeableReadLock();
                if (TrackedKeys.Contains(ID))
                    return false;

                try
                {
                    rwKnownLocationsLock.EnterWriteLock();
                    if (TrackedKeys.Contains(ID))
                        return false;

                    TrackedKeys.Add(ID);
                    try
                    {
                        a?.Invoke();
                    }
                    catch
                    {
                        TrackedKeys.Remove(ID);
                        throw;
                    }
                }
                finally
                {
                    rwKnownLocationsLock.ExitWriteLock();
                }

                return TrackedKeys.Contains(ID);
            }
            finally
            {
                rwKnownLocationsLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Add the ID if it is not in the set and the function returns true
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public bool TryAdd(T ID, Func<bool> CanAdd)
        {
            try
            {
                rwKnownLocationsLock.EnterUpgradeableReadLock();
                if (TrackedKeys.Contains(ID))
                    return false;

                try
                {
                    rwKnownLocationsLock.EnterWriteLock();
                    if (TrackedKeys.Contains(ID))
                        return false;

                    if (CanAdd())
                    {
                        TrackedKeys.Add(ID);
                    }
                }
                finally
                {
                    rwKnownLocationsLock.ExitWriteLock();
                }
                return TrackedKeys.Contains(ID);
            }
            finally
            {
                rwKnownLocationsLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Try to remove the ID.  If the ID is removed execute the action
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public bool TryRemove(T ID, Action a = null)
        {
            try
            {
                rwKnownLocationsLock.EnterUpgradeableReadLock();
                if (!TrackedKeys.Contains(ID))
                    return false;

                try
                {
                    rwKnownLocationsLock.EnterWriteLock();

                    bool removed = TrackedKeys.Remove(ID);
                    if(removed)
                        a?.Invoke();

                    return removed;
                }
                finally
                {
                    rwKnownLocationsLock.ExitWriteLock();
                }
                return TrackedKeys.Contains(ID);
            }
            finally
            {
                rwKnownLocationsLock.ExitUpgradeableReadLock();
            }
        }
    }


    /// <summary>
    /// A thread-safe class that maintains a count of references for a list of IDs
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RefCountingKeyTracker<T> where T : IComparable<T>
    {
        private readonly System.Threading.ReaderWriterLockSlim rwKnownLocationsLock = new System.Threading.ReaderWriterLockSlim();

        private readonly SortedDictionary<T, int> TrackedKeys = new SortedDictionary<T, int>();
        public bool Contains(T ID)
        {
            try
            {
                rwKnownLocationsLock.EnterReadLock();
                return TrackedKeys.ContainsKey(ID);
            }
            finally
            {
                rwKnownLocationsLock.ExitReadLock();
            }
        }

        public int Count
        {
            get
            {
                try
                {
                    rwKnownLocationsLock.EnterReadLock();
                    return TrackedKeys.Count;
                }
                finally
                {
                    rwKnownLocationsLock.ExitReadLock();
                }
            }
        }

        protected int RefCount(T ID)
        {
            try
            {
                rwKnownLocationsLock.EnterReadLock();
                return TrackedKeys.TryGetValue(ID, out int RefCount) ? RefCount : 0;
            }
            finally
            {
                rwKnownLocationsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Add the ID.  If it is added execute the action.
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="a">Action to execute if this is the first reference taken to the key</param>
        /// <returns></returns>
        public void AddRef(T ID, Action<T> OnFirstReferenceAction = null)
        {
            try
            {
                rwKnownLocationsLock.EnterWriteLock();

                int RefCount;
                if (TrackedKeys.TryGetValue(ID, out var key))
                {
                    RefCount = key;
                }
                else
                {
                    RefCount = 0;
                    TrackedKeys.Add(ID, 0);
                }

                if (RefCount == 0)
                {
                    try
                    {
                        OnFirstReferenceAction?.Invoke(ID);
                    }
                    catch
                    {
                        TrackedKeys.Remove(ID);
                        throw;
                    }
                }


                RefCount++;

                TrackedKeys[ID] = RefCount;
            }
            finally
            {
                rwKnownLocationsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Try to remove the ID.  If the ID is removed execute the action
        /// </summary>
        /// <param name="ID">Key to release</param>
        /// <param name="a">Action to take if this removes the last refe</param>
        /// <returns>True if the key was present</returns>
        public bool ReleaseRef(T ID, Action<T> OnLastReferenceReleasedAction = null)
        {
            try
            {
                rwKnownLocationsLock.EnterWriteLock();

                if(!TrackedKeys.TryGetValue(ID, out int RefCount))
                    return false;

                RefCount -= 1;

                if (RefCount == 0)
                {
                    OnLastReferenceReleasedAction?.Invoke(ID);
                    TrackedKeys.Remove(ID);
                }
                else
                {
                    TrackedKeys[ID] = RefCount;
                }

                return true;
            }
            finally
            {
                rwKnownLocationsLock.ExitWriteLock();
            }
        }
    }
}

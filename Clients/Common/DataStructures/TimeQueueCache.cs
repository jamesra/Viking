/******************************************************************************
 * Viking is Open Source under a Creative Commons License:
 * Attribution-NonCommercial-ShareAlike
 * http://creativecommons.org/licenses/by-nc-sa/3.0/legalcode
 * 
 * The reference to use for attribution is 
 * Anderson JR, et al 2010
 * The Viking viewer for connectomics: scalable multi-user annotation and
 * summarization of large volume data sets.
 * J Microscopy: [doi: 10.1111/j.1365-2818.2010.03402.x]
 * 
 * Summary: 
 * 1. You can use or change Viking any way you like 
 * 2. ... for non-commercial purposes.
 * 3. ... as long as you attibute the original development 
 * 4. ... you share your developments with us
 * 5. ... you distribute any derivative works under the same license provisions
 *  
 *****************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Viking.Common
{
    /// <summary>
    /// A cache which prioritizes entries based upon last access time. 
    /// Users can call checkpoint which will call OnFailedCheckpoint for all entries not accessed since the last checkpoint
    /// </summary>
    /// <typeparam name="KEY">Key used to retrieve entries</typeparam>
    /// <typeparam name="CACHEENTRY">Data type of stores entries, derived from CacheEntry template</typeparam>
    /// <typeparam name="ADDTYPE">Type which is added to cache</typeparam>
    /// <typeparam name="FETCHTYPE">Type returned from cache</typeparam>
    abstract public class TimeQueueCache<KEY, CACHEENTRY, ADDTYPE, FETCHTYPE>
        where CACHEENTRY : CacheEntry<KEY>
        where FETCHTYPE : class
    {
        //        static public long MaxCacheSize = 17179869184;// = 2 << 34;
        public Int64 MaxCacheSize = 2 << 23; //128 MB

        protected Int64 TotalCacheSize = 0;

        protected ConcurrentDictionary<KEY, CACHEENTRY> dictEntries = new ConcurrentDictionary<KEY, CACHEENTRY>();

        abstract protected FETCHTYPE Fetch(CACHEENTRY key);

        /// <summary>
        /// The derived object should create a cache entry for the key/value pair.
        /// May return null if the entry cannot be created or if they derived object will update the cahce itself later,,
        /// perhaps if waiting for an asynch operation to complete
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        abstract protected CACHEENTRY CreateEntry(KEY key, ADDTYPE value);

        abstract protected Task<CACHEENTRY> CreateEntryAsync(KEY key, ADDTYPE value);

        /// <summary>
        /// Retrieve an entry from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public virtual FETCHTYPE Fetch(KEY key)
        {
            bool success = dictEntries.TryGetValue(key, out CACHEENTRY entry);
            if (success == false)
                return default;

            //Record the fact that someone asked for this tile
            entry.WasUsedSinceLastCheckpoint = true;
            entry.LastAccessed = DateTime.UtcNow;

            FETCHTYPE value = Fetch(entry);
            /*
            if(value != null)
            {
                //Update the cache entry
                dictEntries[key] = entry; 
            }
             */

            return value;
        }

        /// <summary>
        /// Returns true if the cache contains the key at the time of calling. 
        /// The cache is concurrent so the result may change with later calls
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(KEY key)
        {
            return dictEntries.ContainsKey(key);
        }

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        public virtual bool Add(KEY key, ADDTYPE value)
        {
            CACHEENTRY entry = CreateEntry(key, value);
            if (entry == null)
                return false;

            return AddEntry(entry);
        }

        /// <summary>
        /// Creates a file for the texture passed.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="textureStream"></param>
        public virtual async Task<bool> AddAsync(KEY key, ADDTYPE value)
        {
            var entry = await CreateEntryAsync(key, value);
            if (entry == null)
                return false;

            return AddEntry(entry);
        }

        /// <summary>
        /// Return the cache entry for the key or create a new entry for the key if it does not exist
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual FETCHTYPE GetOrAdd(KEY key, ADDTYPE value)
        { 
            //Check before we create an entry...
            bool found = dictEntries.TryGetValue(key, out CACHEENTRY dictEntry);
            if (found)
            {
                return Fetch(dictEntry);
            }

            //OK, try to create an entry and add it
            CACHEENTRY entry = CreateEntry(key, value);
            dictEntry = dictEntries.GetOrAdd(key, entry);

            if (object.ReferenceEquals(dictEntry, entry))
            {
                ChangeCacheSize(entry.Size);
            }

            return Fetch(dictEntry);
        }

        /// <summary>
        /// Remove the entry from the cache
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(KEY key)
        {
            bool removed = dictEntries.TryRemove(key, out CACHEENTRY value);
            if (removed)
            {
                long size = value.Size;
                ChangeCacheSize(-size);

                return OnRemoveEntry(value);
            }

            return removed;
        }

        /// <summary>
        /// This can be called by derived classes if they want to use asynch operations and wan't to delay adding an entry to 
        /// the cache after thier CreateEntry method is called.  I
        /// </summary>
        /// <param name="entry"></param>
        protected bool AddEntry(CACHEENTRY entry)
        {
            bool success = dictEntries.TryAdd(entry.Key, entry);

            if (success)
            {
                ChangeCacheSize(entry.Size);
            }

            return success;
        }



        private void ChangeCacheSize(Int64 amount)
        {
            Int64 cacheSize;
            Int64 readcacheSize;

            do
            {
                cacheSize = this.TotalCacheSize;
                readcacheSize = System.Threading.Interlocked.CompareExchange(ref this.TotalCacheSize, cacheSize + amount, cacheSize);
            }
            while (readcacheSize != cacheSize);


        }

        /// <summary>
        /// This should be called periodically to reduce the disk footprint
        /// </summary>
        public void ReduceCacheFootprint(object state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (TotalCacheSize <= MaxCacheSize)
                return;

            int RemoveCount = 0;
            int LostCount = 0;
            long FreedCount = 0;
            if (dictEntries.IsEmpty)
                return;

            List<CACHEENTRY> listEntries = dictEntries.Values.ToList<CACHEENTRY>();
            listEntries.Sort();

            while (TotalCacheSize > MaxCacheSize)
            {
                if (listEntries.Count == 0)
                    return;

                CACHEENTRY entry = listEntries[0];
                //Debug.Assert(dictEntries.ContainsKey(entry.Key));
                if (dictEntries.ContainsKey(entry.Key) == false)
                {
                    LostCount++;
                }
                else
                {
                    FreedCount += entry.Size;
                    RemoveEntry(entry);
                    RemoveCount++;
                }

                listEntries.RemoveAt(0);
            }

            if (RemoveCount > 0 || LostCount > 0)
            {
                Trace.WriteLine(this.GetType().ToString() + " reduce cache size", "Cache");
                Trace.WriteLine("\tLost    " + LostCount.ToString() + " entries");
                Trace.WriteLine("\tRemoved " + RemoveCount.ToString() + " entries");
                Trace.WriteLine("\tFreed   " + FreedCount.ToString() + " bytes");
            }
        }

        /// <summary>
        /// Perform a checkpoint.  Call OnCheckpointFailed on each entry that does not pass
        /// </summary>
        public void Checkpoint()
        {
            CACHEENTRY[] EntryListCopy = dictEntries.Values.ToArray<CACHEENTRY>();

            int FailCount = 0;

            //Walk the list and remove all entries who have not been accessed since the last checkpoint

            for (int iEntry = 0; iEntry < EntryListCopy.Length; iEntry++)
            {
                CACHEENTRY entry = EntryListCopy[iEntry];
                if (entry == null)
                    continue;

                if (entry == null)
                    continue;

                if (entry.WasUsedSinceLastCheckpoint)
                {
                    entry.WasUsedSinceLastCheckpoint = false;
                }
                else if (false == entry.CheckpointExempt)
                {
                    //Give derived classes a chance to handle this how they see fit
                    OnCheckpointFailed(entry);
                    FailCount++;
                }

            }

            if (FailCount > 0)
                Trace.WriteLine(this.GetType().ToString() + " " + FailCount.ToString() + " entries failed checkpoint", "Cache");

            EntryListCopy = null;
        }


        /// <summary>
        /// Remove an entry from the cache, does not lock
        /// </summary>
        /// <param name="entry"></param>
        protected void RemoveEntry(CACHEENTRY entry)
        {
            long size = 0;
            bool CanRemoveEntry = true;
            if (entry != null)
            {
                size = entry.Size;
                CanRemoveEntry = OnRemoveEntry(entry);
            }

            if (CanRemoveEntry)
            {
                bool success = dictEntries.TryRemove(entry.Key, out entry);
                if (success)
                {
                    entry.Dispose();

                    ChangeCacheSize(-size);
                    //TotalCacheSize -= size;
                }
            }
        }


        /// <summary>
        /// Called by RemoveEntry when the cache wants to remove an entry.
        /// Derived classes can override to cleanup
        /// Returns true if the entry was successfully cleaned up, otherwise false
        /// </summary>
        /// <param name="entry"></param>
        protected virtual bool OnRemoveEntry(CACHEENTRY entry)
        {
            return true;
        }

        /// <summary>
        /// Default implementation removes an entry which has not been used since the last checkpoint
        /// </summary>
        /// <param name="entry"></param>
        protected virtual void OnCheckpointFailed(CACHEENTRY entry)
        {
            RemoveEntry(entry);
        }
    }
}

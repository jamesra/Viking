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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 

namespace Viking.Common
{
    public abstract class CacheEntry<KEY> : IComparer<CacheEntry<KEY>>, IComparable, IEquatable<CacheEntry<KEY>>, IDisposable 
    {
        /// <summary>
        /// Name of the file
        /// </summary>
        public readonly KEY Key;

        /// <summary>
        /// Last time file was accessed
        /// </summary>
        public DateTime LastAccessed;

        /// <summary>
        /// One way to clean the cache is to remove all entries that have not been used since a checkpoint.
        /// This flag can be set to indicate the entry was recently used.  When a checkpoint occurs we remove
        /// all entries that were not used and reset this flag to false for all entries that were used.
        /// </summary>
        public bool WasUsedSinceLastCheckpoint = true;

        /// <summary>
        /// Sometimes we want an entry to stay in the cache even if it is failing checkpoints.  Setting this 
        /// exempts the entry from failing checkpoints
        /// </summary>
        public bool CheckpointExempt = false; 

        /// <summary>
        /// Size of the file
        /// </summary>
        public Int64 Size;

        public CacheEntry(KEY key)
        {
            this.Key = key;
            this.Size = 1;
            this.LastAccessed = DateTime.UtcNow;
        }

        public CacheEntry(KEY key, DateTime lastAccessed, long size)
        {
            this.Key = key;
            LastAccessed = lastAccessed;
            Size = size; 
        }

        int? _FirstHashCode; 
        public override int GetHashCode()
        {
            if (_FirstHashCode.HasValue == false)
            {
                _FirstHashCode = Key.GetHashCode();
                return _FirstHashCode.Value;
            }
            else
            {
                Debug.Assert(Key.GetHashCode() == _FirstHashCode.Value); 
                return Key.GetHashCode();
            }
        }

        public override bool Equals(object obj)
        {
            CacheEntry<KEY> entry = obj as CacheEntry<KEY>;
            if (entry == null)
                return false;

            return entry.Key.Equals(this.Key);
        }

        #region IComparer<CacheEntry<KEY>> Members

        int IComparer<CacheEntry<KEY>>.Compare(CacheEntry<KEY> x, CacheEntry<KEY> y)
        {
            return x.LastAccessed.CompareTo(y.LastAccessed);
        }

        #endregion

        #region IComparable Members

        int IComparable.CompareTo(object obj)
        {
            CacheEntry<KEY> entry = obj as CacheEntry<KEY>;
            if (entry != null)
            {
                return LastAccessed.CompareTo(entry.LastAccessed);
            }

            return 0; 
        }

        #endregion

        #region IEquatable<CacheEntry<KEY>> Members

        bool IEquatable<CacheEntry<KEY>>.Equals(CacheEntry<KEY> other)
        {
            if (other == null)
                return false; 

            return this.Key.Equals(other.Key);
        }

        #endregion

        public abstract void Dispose();

    }
}

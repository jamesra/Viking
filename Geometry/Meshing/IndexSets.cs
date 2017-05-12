using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public interface IIndexSet : IReadOnlyList<long>
    {
        IIndexSet IncrementStartingIndex(long adjustment);
    }

    public class ContinuousIndexSetEnumerator : IEnumerator<long>
    {
        long position = -1;

        private long StartIndex;
        private long Count;

        public ContinuousIndexSetEnumerator(long startIndex, long count)
        {
            this.StartIndex = startIndex;
            this.Count = count;
        }

        public bool MoveNext()
        {
            if (position < 0)
                position = StartIndex;
            else
                position++;

            return position < StartIndex + Count;
        }

        public void Reset()
        {
            position = -1;
        }

        public void Dispose()
        {
            return;
        }

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public long Current
        {
            get
            {
                if (position < 0)
                    throw new InvalidOperationException("MoveNext has not been called on enumerator");

                if (position >= StartIndex + Count)
                    throw new IndexOutOfRangeException("No more elements in enumerator");

                return position;
            }
        }
    }


    public class ContinuousIndexSet : IIndexSet
    {
        private long StartIndex;
        private long _Count;

        public int Count
        {
            get
            {
                return System.Convert.ToInt32(_Count);
            }
        }

        public long this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }

                return StartIndex + index;
            }
        }

        public ContinuousIndexSet(long startIndex, long count)
        {
            this.StartIndex = startIndex;
            this._Count = count;
        }

        public IEnumerator<long> GetEnumerator()
        {
            return new ContinuousIndexSetEnumerator(StartIndex, Count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ContinuousIndexSetEnumerator(StartIndex, Count);
        }

        public IIndexSet IncrementStartingIndex(long adjustment)
        {
            return new ContinuousIndexSet(StartIndex + adjustment, _Count);
        }
    }

    public class IndexSetEnumerator : IEnumerator<long>
    {
        long[] Indicies = null;
        long index = -1;
        public IndexSetEnumerator(long[] indicies)
        {
            Indicies = indicies;
        }

        public long Current
        {
            get
            {
                if (index < 0)
                    throw new InvalidOperationException("MoveNext has not been called on enumerator");

                if (index >= Indicies.LongLength)
                    throw new IndexOutOfRangeException("No more elements in enumerator");

                return Indicies[index];
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return this.Current;
            }
        }

        public void Dispose()
        {
            return;
        }

        public bool MoveNext()
        {
            index++;
            return index < Indicies.LongLength;
        }

        public void Reset()
        {
            index = -1;
        }
    }

    public class IndexSet : IIndexSet
    {
        private long[] Indicies;

        public IndexSet(long[] indicies)
        {
            Indicies = new long[indicies.LongLength];
            Array.Copy(indicies, Indicies, indicies.Length);
        }

        public long this[int index]
        {
            get
            {
                return Indicies[index];
            }
        }

        public int Count
        {
            get
            {
                return Indicies.Length;
            }
        }

        public IEnumerator<long> GetEnumerator()
        {
            return new IndexSetEnumerator(this.Indicies);
        }

        public IIndexSet IncrementStartingIndex(long adjustment)
        {
            return new IndexSet(Indicies.Select(i => i + adjustment).ToArray());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Indicies.GetEnumerator();
        }
    }



    public class ContinuousWrappedIndexSetEnumerator : IEnumerator<long>
    {
        long position = -1;

        private long MinIndex;
        private long MaxIndex;
        private long StartIndex;
        private long _Count;

        public ContinuousWrappedIndexSetEnumerator(long minIndex, long maxIndex, long startIndex)
        {
            if (maxIndex < minIndex)
                throw new ArgumentException("Max index must be greater or equal to min index");

            if (startIndex < minIndex || startIndex >= maxIndex)
                throw new ArgumentException("Start index must fall between min and max index");

            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.StartIndex = startIndex;
            this._Count = this.MaxIndex - this.MinIndex;
        }

        public bool MoveNext()
        {
            if (position < 0)
                position = 0;
            else
                position++;

            return position < _Count;
        }

        public void Reset()
        {
            position = -1;
        }

        public void Dispose()
        {
            return;
        }

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public long Current
        {
            get
            {
                if (position < 0)
                    throw new InvalidOperationException("MoveNext has not been called on enumerator");

                if (position >= _Count)
                    throw new IndexOutOfRangeException("No more elements in enumerator");

                long value = position + StartIndex;
                if (value >= MaxIndex)
                    value -= _Count;

                return value;
            }
        }
    }

    /// <summary>
    /// An index set where the first element may be non-zero, and at some point the enumerator loops around back to zero
    /// So if we have [1,2,3,4,5] and the start index is 3 this set will index as [4,5,1,2,3]
    /// </summary>
    public class ContinuousWrappedIndexSet : IIndexSet
    {
        private long MinIndex;
        private long MaxIndex;
        private long StartIndex;
        private long _Count;

        public int Count
        {
            get
            {
                return System.Convert.ToInt32(_Count);
            }
        }

        public long this[int index]
        {
            get
            {
                if (index < 0 || index >= _Count)
                {
                    throw new IndexOutOfRangeException();
                }

                long value = StartIndex + index;

                if (value >= MaxIndex)
                {
                    value -= _Count;
                }

                return value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="wrapIndex">The value we never reach, we wrap before</param>
        /// <param name="count">Total number of values in the sequence</param>
        public ContinuousWrappedIndexSet(long minIndex, long maxIndex, long startIndex)
        {
            if (maxIndex < minIndex)
                throw new ArgumentException("Max index must be greater or equal to min index");

            if (startIndex < minIndex || startIndex >= maxIndex)
                throw new ArgumentException("Start index must fall between min and max index");

            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.StartIndex = startIndex;
            this._Count = this.MaxIndex - this.MinIndex;
        }
         
        public IEnumerator<long> GetEnumerator()
        {
            return new ContinuousWrappedIndexSetEnumerator(this.MinIndex, this.MaxIndex, this.StartIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ContinuousWrappedIndexSetEnumerator(this.MinIndex, this.MaxIndex, this.StartIndex);
        }

        public IIndexSet IncrementStartingIndex(long adjustment)
        {
            ContinuousWrappedIndexSet indexSet = new Meshing.ContinuousWrappedIndexSet(MinIndex + adjustment, MaxIndex + adjustment, StartIndex + adjustment);
            return indexSet;
            //MinIndex += adjustment;
            //MaxIndex += adjustment;
            //StartIndex += adjustment; 
        }
    }
}

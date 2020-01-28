using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public interface IIndexSet : IReadOnlyList<long>
    {
        IIndexSet IncrementStartingIndex(long adjustment);

        IIndexSet Reverse();

        long this[long index] { get; }
    }

    /// <summary>
    /// Returns a list of integers, starting at the provided index, incrementing by 1 until count indicies have been returned.
    /// </summary>
    public class ContinuousIndexSetEnumerator : IEnumerator<long>
    {
        long position = -1;

        private long StartIndex;
        private long Count;
        private bool _Reversed;

        public ContinuousIndexSetEnumerator(long startIndex, long count, bool reverse = false)
        {
            this.StartIndex = startIndex;
            this.Count = count;
            _Reversed = reverse;
        }

        public bool MoveNext()
        {
            if (false == _Reversed)
            {
                if (position < 0)
                    position = StartIndex;
                else
                    position++;

                return position < StartIndex + Count;
            }
            else
            {
                if (position < 0)
                    position = Count - 1;
                else
                    position--;

                return position >= StartIndex; 
            }
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


    /// <summary>
    /// An indexable collection of integers, starting at the provided value, incrementing by 1 for each index value.
    /// </summary>
    public class ContinuousIndexSet : IIndexSet
    {
        private long StartIndex;
        private long _Count;
        private bool _Reversed; 

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
                return this[(long)index];
            }
        }


        public long this[long index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }

                if (_Reversed == false)
                    return StartIndex + index;
                else
                    return ((_Count-1) + StartIndex) - index; 
            }
        }

        public ContinuousIndexSet(long startIndex, long count, bool reverse=false)
        {
            this.StartIndex = startIndex;
            this._Count = count;
            this._Reversed = reverse;
        }

        public IIndexSet Reverse()
        {
            ContinuousIndexSet reversed = new ContinuousIndexSet(StartIndex, _Count, !_Reversed);
            return reversed;
        }

        public IEnumerator<long> GetEnumerator()
        {
            return new ContinuousIndexSetEnumerator(StartIndex, Count, _Reversed);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ContinuousIndexSetEnumerator(StartIndex, Count, _Reversed);
        }

        public IIndexSet IncrementStartingIndex(long adjustment)
        {
            return new ContinuousIndexSet(StartIndex + adjustment, _Count, _Reversed);
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

    /// <summary>
    /// Represents a set of indicies with some helper functions added to translate the values.
    /// </summary>
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
                return this[(long)index];
            }
        }

        public long this[long index]
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

        public IIndexSet Reverse()
        {
            long[] reversedIndicies = Indicies.Clone() as long[];
            Array.Reverse(reversedIndicies);
            return new IndexSet(reversedIndicies);
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


    /// <summary>
    /// Represents a set of index values that will loop back to the begining once the last value has been enumerated
    /// </summary>
    public class ContinuousWrappedIndexSetEnumerator : IEnumerator<long>
    {
        long position = -1;

        private long MinIndex;
        private long MaxIndex;
        private long StartIndex;
        private long _Count;
        private bool _Reverse;

        public ContinuousWrappedIndexSetEnumerator(long minIndex, long maxIndex, long startIndex, bool reverse)
        {
            if (maxIndex < minIndex)
                throw new ArgumentException("Max index must be greater or equal to min index");

            if (startIndex < minIndex || startIndex >= maxIndex)
                throw new ArgumentException("Start index must fall between min and max index");

            this._Reverse = reverse;
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

                long value;
                if (false == _Reverse)
                {
                    value = position + StartIndex;
                    if (value >= MaxIndex)
                        value -= _Count;
                }
                else
                {
                    value = position - StartIndex;
                    if (value < 0)
                        value += _Count;
                }

                return value;
            }
        }
    }

    /// <summary>
    /// An index set where the first element may be non-zero, and at some point the enumerator loops around back to zero
    /// So if we have [1,2,3,4,5] and the start index is 3 this set will index as [4,5,1,2,3].
    /// This set has not limit to the index value, negative or positive.  The sequence loops until the bit-depth limit of the system.
    /// </summary>
    public class InfiniteWrappedIndexSet : IIndexSet
    {
        protected long MinIndex;
        protected long MaxIndex;
        protected long StartIndex;
        protected long _Count;

        protected bool _Reverse = false; 

        public virtual IIndexSet Reverse()
        {
            return new InfiniteWrappedIndexSet(MinIndex, MaxIndex, StartIndex, !this._Reverse);
        }

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
                return this[(long)index];
            }
        }

        public virtual long this[long index]
        {
            get
            {
                if (index < 0 || index >= _Count)
                {
                    index = index % _Count; //Force the index into range
                }

                long value;
                if (false == this._Reverse)
                {
                    value = StartIndex + index;

                    if (value >= MaxIndex)
                    {
                        value -= _Count;
                    }
                }
                else
                {
                    value = StartIndex - index;

                    if (value < 0)
                    {
                        value += _Count;
                    }
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
        public InfiniteWrappedIndexSet(long minIndex, long maxIndex, long startIndex, bool reverse=false)
        {
            if (maxIndex < minIndex)
                throw new ArgumentException("Max index must be greater or equal to min index");

            if (startIndex < minIndex || startIndex >= maxIndex)
                throw new ArgumentException("Start index must fall between min and max index");

            this._Reverse = reverse;
            this.MinIndex = minIndex;
            this.MaxIndex = maxIndex;
            this.StartIndex = startIndex;
            this._Count = this.MaxIndex - this.MinIndex;
        }

        public IEnumerator<long> GetEnumerator()
        {
            return new ContinuousWrappedIndexSetEnumerator(this.MinIndex, this.MaxIndex, this.StartIndex, this._Reverse);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ContinuousWrappedIndexSetEnumerator(this.MinIndex, this.MaxIndex, this.StartIndex, this._Reverse);
        }

        /// <summary>
        /// Returns a new IIndexSet that increments all indicies by a fixed amount.
        /// </summary>
        /// <param name="adjustment"></param>
        /// <returns></returns>
        public IIndexSet IncrementStartingIndex(long adjustment)
        {
            FiniteWrappedIndexSet indexSet = new FiniteWrappedIndexSet(MinIndex + adjustment, MaxIndex + adjustment, StartIndex + adjustment, this._Reverse);
            return indexSet;
            //MinIndex += adjustment;
            //MaxIndex += adjustment;
            //StartIndex += adjustment; 
        }
    }

    /// <summary>
    /// An index set where the first element may be non-zero, and at some point the enumerator loops around back to zero
    /// So if we have [1,2,3,4,5] and the start index is 3 this set will index as [4,5,1,2,3]
    /// 
    /// FiniteWrappedIndexSet will throw an exception if it is indexed out of the normal array bounds, ex: this[-1] or this[_Count]
    /// 
    /// </summary>
    public class FiniteWrappedIndexSet : InfiniteWrappedIndexSet
    {

        public override IIndexSet Reverse()
        {
            return new FiniteWrappedIndexSet(MinIndex, MaxIndex, StartIndex, !this._Reverse);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="wrapIndex">The value we never reach, we wrap before</param>
        /// <param name="count">Total number of values in the sequence</param>
        public FiniteWrappedIndexSet(long minIndex, long maxIndex, long startIndex, bool reverse=false) : base(minIndex, maxIndex, startIndex, reverse)
        {
        }

        public override long this[long index]
        {
            get
            {
                if (index < 0 || index >= _Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                long value;
                if (false == _Reverse)
                {
                    value = StartIndex + index;

                    if (value >= MaxIndex)
                    {
                        value -= _Count;
                    }
                }
                else
                {
                    value = StartIndex - index;

                    if (value < 0)
                    {
                        value += _Count;
                    }
                }

                return value;
            }
        }
    }
}

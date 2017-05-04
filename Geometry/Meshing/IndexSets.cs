using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{

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


    public class ContinuousIndexSet : IReadOnlyList<long>
    {
        private long StartIndex;
        private long Count;

        int IReadOnlyCollection<long>.Count
        {
            get
            {
                return System.Convert.ToInt32(Count);
            }
        }

        public long this[int index]
        {
            get
            {
                if(index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }

                return StartIndex + index; 
            }
        }

        public ContinuousIndexSet(long startIndex, long count)
        {
            this.StartIndex = startIndex;
            this.Count = count;
        }

        public IEnumerator<long> GetEnumerator()
        {
            return new ContinuousIndexSetEnumerator(StartIndex, Count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new ContinuousIndexSetEnumerator(StartIndex, Count);
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

    public class IndexSet : IReadOnlyList<long>
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Indicies.GetEnumerator();
        } 
    }
}

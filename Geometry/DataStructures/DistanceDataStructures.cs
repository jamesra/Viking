using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace Geometry
{

    public readonly struct DistanceToPoint<T> : IComparable<DistanceToPoint<T>>, IEquatable<DistanceToPoint<T>>
    {
        public readonly GridVector2 Point;
        public readonly double Distance;
        public readonly T Value;

        public DistanceToPoint(GridVector2 point, double distance, T value)
        {
            Point = point;
            Distance = distance;
            Value = value;
        }

        public int CompareTo(DistanceToPoint<T> other)
        {
            return this.Distance.CompareTo(other.Distance);
        }

        public override int GetHashCode()
        {
            return Point.GetHashCode() ^ Distance.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is DistanceToPoint<T> other)
                return Equals(other);

            return false;
        }

        public bool Equals(DistanceToPoint<T> other)
        {
            return this.Distance.Equals(other.Distance) && this.Point == other.Point;
        }

        public override string ToString()
        {
            return $"{Point} {Distance}";
        }

        public static bool operator ==(DistanceToPoint<T> left, DistanceToPoint<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DistanceToPoint<T> left, DistanceToPoint<T> right)
        {
            return !(left == right);
        }

        public static bool operator <(DistanceToPoint<T> left, DistanceToPoint<T> right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(DistanceToPoint<T> left, DistanceToPoint<T> right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(DistanceToPoint<T> left, DistanceToPoint<T> right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(DistanceToPoint<T> left, DistanceToPoint<T> right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// A sorted list of distances that allows multiple entries at the same distance.  
    /// Indexing is per entry, not for a distance value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DistanceList<T>
    {
        public SortedList<double, List<DistanceToPoint<T>>> Data = null;

        /// <summary>
        /// 
        /// </summary>
        public double MaxDistance = double.MinValue;
        public int Count;

        public DistanceList(int capacity)
        {
            Data = new SortedList<double, List<Geometry.DistanceToPoint<T>>>(capacity);
        }

        public void Add(GridVector2 point, double distance, T value)
        {
            DistanceToPoint<T> item = new DistanceToPoint<T>(point, distance, value);
            Add(item);
        }

        public virtual void Add(DistanceToPoint<T> item)
        {
            Count++; //Increment our count

            if (Data.ContainsKey(item.Distance))
            {
                Debug.Assert(Data[item.Distance].Any(entry => entry.Equals(item)) == false, "Item should not already exist in the distance list");
                Data[item.Distance].Add(item);
                return;
            }

            if (item.Distance > this.MaxDistance)
            {
                this.MaxDistance = item.Distance;
            }

            List<DistanceToPoint<T>> newList = new List<DistanceToPoint<T>>(2)
            {
                item
            };

            Data.Add(item.Distance, newList);
            return;
        }

        public List<Geometry.DistanceToPoint<T>> this[double distance]
        {
            get
            {
                return Data[distance];
            }
        }

        /*
        public void RemoveAt(int index)
        {

            for(int i = 0; i < Data.Count; i++)
            {
                Data[i].Count 
            }
            
            Count = Count - 1;

            Data.RemoveAt(index);

            if(Data.Count == 0)
            {
                this.MaxDistance = double.MaxValue;
            }
            else if (index == Data.Count)
            {
                this.MaxDistance = Data.Last().Key;
            }
        }
        */

    }

    public class FixedSizeDistanceList<T> : DistanceList<T>
    {
        /// <summary>
        /// The list should contain at most MaxCapacity entries, though equidistant points may allow the number of entries to exceed this value.
        /// </summary>
        public readonly int MaxCapacity;
        public FixedSizeDistanceList(int capacity) : base(capacity)
        {
            MaxCapacity = capacity;
        }

        /// <summary>
        /// Adds an item to the list only if it is under the capacity limit OR if it is a closer distance than an existing item in the list
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryAdd(DistanceToPoint<T> item)
        {
            if (this.Count < this.MaxCapacity)
            {
                base.Add(item);
                return true;
            }

            //Check if we should replace an existing item
            if (item.Distance < this.MaxDistance)
            {
                base.Add(item);

                //Remove the largest item in the list
                RemoveOvercapacityItems();
                return true;
            }

            //Every item in the full list was closer than the new item, so it is not added and we return false; 
            return false;

        }

        /// <summary>
        /// Remove entries with the largest distance until we are under capacity
        /// List may still exceed capacity if largest distance entries have multiple equidistant items.
        /// </summary>
        private void RemoveOvercapacityItems()
        {
            while (this.Count > this.MaxCapacity)
            {
                var furthestEntry = this.Data[MaxDistance];

                //Check for multiple equidistant points.  If removing the entries will bring us under max capacity instead of equal to max capacity we need to hold on to the duplicates.
                if (this.Count - furthestEntry.Count < this.MaxCapacity)
                    break;

                //Remove the furthest entry.
                this.Count -= furthestEntry.Count;
                this.Data.RemoveAt(Data.Count - 1);

                MaxDistance = Data.Keys[Data.Keys.Count - 1];
                //MaxDistance = this.Data.Keys[Data.Count - 1];
            }

            return;
        }
    }


    public class DistanceToPointSorter<T> : IComparer<DistanceToPoint<T>>
    {
        public int Compare(DistanceToPoint<T> x, DistanceToPoint<T> y)
        {
            return x.Distance.CompareTo(y.Distance);
        }
    }
}

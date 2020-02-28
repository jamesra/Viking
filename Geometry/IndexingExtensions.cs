using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public static class IndexingExtensions
    {
        /// <summary>
        /// Return all possible pairing of two elements from the passed array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        public static IEnumerable<Combo<T>> CombinationPairs<T>(this IReadOnlyList<T> array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                for (int j = i + 1; j < array.Count; j++)
                {
                    yield return new Combo<T>(array[i], array[j], i, j);
                }
            }
        }
    }

    public struct Combo<T>
    {
        public readonly int iA;
        public readonly int iB;
        public readonly T A;
        public readonly T B;

        public Combo(T a, T b, int I, int J)
        {
            iA = I;
            iB = J;
            A = a;
            B = b;
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null))
                return false;

            Combo<T> other = (Combo<T>)obj;
            return other.iA == this.iA && other.iB == this.iB;
        }

        public override int GetHashCode()
        {
            return (iA * 23) + iB;
        }
    }
}

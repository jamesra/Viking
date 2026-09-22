using System.Collections.Generic;

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

        /// <summary>
        /// Returns all possible pairing of items from A with items from B
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static IEnumerable<Combo<T>> CombinationPairs<T>(this IReadOnlyList<T> A, IReadOnlyList<T> B)
        {
            for (int i = 0; i < A.Count; i++)
            {
                for (int j = 0; j < B.Count; j++)
                {
                    yield return new Combo<T>(A[i], B[j], i, j);
                }
            }
        }
    }

    public readonly struct Combo<T>(T a, T b, int I, int J)
    {
        public readonly int iA = I;
        public readonly int iB = J;
        public readonly T A = a;
        public readonly T B = b;

        public override bool Equals(object obj)
        {
            if (obj is Combo<T> other)
                return other.iA == this.iA && other.iB == this.iB;

            return false;
        }

        public override int GetHashCode() => (iA * 23) + iB;

        public static bool operator ==(Combo<T> left, Combo<T> right) => left.Equals(right);

        public static bool operator !=(Combo<T> left, Combo<T> right) => !(left == right);
    }
}

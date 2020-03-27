using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace Geometry
{
    public static class CollectionExtensions
    {
        public static void AddToSet(this Dictionary<int, SortedSet<int>> dict, int key, int val)
        {
            if (dict.ContainsKey(key) == false)
            {
                var newset = new SortedSet<int>();
                newset.Add(val);
                dict.Add(key, newset);
            }
            else
            {
                dict[key].Add(val);
            }
        }

        /// <summary>
        /// Creates a new array with the new values appended on to the end
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="existing"></param>
        /// <param name="newValues"></param>
        /// <returns></returns>
        public static T[] AddRange<T>(this T[] existing, T[] newValues)
        {
            /////////////////////////
            //Extend our vertex array
            T[] extendedArray = new T[existing.Length + newValues.Length];
            Array.Copy(existing, extendedArray, existing.Length);
            /////////////////////////

            Array.Copy(newValues, 0, extendedArray, existing.Length, newValues.Length);
            return extendedArray;
        }
    }
}

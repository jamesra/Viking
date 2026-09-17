using System;
using System.Collections.Generic;

namespace Geometry
{
    public static class CollectionExtensions
    {
        public static void AddToSet(this Dictionary<int, SortedSet<int>> dict, int key, int val)
        {
            if (dict.TryGetValue(key, out var set))
            {
                set.Add(val);
            }
            else
            {
                set =
                [
                    val
                ];
                dict.Add(key, set);
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

        /// <summary>Copy of the array with <paramref name="newValue"/> appended.</summary>
        public static T[] Add<T>(this T[] existing, T newValue)
        {
            /////////////////////////
            //Extend our vertex array
            T[] extendedArray = new T[existing.Length + 1];
            Array.Copy(existing, extendedArray, existing.Length);
            /////////////////////////
            extendedArray[existing.Length] = newValue;
            return extendedArray;
        }

        /// <summary>Copy of the array with the element at <paramref name="index"/> removed.</summary>
        public static T[] RemoveAt<T>(this T[] existing, int index)
        {
            /////////////////////////
            //Extend our vertex array
            T[] output = new T[existing.Length - 1];
            Array.Copy(existing, output, index);
            Array.Copy(existing, index + 1, output, index, output.Length - index);
            /////////////////////////
            return output;
        }

        /// <summary>Copy of the array with <paramref name="newValue"/> inserted at <paramref name="index"/>.</summary>
        public static T[] Insert<T>(this T[] existing, int index, T newValue)
        {
            /////////////////////////
            //Extend our vertex array
            T[] extendedArray = new T[existing.Length + 1];
            Array.Copy(existing, extendedArray, index);
            Array.Copy(existing, index, extendedArray, index + 1, existing.Length - index);

            extendedArray[index] = newValue;
            /////////////////////////

            return extendedArray;
        }

        /// <summary>
        /// Insert into a closed ring (first equals last). Inserting at 0 also updates the duplicate tail.
        /// </summary>
        public static T[] InsertIntoClosedRing<T>(this T[] existing, int index, T newValue)
        {
            System.Diagnostics.Debug.Assert(existing[0].Equals(existing[existing.Length - 1]), "Input must be a closed array with first == last");


            /*if(index == existing.Length-1) //Insert at the end of the loop, but do not change the 0 index
            {
                index = 0; //Adjust the index we insert into because we handle that case later
            }*/

            T[] output = existing.Insert(index, newValue);

            if (index == 0)
            {
                //Close the loop
                output[output.Length - 1] = newValue;
            }

            return output;
        }

        /// <summary>
        /// Remove from a closed ring (first equals last). Removing index 0 also updates the duplicate tail.
        /// </summary>
        public static T[] RemoveFromClosedRing<T>(this T[] existing, int index)
        {
            System.Diagnostics.Debug.Assert(existing[0].Equals(existing[existing.Length - 1]), "Input must be a closed array with first == last");

            if (index == existing.Length - 1)
            {
                index = 0; //Adjust the index we insert into because we handle that case later
            }

            T[] output = existing.RemoveAt(index);

            if (index == 0)
            {
                //Close the loop
                output[output.Length - 1] = output[0];
            }

            return output;
        }
    }
}

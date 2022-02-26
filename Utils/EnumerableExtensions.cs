using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Viking
{
    public static class EnumerableExtensions
    {
        public static string ToCsv<T>(this IEnumerable<T> collection)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (T item in collection)
            {
                if (!first)
                    sb.Append($", {item}");
                else
                {
                    first = false;
                    sb.Append(item.ToString());
                }
            }

            return sb.ToString();
        }
    }
}
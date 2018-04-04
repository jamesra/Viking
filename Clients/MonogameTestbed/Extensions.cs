using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonogameTestbed
{ 
    public static class ArrayExtensions
    {
        public static int? IndexOf(this IReadOnlyList<int> array, int value)
        {
            for(int i = 0; i < array.Count; i++)
            {
                if (array[i] == value)
                    return i; 
            }

            return new int?();
        }
    }
    

}

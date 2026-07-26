using System;

namespace Viking.DataModel.Annotation.Tests
{
    public static class Shared
    {
        private static readonly Random rd = new Random();
        
        /// <summary>
        /// Generate a random set of letters and digits
        /// </summary>
        /// <param name="stringLength"></param>
        /// <returns></returns>
        public static string RandomLetters(int stringLength)
        {
            const string allowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789";
            char[] chars = new char[stringLength];

            for (int i = 0; i < stringLength; i++)
            {
                chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];
            }

            return new string(chars);
        }
    }
}
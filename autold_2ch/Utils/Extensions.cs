using System;

namespace Olfactory2Ch.Utils
{
    internal static class StringExtensions
    {
        public static string ToPath(this string s, string replacement = "-")
        {
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(replacement, temp);
        }
        public static bool IsIP(this string s) => System.Net.IPAddress.TryParse(s, out _);
    }

    internal static class RandomExtensions
    {
        public static void Shuffle<T>(this Random rng, T[] array, int offset = 0, int length = -1)
        {
            length = length < 0 ? array.Length - offset : Math.Min(length, array.Length - offset);
            var n = length;

            while (n-- > 0)
            {
                int k = rng.Next(length);
                T temp = array[offset + n];
                array[offset + n] = array[offset + k];
                array[offset + k] = temp;
            }
        }
    }
}

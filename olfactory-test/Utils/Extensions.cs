using System;

namespace Olfactory.Utils
{
    internal static class RandomExtensions
    {
        public static Random Shuffle<T>(this Random rng, T[] array)
        {
            void Shuffle()
            {
                int n = array.Length;
                while (n > 1)
                {
                    int k = rng.Next(n--);
                    T temp = array[n];
                    array[n] = array[k];
                    array[k] = temp;
                }
            }

            int repetitions = rng.Next(8) + 3;  // 3..10 repetitions
            for (int i = 0; i < repetitions; i++)
            {
                Shuffle();
            }

            return rng;
        }
    }

    internal static class StringExtensions
    {
        public static string ToPath(this string s, string replacement = "-")
        {
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(replacement, temp);
        }
    }
}

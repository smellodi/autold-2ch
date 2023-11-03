using System;

namespace AutOlD2Ch.Utils;

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

        // Running randomization procedure once is not enough to get the uniform permutations:
        // the chance for each element to stay in tis original location isn't linear,
        // as this chance increases with the increase of a location proximity to the edge of the range
        // , as somewhat "a*(1 + 0.15*x^2"
        // Running the procedure at least twice forces equal probability for each element of the array
        // to appear in each location
        for (int i = 0; i < 2; i++)
        {
            var n = length;
            while (n-- > 0)
            {
                int k = rng.Next(length);
                (array[offset + k], array[offset + n]) = (array[offset + n], array[offset + k]);
            }
        }

        /* Testing procedure 
        void Test()
        {
            const int repetitions = 100;
            const int cycles = 10000;
            const int valueCount = 10;

            var data = new List<int[]>();
            for (int k = 0; k < repetitions; k++)
            {
                var counter = new int[valueCount] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                data.Add(counter);
                for (int i = 0; i < cycles; i++)
                {
                    var arr = new int[valueCount] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                    new Random().Shuffle(arr);

                    for (int j = 0; j < arr.Length; j++)
                    {
                        if (j == arr[j])
                            counter[j]++;
                    }
                }
            }

            StringBuilder sb = new();
            for (int i = 0; i < valueCount; i++)
            {
                sb.Append($"{i} ");
            }
            sb.AppendLine("");

            for (int i = 0; i < data.Count; i++)
            {
                var counter = data[i];
                for (int j = 0; j < counter.Length; j++)
                {
                    sb.Append($"{counter[j]} ");
                }
                sb.AppendLine("");
            }

            Clipboard.SetText(sb.ToString());

            // Now open Excel, paste the result, calculate AVERAGE of each column, and draw a graph
        }
         */
    }
}

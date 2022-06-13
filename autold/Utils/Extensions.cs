using System;

namespace Olfactory.Utils
{
    internal static class String
    {
        public static string ToPath(this string s, string replacement = "-")
        {
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            string[] temp = s.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(replacement, temp);
        }
    }
}

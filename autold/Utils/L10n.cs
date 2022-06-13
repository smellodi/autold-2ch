using WPFLocalizeExtension.Engine;

namespace Olfactory.Utils
{
    public static class L10n
    {
        public static string T(string key)
        {
            return (string)LocalizeDictionary.Instance.GetLocalizedObject(key, null, LocalizeDictionary.Instance.Culture);
        }
    }
}

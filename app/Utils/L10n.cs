using WPFLocalizeExtension.Engine;

namespace AutOlD2Ch.Utils;

public static class L10n
{
    public static string T(string key)
    {
        return (string)LocalizeDictionary.Instance.GetLocalizedObject(key, null, LocalizeDictionary.Instance.Culture);
    }
}

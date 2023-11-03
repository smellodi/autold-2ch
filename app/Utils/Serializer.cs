using System.Linq;

namespace AutOlD2Ch.Utils;

internal static class Serializer
{
    public static string AsDict(object obj)
    {
        return string.Join(", ", obj.GetType().GetProperties().Select(p => $"{p.Name} = {p.GetValue(obj)}"));
    }
}

internal abstract class Serializable
{
    public override string ToString() => Serializer.AsDict(this);
}

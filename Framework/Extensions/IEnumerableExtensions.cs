using System;
using System.Collections.Generic;
using System.Text;

namespace Hyleus.Soundboard.Framework.Extensions;
public static class IEnumerableExtensions {

    public static string AsString<T>(this IEnumerable<T> enumerable) =>
        $"[{string.Join(", ", enumerable)}]";
    public static string AsString<T>(this Span<T> span) =>
        $"[{string.Join(", ", span.ToArray())}]";

    public static bool TryFindValue<T>(this IEnumerable<T> enumerable, Func<T, bool> pred, out T value) {
        foreach (var item in enumerable) {
            if (pred(item)) {
                value = item;
                return true;
            }
        }
        value = default;
        return false;
    }

    public static T GetKey<T, V>(this Dictionary<T, V> dict, V value) {
        foreach (var kvp in dict)
            if (kvp.Value?.Equals(value) == true)
                return kvp.Key;
        return default;
    }

    public static string Decode(this byte[] array, Encoding encoding = null) => (encoding ?? Encoding.ASCII).GetString(array);
}

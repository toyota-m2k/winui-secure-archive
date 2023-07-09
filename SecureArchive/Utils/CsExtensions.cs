using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils; 
public static class CsExtensions {
    public static T? GetValue<T>(this WeakReference<T> w) where T : class {
        if (!w.TryGetTarget(out var target)) {
            return null;
        }
        return target;
    }

    //public static V? GetValue<K, V>(this IDictionary<K, V> dic, K key, V? def=default(V)) {
    //    if (!dic.TryGetValue(key, out var value)) {
    //        return def;
    //    }
    //    return value;
    //}
    public static V GetValue<K, V>(this IDictionary<K, V> dic, K key, V def) { 
        if (!dic.TryGetValue(key, out var value)) {
            return def;
        }
        return value;
    }
    public static V? GetValue<K, V>(this IDictionary<K, V> dic, K key) where V : class {
        if (!dic.TryGetValue(key, out var value)) {
            return null;
        }
        return value;
    }

    public static bool IsNullOrEmpty<T>(IEnumerable<T> v) {
        return !(v?.Any() ?? false);
    }

    public static T[] ArrayOf<T>(params T[] args) {
        return args;
    }

    public static bool IsEmpty([NotNullWhen(false)] this string? s) {
        return string.IsNullOrEmpty(s);
    }
    public static bool IsNotEmpty([NotNullWhen(true)] this string? s) {
        return !string.IsNullOrEmpty(s);
    }

    public static T ParseToEnum<T>(string name, T defValue, bool igonreCase = true) where T : struct {
        if (Enum.TryParse<T>(name, igonreCase, out var result)) {
            return result;
        }

        return defValue;
    }

    public static T Also<T>(this T obj, Action<T> fn) {
        fn(obj);
        return obj;
    }
    public static R Let<T,R>(this T obj, Func<T,R> fn) {
        return fn(obj);
    }
}

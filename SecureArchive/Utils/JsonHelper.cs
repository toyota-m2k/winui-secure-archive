using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SecureArchive.Utils;
public static class Json {
    public static async Task<T?> ToObjectAsync<T>(string value) {
        return await Task.Run(() => {
            return JsonConvert.DeserializeObject<T>(value);
        });
    }

    public static async Task<string> StringifyAsync(object? value) {
        return await Task.Run(() => {
            return JsonConvert.SerializeObject(value);
        });
    }

    public static string? GetStringValue(this JObject jObject, string key) {
        return jObject.GetValue(key)?.Value<string>();
    }
    public static string GetStringValue(this JObject jObject, string key, string fallback) {
        return jObject.GetValue(key)?.Value<string>() ?? fallback;
    }
    public static int GetIntValue(this JObject jObject, string key, int fallback = 0) {
        return jObject.GetValue(key)?.Value<int>() ?? fallback;
    }
    public static long GetLongValue(this JObject jObject, string key, long fallback = 0) {
        return jObject.GetValue(key)?.Value<long>() ?? fallback;
    }
}

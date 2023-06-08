using Newtonsoft.Json;
using System.Text;

namespace SecureArchive.Utils;
internal static class JsonFileHelper {
    public static T? Read<T>(string path) {
        if (File.Exists(path)) {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }
        return default;
    }

    public static void Save<T>(string path, T content) {
        var fileContent = JsonConvert.SerializeObject(content);
        File.WriteAllText(path, fileContent, Encoding.UTF8);
    }

    public static void Delete(string path) {
        if (File.Exists(path)) {
            File.Delete(path);
        }
    }
}

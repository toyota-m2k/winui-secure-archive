using Microsoft.Windows.ApplicationModel.Resources;

namespace SecureArchive.Utils;

public static class ResourceExtensions
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static string GetLocalized(this string resourceKey) => _resourceLoader.GetString(resourceKey);
    public static string SafeGetLocalized(this string resourceKey, string def) {
        try {
            return _resourceLoader.GetString(resourceKey);
        } catch(Exception) {
            return def;
        }
    }
}

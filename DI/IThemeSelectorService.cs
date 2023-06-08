using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace SecureArchive.DI;

public interface IThemeSelectorService
{
    ElementTheme Theme
    {
        get;
    }

    Task InitializeAsync();

    Task SetThemeAsync(ElementTheme theme);

    Task SetRequestedThemeAsync();
}

using Avalonia;

namespace Mhodume;

internal static class Program
{
    // Avalonia needs to be initialised before any control is touched, so keep
    // the entry point tiny and let BuildAvaloniaApp do the setup.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}

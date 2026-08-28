using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Mhodume;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new();
    private readonly ModConfig _config;

    public MainWindow()
    {
        InitializeComponent();

        _config = _store.LoadLive();

        // The crosshair page edits cfg.Crosshair; every change is written back to
        // the live file (debounced), which is what the mod re-reads in game.
        Crosshair.DataContext = _config.Crosshair;
        _config.Crosshair.PropertyChanged += (_, _) => _store.QueueLiveWrite(_config);

        _store.Status += msg => Avalonia.Threading.Dispatcher.UIThread.Post(
            () => StatusText.Text = msg);

        ShowBridgeState();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Says, in one line, whether the config we write actually reaches the game.
    /// On Linux that hinges on finding VHOLUME's Proton prefix — the single most
    /// important thing to get right on this platform, so it is shown up front.
    /// </summary>
    private void ShowBridgeState()
    {
        StatusText.Text = ConfigStore.RootDir;

        if (OperatingSystem.IsWindows())
        {
            BridgeText.Text = "";
            return;
        }

        if (MhodumePaths.BridgedToGame)
        {
            BridgeText.Text = "● bridged to VHOLUME";
            BridgeText.Foreground = this.FindResource("Accent") as IBrush;
        }
        else
        {
            BridgeText.Text = "○ no Proton prefix — launch VHOLUME once, then reopen";
            BridgeText.Foreground = this.FindResource("Muted") as IBrush;
        }
    }
}

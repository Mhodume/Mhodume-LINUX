using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace Mhodume;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new();
    private ModConfig _config = new();
    private bool _suspendWrites;          // set while a profile is being swapped in
    private DispatcherTimer? _gameWatch;

    /// <summary>
    /// One entry in the navigation column: what it is called, which group it
    /// belongs under, and the page it shows. Group headers are rows too, and are
    /// the rows that cannot be selected.
    /// </summary>
    private sealed class Section
    {
        public required string Name { get; init; }
        public string? Group { get; init; }          // set on a header row
        public Control? Page { get; init; }
        public string Number { get; set; } = "";     // filled in below
        public string Note { get; init; } = "";      // what this page does
        public bool NeedsTraining { get; init; }     // training mode required
        public bool IsHeader => Group is not null;
        public string Title => Name.ToUpperInvariant();
        public override string ToString() => Name;
    }

    private List<Section> _sections = new();

    public MainWindow()
    {
        InitializeComponent();

        _store.Status += msg => Dispatcher.UIThread.Post(() => SetStatus(msg, ok: true));

        _sections = new List<Section>
        {
            new() { Name = "OVERLAY", Group = "OVERLAY" },
            new() { Name = "Crosshair", Page = PageCrosshair,
                    Note = "drawn by the mod · applies live" },
            new() { Name = "HUD", Page = PageHud,
                    Note = "the game's own overlay · applies live" },

            new() { Name = "ROUTE", Group = "ROUTE" },
            new() { Name = "Checkpoints", Page = PageCheckpoints,
                    Note = "your own splits" },
            new() { Name = "Trajectory", Page = PageTrajectory,
                    Note = "a saved run drawn in the world" },

            new() { Name = "TOOLS", Group = "TOOLS" },
            new() { Name = "Freecam", Page = PageFreecam,
                    Note = "detach the camera" },
            new() { Name = "Tweaks", Page = PageTweaks,
                    Note = "how the game behaves while you practise" },

            new() { Name = "PROGRESS", Group = "PROGRESS" },
            new() { Name = "Completion", Page = PageCompletion,
                    Note = "levels finished, B-sides, best times — live from your save" },
            new() { Name = "NPCs", Page = PageNpcs,
                    Note = "who you have spoken to, live from your save" },

            new() { Name = "APP", Group = "APP" },
            new() { Name = "Profiles", Page = PageProfiles,
                    Note = "every setting at once, saved under a name" },
            new() { Name = "About", Page = PageAbout,
                    Note = "files and how the mod reaches the game" },
        };

        // Numbered by position among the real destinations, so the column and
        // the page title agree and the headers are not counted.
        var n = 0;
        foreach (var section in _sections)
            if (!section.IsHeader) section.Number = (++n).ToString("00");

        NavList.ItemsSource = _sections;
        NavList.SelectedIndex = 1;              // the first row is a header

        var built = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (built is not null) VersionText.Text = $"v{built.Major}.{built.Minor}";

        _store.SeedDefaultProfiles();
        AttachConfig(_store.LoadLive());

        // Write the whole file back straight away, so a config saved by an older
        // build is completed with any section added since instead of the mod
        // silently falling back to defaults for it.
        _store.FlushLive(_config);

        PageTrajectory.Initialize(_store);
        PageProfiles.Initialize(_store, () => _config);
        PageProfiles.ProfileLoaded += OnProfileLoaded;

        ShowBridgeState();

        StartGameWatch();
        SetStatus("settings loaded", ok: false);
    }


    // -------------------------------------------------------------- config
    private void AttachConfig(ModConfig cfg)
    {
        if (_config is not null) _config.AnyChanged -= Config_AnyChanged;

        _config = cfg;
        _config.AnyChanged += Config_AnyChanged;

        PageCrosshair.DataContext = _config.Crosshair;
        PageHud.DataContext = _config.Hud;
        PageTrajectory.DataContext = _config.Trajectory;
        PageFreecam.DataContext = _config.Freecam;
        PageCrosshair.SetSpeedContext(_config.Speed);
        PageTweaks.DataContext = _config.Tweaks;
        PageCheckpoints.DataContext = _config.Checkpoints;
    }

    private void Config_AnyChanged(object? sender, EventArgs e)
    {
        if (_suspendWrites) return;
        _store.QueueLiveWrite(_config);
    }

    private void OnProfileLoaded(ModConfig cfg, string name)
    {
        _suspendWrites = true;
        AttachConfig(cfg);
        _suspendWrites = false;

        _store.FlushLive(_config);
        _store.SaveLastProfile(name);
        SetStatus($"Profile “{name}” loaded", ok: true);
    }

    // ---------------------------------------------------------- navigation
    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var chosen = NavList.SelectedItem as Section;

        // A header is not a destination. The keyboard can still walk onto one, so
        // it is passed straight through to the next real section.
        if (chosen is { IsHeader: true })
        {
            var from = NavList.SelectedIndex;
            var next = _sections.FindIndex(from + 1, x => !x.IsHeader);
            if (next < 0) next = _sections.FindLastIndex(from, x => !x.IsHeader);
            if (next >= 0) NavList.SelectedIndex = next;
            return;
        }

        foreach (var section in _sections)
            if (section.Page is not null)
                section.Page.IsVisible = ReferenceEquals(section, chosen);

        PageNumber.Text = chosen?.Number ?? "";
        PageTitle.Text = chosen?.Title ?? "";
        PageNote.Text = chosen?.Note ?? "";
        TrainingNeeded.IsVisible = chosen is { NeedsTraining: true };
    }

    // --------------------------------------------------------------- about
    /// <summary>
    /// Says where config is written and — on Linux — whether that path is inside
    /// VHOLUME's Proton prefix, i.e. whether the game will actually read it.
    /// </summary>
    private void ShowBridgeState()
    {
        ConfigPathText.Text = ConfigStore.LivePath;

        if (OperatingSystem.IsWindows())
        {
            BridgeState.Text = "The mod reads this folder directly.";
            BridgeState.Foreground = this.FindResource("Muted") as IBrush;
        }
        else if (MhodumePaths.BridgedToGame)
        {
            BridgeState.Text = "● This folder is inside VHOLUME's Proton prefix — the mod reads it.";
            BridgeState.Foreground = this.FindResource("Accent") as IBrush;
        }
        else
        {
            BridgeState.Text = "○ No Proton prefix found. Launch VHOLUME once through Steam, then reopen this window.";
            BridgeState.Foreground = this.FindResource("Muted") as IBrush;
        }
    }

    private void OpenConfigFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // UseShellExecute opens the folder with the desktop's file manager on
            // both Windows and Linux (xdg-open).
            Process.Start(new ProcessStartInfo(ConfigStore.RootDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus("Could not open the folder: " + ex.Message, ok: false);
        }
    }

    // ------------------------------------------------------------ game watch
    private void StartGameWatch()
    {
        _gameWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _gameWatch.Tick += (_, _) =>
        {
            var running = GameRunning();
            var status = running
                ? ConfigStore.ReadStatus()
                : new ConfigStore.GameStatus(null, false, false);

            LiveBadge.Foreground = this.FindResource(running ? "Accent" : "Muted") as IBrush;

            if (!running)
            {
                GameStatus.Text = "game not running";
                GameStatus.Foreground = this.FindResource("Muted") as IBrush;
            }
            else
            {
                GameStatus.Text = status.Map is null ? "no level loaded" : MapNames.Display(status.Map);
                GameStatus.Foreground = this.FindResource(status.Map is null ? "Muted" : "Text") as IBrush;
                if (!status.Training && status.LapTainted)
                {
                    GameStatus.Text += " — lap spent, restart the level";
                    GameStatus.Foreground = this.FindResource("WarnText") as IBrush;
                }
            }

            PageTrajectory.UpdateCurrentMap(running ? status.Map : null);
        };
        _gameWatch.Start();
    }

    /// <summary>
    /// Best-effort "is VHOLUME up". Under Proton the process keeps its Windows
    /// name, sometimes with the .exe suffix, so both are tried.
    /// </summary>
    private static bool GameRunning()
    {
        // Match by prefix, not exact name: on Linux a process name is the comm
        // field, cut to 15 characters, so under Proton "VHOLUME-Win64-Shipping"
        // shows up as "VHOLUME-Win64-S". Any process starting with VHOLUME is it.
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.ProcessName.StartsWith("VHOLUME", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { /* a process that vanished mid-enumeration */ }
            }
        }
        catch { /* nothing we can do */ }
        return false;
    }

    private void SetStatus(string message, bool ok)
    {
        StatusText.Text = ok ? $"{message} · {DateTime.Now:HH:mm:ss}" : message;
        StatusDot.Fill = this.FindResource(ok ? "Ok" : "Muted") as IBrush;
    }

    protected override void OnClosed(EventArgs e)
    {
        _gameWatch?.Stop();
        _store.FlushLive(_config);
        base.OnClosed(e);
    }
}

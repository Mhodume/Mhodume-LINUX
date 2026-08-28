using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Mhodume;

public partial class TrajectoryPage : UserControl
{
    private static readonly string[] SwatchColors =
    {
        "#00E676", "#00E5FF", "#FFFFFF", "#FFEB3B",
        "#FF3D3D", "#FF00E5", "#FF9100", "#7C4DFF",
    };

    private ConfigStore? _store;
    private TrajectoryConfig? _config;
    private List<GhostInfo> _all = new();
    private bool _scanned;

    public TrajectoryPage()
    {
        InitializeComponent();
        BuildSwatches();
        DataContextChanged += (_, _) =>
        {
            if (_config is not null) _config.PropertyChanged -= Config_PropertyChanged;
            _config = DataContext as TrajectoryConfig;
            if (_config is not null) _config.PropertyChanged += Config_PropertyChanged;

            UpdateColorMode();
            ShowLoaded();
            RefreshMapWarning();
        };

        // Avalonia has no IsVisibleChanged; observe the property instead. It
        // emits the current value on subscribe (before the host has set this
        // page hidden) and on every change after, so the first emission is
        // skipped and the scan waits for the page to actually be shown — as the
        // WPF IsVisibleChanged did.
        this.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty) return;
            if (e.GetNewValue<bool>() && !_scanned) _ = ScanAsync();
        };
    }


    public void Initialize(ConfigStore store) => _store = store;

    private string? _currentMap;

    /// <summary>
    /// Called with the level the game currently has loaded (null when it is not
    /// running). Nothing gets drawn on a map the loaded run does not belong to,
    /// so say it plainly rather than letting the user wonder.
    /// </summary>
    public void UpdateCurrentMap(string? map)
    {
        _currentMap = map;
        RefreshMapWarning();
    }

    private void RefreshMapWarning()
    {
        var wanted = _config?.Map;

        if (_config is null || !_config.Enabled || string.IsNullOrWhiteSpace(wanted) ||
            string.IsNullOrWhiteSpace(_currentMap) || MapsMatch(_currentMap!, wanted!))
        {
            MapWarning.IsVisible = false;
            return;
        }

        MapWarningText.Text =
            $"You are playing {_currentMap}, but this run was recorded on {wanted}. " +
            "Nothing will be drawn until you load that map, or pick a run from the one you are on.";
        MapWarning.IsVisible = true;
    }

    /// <summary>Mirrors the loose comparison the Lua module uses.</summary>
    private static bool MapsMatch(string a, string b)
        => a.Equals(b, StringComparison.OrdinalIgnoreCase)
           || a.Contains(b, StringComparison.OrdinalIgnoreCase)
           || b.Contains(a, StringComparison.OrdinalIgnoreCase);

    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrajectoryConfig.Gradient)) UpdateColorMode();
        if (e.PropertyName is nameof(TrajectoryConfig.Color) or nameof(TrajectoryConfig.LineColor))
            UpdateHexBox();
        if (e.PropertyName is nameof(TrajectoryConfig.Map) or nameof(TrajectoryConfig.Enabled))
            RefreshMapWarning();
    }

    /// <summary>
    /// Both colour modes stay on screen: hiding the swatches behind an
    /// unchecked box made the single-colour option impossible to discover.
    /// Only the gradient legend comes and goes.
    /// </summary>
    private void UpdateColorMode()
    {
        var gradient = _config?.Gradient ?? true;
        GradientLegend.IsVisible = gradient;
        GradientLabels.IsVisible = gradient;

        // Both, always, and in this order: checking one makes the other clear,
        // so setting only one leaves the result to whatever ran last.
        SpeedRadio.IsChecked = gradient;
        SolidRadio.IsChecked = !gradient;
        UpdateHexBox();
    }

    private void SpeedRadio_Checked(object? sender, RoutedEventArgs e)
    {
        if (SpeedRadio.IsChecked == true && _config is not null) _config.Gradient = true;
    }

    private void SolidRadio_Checked(object? sender, RoutedEventArgs e)
    {
        if (SolidRadio.IsChecked == true && _config is not null) _config.Gradient = false;
    }

    private void UpdateHexBox()
    {
        if (_config is null) return;
        var c = _config.LineColor;
        var text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        if (!string.Equals(HexBox.Text, text, StringComparison.OrdinalIgnoreCase))
            HexBox.Text = text;
    }

    private void HexBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyHex();
    }

    private void HexBox_LostFocus(object? sender, RoutedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (_config is null) return;
        var text = (HexBox.Text ?? "").Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        try
        {
            PickColour(Color.Parse(text));
        }
        catch
        {
            UpdateHexBox();     // reject silently, show the real value back
        }
    }

    /// <summary>Choosing a colour implies you want that colour, not the gradient.</summary>
    private void PickColour(Color c)
    {
        if (_config is null) return;
        _config.LineColor = c;
        _config.Gradient = false;
    }

    // -------------------------------------------------------------- scanning
    private async Task ScanAsync()
    {
        _scanned = true;
        ScanNotice.IsVisible = true;
        GhostList.ItemsSource = null;
        MapBox.ItemsSource = null;

        var found = await Task.Run(GhostFile.Discover);

        _all = found;
        var maps = found.Select(g => g.Map).Distinct().OrderBy(m => m).ToList();

        ScanNotice.IsVisible = false;

        if (maps.Count == 0)
        {
            ScanNotice.Text = "No ghost files found in your VHOLUME save folder.";
            ScanNotice.IsVisible = true;
            return;
        }

        MapBox.ItemsSource = maps;

        // preselect the map of whatever is currently loaded
        // prefer the map in play, then the loaded run's map, then the first one
        var preferred = maps.FirstOrDefault(m => _currentMap is not null && MapsMatch(_currentMap, m))
                        ?? (_config?.Map is { Length: > 0 } cm && maps.Contains(cm) ? cm : null)
                        ?? maps[0];
        MapBox.SelectedItem = preferred;
    }

    private async void Rescan_Click(object? sender, RoutedEventArgs e)
    {
        _scanned = false;
        await ScanAsync();
    }

    private void MapBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MapBox.SelectedItem is not string map) return;

        // best (fastest completed) first, unfinished runs last
        GhostList.ItemsSource = _all
            .Where(g => g.Map == map)
            .OrderBy(g => g.Completed ? 0 : 1)
            .ThenBy(g => g.TimeMs)
            .ToList();

        LoadButton.IsEnabled = false;
    }

    private void GhostList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => LoadButton.IsEnabled = GhostList.SelectedItem is GhostInfo;

    // ---------------------------------------------------------------- loading
    private void Load_Click(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _config is null) return;
        if (GhostList.SelectedItem is not GhostInfo info) return;

        try
        {
            var traj = GhostFile.Load(info.Path);
            if (traj.PointCount < 2)
            {
                // TODO(linux): was MessageBox.Show("That run has no usable path data.", "Mhodume");
                LoadedDetail.Text = "That run has no usable path data.";
                return;
            }

            _store.WriteTrajectory(traj);

            _config.Map = traj.Map;
            _config.SourcePath = info.Path;
            _config.Label = $"{info.PlayerName} — {info.TimeText} on {MapNames.Display(traj.Map)}";
            _config.Enabled = true;

            ShowLoaded(traj);
            RefreshMapWarning();
        }
        catch (Exception ex)
        {
            // TODO(linux): was MessageBox.Show("Could not read that ghost:\n\n" + ex.Message, "Mhodume");
            LoadedDetail.Text = "Could not read that ghost: " + ex.Message;
        }
    }

    private void ShowLoaded(Trajectory? traj = null)
    {
        if (_config is null || string.IsNullOrWhiteSpace(_config.Label))
        {
            LoadedTitle.Text = "No run loaded";
            LoadedDetail.Text = "Select a run above and press “Draw this run”.";
            return;
        }

        LoadedTitle.Text = _config.Label;
        LoadedDetail.Text = traj is not null
            ? $"{traj.PointCount} points across {traj.Segments.Count} segment" +
              (traj.Segments.Count == 1 ? "" : "s") +
              $". The line is only drawn while you are playing {MapNames.Display(traj.Map)}."
            : $"The line is only drawn while you are playing {MapNames.Display(_config.Map)}.";
    }

    // ----------------------------------------------------------------- inputs
    /// <summary>
    /// Opens the loaded run's keys end to end. Read back from the ghost rather
    /// than kept in memory: the page holds a config, not a run, and the file it
    /// came from is named in that config.
    /// </summary>
    private void ShowInputs_Click(object? sender, RoutedEventArgs e)
    {
        if (_config is null || string.IsNullOrWhiteSpace(_config.SourcePath))
        {
            // TODO(linux): was MessageBox.Show("No run is loaded. Pick one above and press “Draw this run”.", "Mhodume");
            LoadedDetail.Text = "No run is loaded. Pick one above and press “Draw this run”.";
            return;
        }

        try
        {
            var traj = GhostFile.Load(_config.SourcePath);
            var owner = TopLevel.GetTopLevel(this) as Window;
            var window = new InputsWindow();
            window.Attach(_config);
            window.Present(traj, _config.Label);
            if (owner is not null) window.Show(owner);
            else window.Show();
        }
        catch (Exception ex)
        {
            // TODO(linux): was MessageBox.Show("Could not read that ghost:\n\n" + ex.Message, "Mhodume");
            LoadedDetail.Text = "Could not read that ghost: " + ex.Message;
        }
    }

    // ----------------------------------------------------------------- colour
    private void BuildSwatches()
    {
        foreach (var hex in SwatchColors)
        {
            var color = Color.Parse(hex);
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(color),
                BorderBrush = this.FindResource("Edge") as IBrush,
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(swatch, hex);
            swatch.PointerReleased += (_, _) => PickColour(color);
            Swatches.Items.Add(swatch);
        }
    }
}

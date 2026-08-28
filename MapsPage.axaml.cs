using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Mhodume;

/// <summary>
/// Lists every map the game has kept something for — a best time, a recorded
/// run, or both — and lets a run be drawn as a route to practise against.
/// </summary>
public partial class MapsPage : UserControl
{
    private ConfigStore? _store;
    private TrajectoryConfig? _trajectory;
    private List<MapEntry> _all = new();
    private bool _scanned;

    public MapsPage()
    {
        InitializeComponent();

        // Reading every ghost header means opening and unzipping each file, so
        // it waits until the page is actually looked at.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && e.GetNewValue<bool>() && !_scanned)
                _ = ScanAsync();
        };
    }


    public void Initialize(ConfigStore store, TrajectoryConfig trajectory)
    {
        _store = store;
        _trajectory = trajectory;
    }

    /// <summary>Drops the cached listing so the next visit re-reads the disk.</summary>
    public void Invalidate() => _scanned = false;

    // ------------------------------------------------------------- scanning
    private async Task ScanAsync()
    {
        _scanned = true;
        MapList.ItemsSource = null;
        ListSummary.Text = "";
        EmptyState.IsVisible = false;
        ScanSkeleton.IsVisible = true;

        try
        {
            _all = await Task.Run(MapLibrary.Build);
        }
        catch (Exception ex)
        {
            _all = new List<MapEntry>();
            ShowEmpty("Could not read the save folder", ex.Message);
            return;
        }
        finally
        {
            ScanSkeleton.IsVisible = false;
        }

        ApplyFilter();
    }

    private void Rescan_Click(object? sender, RoutedEventArgs e) => _ = ScanAsync();

    // -------------------------------------------------------------- filtering
    private void Filter_Changed(object? sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!IsInitialized) return;

        var needle = FilterBox.Text?.Trim() ?? "";
        FilterHint.IsVisible = !(FilterBox.Text?.Length > 0);
        var onlyRuns = OnlyWithRuns.IsChecked == true;

        var shown = _all
            .Where(m => !onlyRuns || m.HasGhosts)
            // Either name will do: people who know the game type "admin",
            // people looking at their ghost folder type "fastlife02".
            .Where(m => needle.Length == 0 ||
                        m.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                        m.Map.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var keep = MapList.SelectedItem as MapEntry;
        MapList.ItemsSource = shown;
        if (keep is not null && shown.Contains(keep)) MapList.SelectedItem = keep;

        ListSummary.Text = _all.Count == 0
            ? ""
            : $"{_all.Count} maps · {_all.Count(m => m.HasGhosts)} with runs";

        // Three different nothings, and they want three different answers.
        if (_all.Count == 0)
            ShowEmpty("No times recorded yet",
                      "Finish any level once — times are read from your save file.");
        else if (shown.Count == 0 && needle.Length > 0)
            ShowEmpty("No map matches that",
                      $"Nothing in your save is called “{needle}”.");
        else if (shown.Count == 0)
            ShowEmpty("No map has a saved run",
                      "Runs come from your ghost folder. Untick “with runs only” to see every map.");
        else
            EmptyState.IsVisible = false;
    }

    private void ShowEmpty(string title, string why)
    {
        EmptyTitle.Text = title;
        EmptyWhy.Text = why;
        EmptyState.IsVisible = true;
    }

    // --------------------------------------------------------------- detail
    private void MapList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var entry = MapList.SelectedItem as MapEntry;

        if (entry is null)
        {
            DetailMap.Text = "No map selected";
            DetailAsset.Text = "";
            DetailBest.Text = "Pick a map on the left.";
            VariantList.ItemsSource = null;
            RunList.ItemsSource = null;
            NoRuns.IsVisible = false;
            DrawButton.IsEnabled = false;
            SourceNote.Text = "";
            return;
        }

        DetailMap.Text = entry.DisplayName;
        DetailAsset.Text = entry.ShortCode.Length > 0
            ? entry.AssetNote + "  ·  ghost code " + entry.ShortCode
            : entry.AssetNote;
        DetailBest.Text = entry.Best is { } b
            ? "Best time " + MapEntry.Format(b)
            : "No time recorded for this map yet.";

        VariantList.ItemsSource = entry.Variants
            .Select(v => new { v.Name, Time = MapEntry.Format(v.Time) })
            .ToList();

        RunList.ItemsSource = entry.Ghosts;
        NoRuns.IsVisible = !entry.HasGhosts;
        DrawButton.IsEnabled = false;

        SourceNote.Text = entry.HasGhosts
            ? $"Times from your save file. {entry.Ghosts.Count} run(s) read from the ghost folder."
            : "Time from your save file. VHOLUME keeps a ghost only for some runs, so there may be nothing to draw here.";
    }

    private void RunList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        => DrawButton.IsEnabled = RunList.SelectedItem is GhostInfo;

    // --------------------------------------------------------------- drawing
    private void Draw_Click(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _trajectory is null) return;
        if (RunList.SelectedItem is not GhostInfo info) return;

        try
        {
            var traj = GhostFile.Load(info.Path);
            if (traj.PointCount < 2)
            {
                DrawNotice.Text = "That run has no usable path data.";
                return;
            }

            _store.WriteTrajectory(traj);

            _trajectory.Map = traj.Map;
            _trajectory.SourcePath = info.Path;
            _trajectory.Label = $"{info.PlayerName} — {info.TimeText} on {MapNames.Display(traj.Map)}";
            _trajectory.Enabled = true;

            DrawNotice.Text =
                $"Loaded {traj.PointCount} points from {info.PlayerName}'s run. " +
                "Press F7 in game to turn training mode on and see it.";
        }
        catch (Exception ex)
        {
            DrawNotice.Text = "Could not read that run: " + ex.Message;
        }
    }
}

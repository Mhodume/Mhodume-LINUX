using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Mhodume;

/// <summary>
/// Overall progress read live from the save: which levels are finished, which
/// have their B-side, and the best time on each. The roster is the game's full
/// level list (MapNames); the ticks come from the save.
/// </summary>
public partial class CompletionPage : UserControl
{
    /// <summary>One level's line: name, best time, and the two ticks.</summary>
    public record MapRow(string Display, string Best, string Finished, string BSide);

    private readonly DispatcherTimer _watch;

    public CompletionPage()
    {
        InitializeComponent();

        _watch = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
        _watch.Tick += (_, _) => Refresh();

        this.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty) return;
            if (e.NewValue is true) { Refresh(); _watch.Start(); }
            else _watch.Stop();
        };
    }

    private void Refresh()
    {
        var best = SaveFile.BestTimes();
        var finished = new HashSet<string>(SaveFile.MapsFinished(),
                                           System.StringComparer.OrdinalIgnoreCase);
        var bside = new HashSet<string>(SaveFile.BSideMaps(),
                                        System.StringComparer.OrdinalIgnoreCase);

        var rows = new List<MapRow>();
        int nFinished = 0, nBside = 0;

        foreach (var (asset, display) in MapNames.All)
        {
            var f = finished.Contains(asset);
            var bs = bside.Contains(asset);
            if (f) nFinished++;
            if (bs) nBside++;

            rows.Add(new MapRow(
                display,
                best.TryGetValue(asset, out var t) ? MapEntry.Format(t) : "—",
                f ? "✓" : "",
                bs ? "✓" : ""));
        }

        MapList.ItemsSource = rows;
        Summary.Text = $"{nFinished} / {rows.Count} levels finished";
        var timed = rows.Count(r => r.Best != "—");
        Detail.Text = $"{nBside} B-sides · {timed} timed";
    }
}

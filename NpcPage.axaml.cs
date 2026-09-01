using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Mhodume;

/// <summary>
/// Tracks which talkable NPCs you have spoken to, read live from the save. The
/// full roster comes from <see cref="NpcRoster"/> (a 100%-complete save), so a
/// map shows both what you have found and what is still out there.
/// </summary>
public partial class NpcPage : UserControl
{
    /// <summary>One map's line in the list.</summary>
    public record MapRow(string Display, string Count, string Missing, bool Complete);

    private readonly DispatcherTimer _watch;

    public NpcPage()
    {
        InitializeComponent();

        // The save is rewritten as you play, so re-read it while the page is up.
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
        var talked = new HashSet<string>(SaveFile.NpcsInteractedWith(),
                                         System.StringComparer.OrdinalIgnoreCase);

        var rows = new List<MapRow>();
        var totalTalked = 0;

        foreach (var g in NpcRoster.All
                     .GroupBy(NpcRoster.MapOf)
                     .OrderBy(g => MapNames.Display(g.Key), System.StringComparer.OrdinalIgnoreCase))
        {
            var keys = g.ToList();
            var have = keys.Count(talked.Contains);
            totalTalked += have;

            var missing = keys.Where(k => !talked.Contains(k))
                              .Select(NpcRoster.ShortId)
                              .ToList();

            rows.Add(new MapRow(
                MapNames.Display(g.Key),
                $"{have}/{keys.Count}",
                missing.Count == 0 ? "" : "missing " + string.Join(" ", missing),
                missing.Count == 0));
        }

        MapList.ItemsSource = rows;
        Summary.Text = $"{totalTalked} / {NpcRoster.Total} NPCs spoken to";
        var maps = rows.Count(r => r.Complete);
        MapsSummary.Text = $"{maps} / {rows.Count} maps fully done";
    }
}

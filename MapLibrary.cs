namespace Mhodume;

/// <summary>One map, with the best time on it and the runs recorded there.</summary>
public class MapEntry
{
    public required string Map { get; init; }
    public TimeSpan? Best { get; init; }
    public List<GhostInfo> Ghosts { get; init; } = new();

    /// <summary>Variant keys for this map, e.g. "Normal_Base", with their times.</summary>
    public List<(string Name, TimeSpan Time)> Variants { get; init; } = new();

    public string BestText => Best is { } b ? Format(b) : "—";

    /// <summary>The run count as a figure, for a column that has to line up.</summary>
    public string RunCountText => Ghosts.Count == 0 ? "—" : Ghosts.Count.ToString();

    /// <summary>What the game's own menu calls this level.</summary>
    public string DisplayName => MapNames.Display(Map);

    /// <summary>The short code the game uses in ghost links.</summary>
    public string ShortCode => MapNames.Code(Map);

    /// <summary>The file name, for anyone matching this against a save or a ghost.</summary>
    public string AssetNote => MapNames.IsKnown(Map) ? Map : Map + " (not in the game's list)";
    public string GhostText => Ghosts.Count switch
    {
        0 => "no run saved",
        1 => "1 run saved",
        var n => $"{n} runs saved",
    };

    /// <summary>True when the map has something to practise against.</summary>
    public bool HasGhosts => Ghosts.Count > 0;

    public static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss\.fff") : t.ToString(@"m\:ss\.fff");
}

/// <summary>
/// Brings together what the game records about each map: the best times from
/// the save file, and the ghost recordings on disk.
///
/// Neither source is complete on its own — a map can have a time with no ghost
/// kept, and a ghost can exist for a map whose time was never written — so the
/// listing is the union of both.
/// </summary>
public static class MapLibrary
{
    public static List<MapEntry> Build()
    {
        var times = SaveFile.BestTimes();
        var ghosts = GhostFile.Discover();

        // "Gold_racetrack" is a map; "Gold_racetrack_Normal_Base" is a variant
        // of it. A key is a variant when another key is its prefix.
        var keys = times.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? BaseOf(string key)
        {
            foreach (var candidate in keys)
                if (candidate.Length < key.Length &&
                    key.StartsWith(candidate + "_", StringComparison.OrdinalIgnoreCase))
                    return candidate;
            return null;
        }

        var variants = new Dictionary<string, List<(string, TimeSpan)>>(StringComparer.OrdinalIgnoreCase);
        var maps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, time) in times)
        {
            var parent = BaseOf(key);
            if (parent is null)
            {
                maps.Add(key);
                continue;
            }
            if (!variants.TryGetValue(parent, out var list))
                variants[parent] = list = new List<(string, TimeSpan)>();
            list.Add((key[(parent.Length + 1)..], time));
        }

        var byMap = ghosts.GroupBy(g => g.Map, StringComparer.OrdinalIgnoreCase)
                          .ToDictionary(g => g.Key,
                                        g => g.OrderBy(x => x.TimeMs).ToList(),
                                        StringComparer.OrdinalIgnoreCase);
        foreach (var map in byMap.Keys) maps.Add(map);

        return maps
            .Select(map => new MapEntry
            {
                Map = map,
                Best = times.TryGetValue(map, out var t) ? t : null,
                Ghosts = byMap.TryGetValue(map, out var g) ? g : new List<GhostInfo>(),
                Variants = variants.TryGetValue(map, out var v)
                    ? v.OrderBy(x => x.Item1).ToList()
                    : new List<(string, TimeSpan)>(),
            })
            // By the name that is shown, not the one on disk: a list sorted by
            // file name looks unsorted when it is drawn by display name.
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

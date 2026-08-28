using System.Globalization;
using System.IO;

namespace Mhodume;

/// <summary>One checkpoint on a map.</summary>
public record CheckpointPoint(double X, double Y, double Z);

/// <summary>
/// A pair of checkpoints and the times recorded between them.
///
/// Checkpoints pair up in the order they were dropped: the first opens a
/// section and the second closes it. A trailing checkpoint with no partner is
/// still shown, so it can be seen and removed rather than silently ignored.
/// </summary>
public class CheckpointSection
{
    public required int Number { get; init; }
    public required CheckpointPoint Start { get; init; }
    public CheckpointPoint? End { get; init; }

    public double? Best { get; set; }
    public double? Previous { get; set; }
    public double? Last { get; set; }

    public bool Complete => End is not null;

    public string Title => Complete ? $"Section {Number}" : $"Section {Number} — unpaired";
    public string BestText => Format(Best);
    public string PreviousText => Format(Previous);
    public string LastText => Format(Last);

    /// <summary>
    /// How the newest run compares with the best. When it is the best - the
    /// two are the same run - there is nothing to subtract and something to
    /// say instead.
    /// </summary>
    public string DeltaText
    {
        get
        {
            if (Last is not { } l || Best is not { } b) return "";
            var d = l - b;
            return d <= 0.0005 ? "new best" : $"+{d:0.000}";
        }
    }

    public bool LastIsBest => Last is { } l && Best is { } b && l - b <= 0.0005;

    /// <summary>How far apart the two ends are, as a rough sense of size.</summary>
    public string LengthText
    {
        get
        {
            if (End is null) return "no end point yet";
            var dx = End.X - Start.X;
            var dy = End.Y - Start.Y;
            var dz = End.Z - Start.Z;
            var d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            return $"{d / 100:0.#} m apart";
        }
    }

    private static string Format(double? seconds)
    {
        if (seconds is not { } s) return "—";
        var t = TimeSpan.FromSeconds(s);
        return t.TotalMinutes >= 1 ? t.ToString(@"m\:ss\.fff") : $"{s:0.000}";
    }
}

/// <summary>
/// Reads and writes the checkpoint files the mod keeps.
///
/// Same format the mod uses, deliberately: "M &lt;map&gt;" then "P x y z" for
/// checkpoints, and "S index best previous last" for times. Both are plain
/// text so they can be shared by hand as easily as through the app.
/// </summary>
public static class CheckpointStore
{
    public static string CheckpointsPath => Path.Combine(ConfigStore.RootDir, "checkpoints.txt");
    public static string SplitsPath => Path.Combine(ConfigStore.RootDir, "splits.txt");

    // ------------------------------------------------------------- reading
    public static Dictionary<string, List<CheckpointPoint>> ReadCheckpoints()
    {
        var maps = new Dictionary<string, List<CheckpointPoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (map, line) in ReadLines(CheckpointsPath))
        {
            if (!line.StartsWith("P ")) continue;
            var bits = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length < 3) continue;
            if (!double.TryParse(bits[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !double.TryParse(bits[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                continue;

            if (!maps.TryGetValue(map, out var list)) maps[map] = list = new List<CheckpointPoint>();
            list.Add(new CheckpointPoint(x, y, z));
        }
        return maps;
    }

    /// <summary>Times per map, indexed by section number.</summary>
    public static Dictionary<string, Dictionary<int, (double? Best, double? Prev, double? Last)>> ReadSplits()
    {
        var maps = new Dictionary<string, Dictionary<int, (double?, double?, double?)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (map, line) in ReadLines(SplitsPath))
        {
            if (!line.StartsWith("S ")) continue;
            var bits = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length < 3) continue;
            if (!int.TryParse(bits[0], out var index)) continue;

            // three fields is the older layout, with no previous time
            double? best = Parse(bits[1]);
            double? prev = bits.Length >= 4 ? Parse(bits[2]) : null;
            double? last = Parse(bits[^1]);

            if (!maps.TryGetValue(map, out var times))
                maps[map] = times = new Dictionary<int, (double?, double?, double?)>();
            times[index] = (best, prev, last);
        }
        return maps;

        static double? Parse(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0
                ? v : null;
    }

    /// <summary>Everything known about one map, paired up into sections.</summary>
    public static List<CheckpointSection> SectionsFor(string map)
    {
        var points = ReadCheckpoints().TryGetValue(map, out var list)
            ? list : new List<CheckpointPoint>();
        var times = ReadSplits().TryGetValue(map, out var t)
            ? t : new Dictionary<int, (double?, double?, double?)>();

        var sections = new List<CheckpointSection>();
        for (var i = 0; i < points.Count; i += 2)
        {
            var number = i / 2 + 1;
            var section = new CheckpointSection
            {
                Number = number,
                Start = points[i],
                End = i + 1 < points.Count ? points[i + 1] : null,
            };
            if (times.TryGetValue(number, out var v))
                (section.Best, section.Previous, section.Last) = v;
            sections.Add(section);
        }
        return sections;
    }

    public static IEnumerable<string> Maps() => ReadCheckpoints().Keys.OrderBy(m => m);

    // ------------------------------------------------------------- writing
    public static void WriteCheckpoints(Dictionary<string, List<CheckpointPoint>> maps)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (map, points) in maps.OrderBy(kv => kv.Key))
        {
            if (points.Count == 0) continue;
            sb.Append("M ").Append(map).Append('\n');
            foreach (var p in points)
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                                        "P {0:0.0} {1:0.0} {2:0.0}\n", p.X, p.Y, p.Z));
        }
        WriteAtomic(CheckpointsPath, sb.ToString());
    }

    /// <summary>
    /// Removes one section from a map — both its checkpoints — and shifts the
    /// times of every section after it down to match, since sections are
    /// numbered by position rather than carrying an identity.
    /// </summary>
    public static void DeleteSection(string map, int number)
    {
        var maps = ReadCheckpoints();
        if (!maps.TryGetValue(map, out var points)) return;

        var first = (number - 1) * 2;
        if (first >= points.Count) return;
        var count = Math.Min(2, points.Count - first);
        points.RemoveRange(first, count);
        WriteCheckpoints(maps);

        ShiftSplitsDown(map, number);
    }

    public static void DeleteMap(string map)
    {
        var maps = ReadCheckpoints();
        if (!maps.Remove(map)) return;
        WriteCheckpoints(maps);

        var splits = ReadSplits();
        if (splits.Remove(map)) WriteSplits(splits);
    }

    public static void ClearTimes(string map)
    {
        var splits = ReadSplits();
        if (splits.Remove(map)) WriteSplits(splits);
    }

    private static void ShiftSplitsDown(string map, int removed)
    {
        var splits = ReadSplits();
        if (!splits.TryGetValue(map, out var times)) return;

        var moved = new Dictionary<int, (double?, double?, double?)>();
        foreach (var (index, value) in times)
        {
            if (index < removed) moved[index] = value;
            else if (index > removed) moved[index - 1] = value;
            // the removed section's own times go with it
        }
        splits[map] = moved;
        WriteSplits(splits);
    }

    private static void WriteSplits(
        Dictionary<string, Dictionary<int, (double? Best, double? Prev, double? Last)>> maps)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (map, times) in maps.OrderBy(kv => kv.Key))
        {
            if (times.Count == 0) continue;
            sb.Append("M ").Append(map).Append('\n');
            foreach (var index in times.Keys.OrderBy(i => i))
            {
                var (best, prev, last) = times[index];
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                                        "S {0} {1:0.000} {2:0.000} {3:0.000}\n",
                                        index, best ?? -1, prev ?? -1, last ?? -1));
            }
        }
        WriteAtomic(SplitsPath, sb.ToString());
    }

    // ------------------------------------------------------------- sharing
    /// <summary>Writes one map's checkpoints to a file someone else can import.</summary>
    public static void Export(string map, string path)
    {
        var points = ReadCheckpoints().TryGetValue(map, out var list)
            ? list : new List<CheckpointPoint>();

        var sb = new System.Text.StringBuilder();
        sb.Append("M ").Append(map).Append('\n');
        foreach (var p in points)
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                                    "P {0:0.0} {1:0.0} {2:0.0}\n", p.X, p.Y, p.Z));
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Writes a single section — its two checkpoints — to a file.
    ///
    /// A section is the unit worth sharing: one stretch someone worked out a
    /// line for. Exporting a whole map is the coarser case, kept for when you
    /// want to hand over an entire route.
    /// </summary>
    public static void ExportSection(string map, int number, string path)
    {
        var points = ReadCheckpoints().TryGetValue(map, out var list)
            ? list : new List<CheckpointPoint>();

        var first = (number - 1) * 2;
        if (first >= points.Count) return;
        var count = Math.Min(2, points.Count - first);

        var sb = new System.Text.StringBuilder();
        sb.Append("M ").Append(map).Append('\n');
        for (var i = first; i < first + count; i++)
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                                    "P {0:0.0} {1:0.0} {2:0.0}\n",
                                    points[i].X, points[i].Y, points[i].Z));
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Adds the checkpoints from a file. Returns the maps it touched and how
    /// many points came in, so the caller can say what happened rather than
    /// leaving it to be discovered.
    /// </summary>
    public static (int Maps, int Points) Import(string path, bool replace)
    {
        var incoming = new Dictionary<string, List<CheckpointPoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (map, line) in ReadLines(path))
        {
            if (!line.StartsWith("P ")) continue;
            var bits = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length < 3) continue;
            if (!double.TryParse(bits[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !double.TryParse(bits[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                continue;

            if (!incoming.TryGetValue(map, out var list)) incoming[map] = list = new List<CheckpointPoint>();
            list.Add(new CheckpointPoint(x, y, z));
        }

        if (incoming.Count == 0) return (0, 0);

        var mine = ReadCheckpoints();
        var points = 0;
        foreach (var (map, list) in incoming)
        {
            points += list.Count;
            if (replace || !mine.TryGetValue(map, out var existing))
            {
                mine[map] = new List<CheckpointPoint>(list);
                ClearTimes(map);        // the numbering changed; the times no longer apply
            }
            else
            {
                existing.AddRange(list);
            }
        }
        WriteCheckpoints(mine);
        return (incoming.Count, points);
    }

    // ------------------------------------------------------------- plumbing
    /// <summary>Yields (current map, line) for the "M name" / body format.</summary>
    private static IEnumerable<(string Map, string Line)> ReadLines(string path)
    {
        string[] lines;
        try
        {
            if (!File.Exists(path)) yield break;
            lines = File.ReadAllLines(path);
        }
        catch (IOException) { yield break; }

        var map = "";
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("M "))
            {
                map = line[2..].Trim();
                continue;
            }
            if (map.Length > 0) yield return (map, line);
        }
    }

    private static void WriteAtomic(string path, string text)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, overwrite: true);
    }
}

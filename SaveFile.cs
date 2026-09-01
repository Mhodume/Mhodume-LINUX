using System.IO;
using System.Linq;
using System.Text;

namespace Mhodume;

/// <summary>
/// Reads the best times out of VHOLUME's save file.
///
/// The save is GVAS-flavoured but not standard GVAS: properties are written as
/// (FString name, FString type, ...payload) without the usual int64 size field,
/// so offsets cannot be skipped blindly. BestTimes is a Map&lt;Name, Timespan&gt;
/// and Timespan is stored as UE ticks — 100 nanoseconds each.
///
/// Rather than hard-coding where the entry count sits after the map's type
/// metadata, the reader scans forward for the first position where a count and
/// that many (name, ticks) pairs parse cleanly AND land exactly on the next
/// property header. A wrong guess cannot satisfy all three.
/// </summary>
public static class SaveFile
{
    public static string SaveDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VHOLUME", "Saved", "SaveGames");

    public static string SavePath => Path.Combine(SaveDir, "VHOLUME_Save1.sav");

    private const long TicksPerUeTick = 1;   // UE ticks are 100ns, same as .NET
    private const int MaxEntries = 20000;

    /// <summary>
    /// Best time per map key. Keys come in two shapes: the bare map name, and
    /// "&lt;map&gt;_&lt;mode&gt;_&lt;variant&gt;" for a specific variant.
    /// </summary>
    public static Dictionary<string, TimeSpan> BestTimes()
    {
        var result = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        byte[] b;
        try { b = File.ReadAllBytes(SavePath); }
        catch { return result; }

        var at = Find(b, "BestTimes");
        if (at < 0) return result;

        // step over the name and the "MapProperty" type that follows it
        var o = at - 4;
        if (ReadString(b, ref o) is null) return result;
        if (ReadString(b, ref o) is not "MapProperty") return result;

        // the entry count is somewhere in the next stretch of type metadata
        for (var probe = o; probe < Math.Min(b.Length - 4, o + 256); probe++)
        {
            var entries = TryReadEntries(b, probe);
            if (entries is null) continue;
            foreach (var (key, ticks) in entries) result[key] = TimeSpan.FromTicks(ticks);
            return result;
        }
        return result;
    }

    /// <summary>The NPCs spoken to, as "&lt;map&gt;:BP_NPC_Dialog_C_&lt;n&gt;" keys.</summary>
    public static List<string> NpcsInteractedWith() =>
        ReadStringArray("NPCInteractedWith", s => s.Contains(':'));

    /// <summary>The maps finished at least once, as level asset names.</summary>
    public static List<string> MapsFinished() =>
        ReadStringArray("MapFinishedOnce", LooksLikeMap);

    /// <summary>The maps whose B-side has been collected, as level asset names.</summary>
    public static List<string> BSideMaps() =>
        ReadStringArray("BSideCollectedMaps", LooksLikeMap);

    /// <summary>
    /// A level asset name: starts with a letter, then word characters, and not a
    /// property-type name like "StrProperty" — those sit in the array's metadata
    /// and would otherwise be taken as the first entry and pass the scan.
    /// </summary>
    private static bool LooksLikeMap(string s) =>
        s.Length > 2 && char.IsLetter(s[0])
        && s.All(c => char.IsLetterOrDigit(c) || c == '_')
        && !s.EndsWith("Property");

    /// <summary>
    /// Reads a named ArrayProperty of strings. The array's type metadata is
    /// stepped over by scanning: the count is the first position where it and
    /// that many strings all passing <paramref name="valid"/> parse cleanly — a
    /// stray integer taken as a count then fails the test, so the scan moves on.
    /// </summary>
    private static List<string> ReadStringArray(string key, Func<string, bool> valid)
    {
        var result = new List<string>();
        byte[] b;
        try { b = File.ReadAllBytes(SavePath); }
        catch { return result; }

        var at = Find(b, key);
        if (at < 0) return result;

        var o = at - 4;
        if (ReadString(b, ref o) is null) return result;

        for (var probe = o; probe < Math.Min(b.Length - 4, o + 128); probe++)
        {
            var list = TryReadArray(b, probe, valid);
            if (list is not null) return list;
        }
        return result;
    }

    private static List<string>? TryReadArray(byte[] b, int o, Func<string, bool> valid)
    {
        var count = BitConverter.ToInt32(b, o);
        if (count <= 0 || count > MaxEntries) return null;
        o += 4;

        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var s = ReadString(b, ref o);
            if (string.IsNullOrEmpty(s) || !valid(s)) return null;
            list.Add(s);
        }
        return list;
    }

    /// <summary>
    /// Reads a count followed by that many (name, ticks) pairs, but only accepts
    /// the result if it ends on a real property header — otherwise a stray
    /// integer in the metadata would pass as a plausible count.
    /// </summary>
    private static List<(string Key, long Ticks)>? TryReadEntries(byte[] b, int o)
    {
        var count = BitConverter.ToInt32(b, o);
        if (count <= 0 || count > MaxEntries) return null;
        o += 4;

        var entries = new List<(string, long)>(count);
        for (var i = 0; i < count; i++)
        {
            var key = ReadString(b, ref o);
            if (key is null || o + 8 > b.Length) return null;
            entries.Add((key, BitConverter.ToInt64(b, o)));
            o += 8;
        }

        // the next property: a name, then a type ending in "Property"
        if (ReadString(b, ref o) is null) return null;
        if (ReadString(b, ref o) is not string type || !type.EndsWith("Property")) return null;

        return entries;
    }

    /// <summary>Reads an FString and advances. Null when the bytes are not one.</summary>
    private static string? ReadString(byte[] b, ref int o)
    {
        if (o + 4 > b.Length) return null;
        var n = BitConverter.ToInt32(b, o);

        if (n == 0) { o += 4; return ""; }
        if (n < 0)
        {
            var len = -2 * n;
            if (len > 1024 || o + 4 + len > b.Length) return null;
            var s = Encoding.Unicode.GetString(b, o + 4, len - 2);
            o += 4 + len;
            return s;
        }

        if (n > 1024 || o + 4 + n > b.Length) return null;
        if (b[o + 4 + n - 1] != 0) return null;            // must be terminated
        var t = Encoding.UTF8.GetString(b, o + 4, n - 1);
        o += 4 + n;
        return t;
    }

    private static int Find(byte[] haystack, string needle)
    {
        var pat = Encoding.UTF8.GetBytes(needle);
        for (var i = 0; i <= haystack.Length - pat.Length; i++)
        {
            var hit = true;
            for (var j = 0; j < pat.Length; j++)
                if (haystack[i + j] != pat[j]) { hit = false; break; }
            if (hit) return i;
        }
        return -1;
    }
}

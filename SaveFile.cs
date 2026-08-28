using System.IO;
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

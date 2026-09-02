using System.IO;

namespace Mhodume;

/// <summary>
/// Resolves the one folder the app and the mod both use — the crux of the Linux
/// port.
///
/// On Windows the mod runs in the same process space as everything else, so the
/// config lives at <c>%LOCALAPPDATA%\Mhodume</c> and the app writes there
/// directly. On Linux the game runs through Proton, which is Wine: the mod sees
/// a fake <c>C:</c> drive inside the game's Proton prefix, and
/// <c>%LOCALAPPDATA%</c> for it means
/// <c>&lt;prefix&gt;/drive_c/users/steamuser/AppData/Local/Mhodume</c>. A native
/// Linux app writing to <c>~/.local/share</c> would write somewhere the mod
/// never looks. So on Linux we target the prefix path instead.
///
/// The prefix is Steam's compatdata folder for VHOLUME's app id. Steam can put
/// its library on any disk, so we search the known library roots rather than
/// assume the default one.
/// </summary>
public static class MhodumePaths
{
    /// <summary>VHOLUME's Steam app id — names its Proton prefix.</summary>
    public const string AppId = "4131730";

    /// <summary>
    /// The Local-AppData base both the app and the mod resolve to: on Windows the
    /// real one, on Linux the one inside VHOLUME's Proton prefix. Everything the
    /// game keeps under Local-AppData — the mod's Mhodume folder AND the game's
    /// own VHOLUME/Saved (save file, ghosts) — hangs off this.
    /// </summary>
    public static string LocalAppDataBase { get; } = ResolveBase();

    /// <summary>The Mhodume data folder both sides agree on. Created if missing.</summary>
    public static string RootDir { get; } = Resolve();

    private static string Resolve()
    {
        var root = Path.Combine(LocalAppDataBase, "Mhodume");
        try { Directory.CreateDirectory(root); } catch { /* first write will surface it */ }
        return root;
    }

    private static string ResolveBase()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Linux: the Local-AppData as the mod sees it from inside the Proton
        // prefix. Falls back to the native share dir if no prefix is found — the
        // app still runs and edits config, it just cannot reach a game that has
        // never been launched through Proton yet.
        var prefix = FindProtonPrefix();
        if (prefix is not null)
            return Path.Combine(prefix, "drive_c", "users", "steamuser", "AppData", "Local");

        var share = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(share))
            share = Path.Combine(Home(), ".local", "share");
        return share;
    }

    /// <summary>Whether we resolved to a real Proton prefix (so config reaches the game).</summary>
    public static bool BridgedToGame { get; private set; }

    /// <summary>
    /// Finds VHOLUME's Proton prefix by walking every Steam library's
    /// <c>compatdata/&lt;appid&gt;/pfx</c>. Returns null if the game was never
    /// run through Proton.
    /// </summary>
    private static string? FindProtonPrefix()
    {
        foreach (var lib in SteamLibraries())
        {
            var pfx = Path.Combine(lib, "steamapps", "compatdata", AppId, "pfx");
            if (Directory.Exists(pfx))
            {
                BridgedToGame = true;
                return pfx;
            }
        }
        return null;
    }

    /// <summary>
    /// Every Steam library folder on the machine. The main one is under the
    /// Steam install; extra ones are listed in libraryfolders.vdf, which we read
    /// leniently — we only need the "path" values out of it.
    /// </summary>
    private static IEnumerable<string> SteamLibraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var steam in SteamRoots())
        {
            if (seen.Add(steam)) yield return steam;

            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            string text;
            try { text = File.ReadAllText(vdf); } catch { continue; }

            foreach (var path in ExtractPaths(text))
                if (Directory.Exists(path) && seen.Add(path))
                    yield return path;
        }
    }

    /// <summary>The usual places a Steam install lives on Linux.</summary>
    private static IEnumerable<string> SteamRoots()
    {
        var home = Home();
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "root");
        // Flatpak Steam keeps its own home.
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam",
            ".local", "share", "Steam");
    }

    /// <summary>
    /// Pulls the <c>"path"  "…"</c> values out of libraryfolders.vdf without a
    /// full VDF parser — that file's only shape we care about is those strings.
    /// </summary>
    private static IEnumerable<string> ExtractPaths(string vdf)
    {
        const string key = "\"path\"";
        int i = 0;
        while ((i = vdf.IndexOf(key, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            i += key.Length;
            int open = vdf.IndexOf('"', i);
            if (open < 0) yield break;
            int close = vdf.IndexOf('"', open + 1);
            if (close < 0) yield break;
            yield return vdf[(open + 1)..close].Replace("\\\\", "/");
            i = close + 1;
        }
    }

    private static string Home() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}

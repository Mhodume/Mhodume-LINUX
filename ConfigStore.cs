using System.IO;
using System.Text.Json;
using Avalonia.Threading;
using Avalonia.Media;

namespace Mhodume;

/// <summary>
/// Reads and writes the mod configuration and its profiles.
///
/// crosshair.json is the live file: the Lua module re-reads it three times a
/// second. Writes are therefore debounced (so dragging a slider does not
/// hammer the disk) and atomic (so the game can never read a half-written
/// file).
/// </summary>
public class ConfigStore
{
    public static readonly string RootDir = MhodumePaths.RootDir;

    public static readonly string LivePath    = Path.Combine(RootDir, "crosshair.json");
    public static readonly string ProfilesDir = Path.Combine(RootDir, "profiles");
    private static readonly string StatePath  = Path.Combine(RootDir, "app-state.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly DispatcherTimer _debounce;
    private ModConfig? _pending;

    public event Action<string>? Status;

    /// <summary>Names the app has been shipped under, newest first.</summary>
    private static readonly string[] FormerNames = { "Modhume", "VholumeCrosshair" };

    /// <summary>
    /// Brings settings over from a name the app used to have, so a rename does
    /// not look like a reset.
    ///
    /// The files are copied, not moved: an old folder may also hold the exe
    /// that is running right now, and moving it out from under itself fails.
    /// </summary>
    static ConfigStore()
    {
        try
        {
            if (File.Exists(LivePath)) return;

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string? old = null;
            foreach (var name in FormerNames)
            {
                var candidate = Path.Combine(local, name);
                if (File.Exists(Path.Combine(candidate, "crosshair.json"))) { old = candidate; break; }
            }
            if (old is null) return;

            Directory.CreateDirectory(RootDir);
            foreach (var name in new[] { "crosshair.json", "app-state.json" })
            {
                var from = Path.Combine(old, name);
                if (File.Exists(from)) File.Copy(from, Path.Combine(RootDir, name));
            }

            var oldProfiles = Path.Combine(old, "profiles");
            if (Directory.Exists(oldProfiles))
            {
                Directory.CreateDirectory(ProfilesDir);
                foreach (var f in Directory.EnumerateFiles(oldProfiles, "*.json"))
                    File.Copy(f, Path.Combine(ProfilesDir, Path.GetFileName(f)));
            }
        }
        catch
        {
            // Settings are not worth refusing to start over; defaults will do.
        }
    }

    public ConfigStore()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(ProfilesDir);

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            if (_pending is null) return;
            var cfg = _pending;
            _pending = null;
            WriteLive(cfg);
        };
    }

    // -------------------------------------------------------------- live file
    /// <summary>Schedules a debounced write of the live config.</summary>
    public void QueueLiveWrite(ModConfig cfg)
    {
        _pending = cfg;
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Writes immediately, bypassing the debounce.</summary>
    public void FlushLive(ModConfig cfg)
    {
        _debounce.Stop();
        _pending = null;
        WriteLive(cfg);
    }

    private void WriteLive(ModConfig cfg)
    {
        try
        {
            WriteAtomic(LivePath, JsonSerializer.Serialize(cfg, Options));
            Status?.Invoke("Applied in game");
        }
        catch (Exception ex)
        {
            Status?.Invoke("Could not write config: " + ex.Message);
        }
    }

    /// <summary>
    /// Writes through a temporary file then replaces, so a reader never sees
    /// truncated content.
    /// </summary>
    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    public ModConfig LoadLive()
    {
        try
        {
            if (File.Exists(LivePath))
            {
                var cfg = Deserialize(File.ReadAllText(LivePath));
                if (cfg is not null) return cfg;
            }
        }
        catch (Exception ex)
        {
            Status?.Invoke("Config unreadable, using defaults: " + ex.Message);
        }
        return new ModConfig();
    }

    /// <summary>
    /// Accepts both the nested layout and the older flat crosshair-only file,
    /// so configs written by earlier versions still load.
    /// </summary>
    private static ModConfig? Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("crosshair", out _))
            return JsonSerializer.Deserialize<ModConfig>(json);

        var crosshair = JsonSerializer.Deserialize<CrosshairConfig>(json);
        return crosshair is null ? null : new ModConfig { Crosshair = crosshair };
    }

    // -------------------------------------------------------------- trajectory
    public static readonly string TrajectoryPath = Path.Combine(RootDir, "trajectory.txt");

    // WriteTrajectory / ClearTrajectory come back with the Trajectory page port.

    /// <summary>What the mod is currently reporting about the running game.</summary>
    public record GameStatus(string? Map, bool Training, bool LapTainted);

    /// <summary>
    /// Reads the status the mod publishes each time something changes. Null
    /// map when the game is not running or has not reached a level yet.
    /// </summary>
    public static GameStatus ReadStatus()
    {
        string? map = null;
        bool training = false, tainted = false;
        try
        {
            var path = Path.Combine(RootDir, "status.txt");
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("map ", StringComparison.Ordinal))
                        map = line[4..].Trim();
                    else if (line.StartsWith("training ", StringComparison.Ordinal))
                        training = line[9..].Trim() == "true";
                    else if (line.StartsWith("tainted ", StringComparison.Ordinal))
                        tainted = line[8..].Trim() == "true";
                }
            }
        }
        catch { /* the mod may be mid-write; try again next tick */ }
        return new GameStatus(map, training, tainted);
    }

    public static string? ReadCurrentMap() => ReadStatus().Map;

    public void ClearTrajectory()
    {
        try
        {
            if (File.Exists(TrajectoryPath)) File.Delete(TrajectoryPath);
        }
        catch { /* nothing useful to do */ }
    }

    // ---------------------------------------------------------------- profiles
    public IEnumerable<string> ListProfiles()
    {
        if (!Directory.Exists(ProfilesDir)) yield break;
        foreach (var f in Directory.EnumerateFiles(ProfilesDir, "*.json").OrderBy(f => f))
            yield return Path.GetFileNameWithoutExtension(f);
    }

    private static string ProfilePath(string name) =>
        Path.Combine(ProfilesDir, Sanitize(name) + ".json");

    public static string Sanitize(string name)
    {
        var clean = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "unnamed" : clean;
    }

    public void SaveProfile(string name, ModConfig cfg)
    {
        try
        {
            WriteAtomic(ProfilePath(name), JsonSerializer.Serialize(cfg, Options));
            Status?.Invoke($"Profile “{name}” saved");
        }
        catch (Exception ex)
        {
            Status?.Invoke("Could not save profile: " + ex.Message);
        }
    }

    public ModConfig? LoadProfile(string name)
    {
        try
        {
            var p = ProfilePath(name);
            return File.Exists(p) ? Deserialize(File.ReadAllText(p)) : null;
        }
        catch (Exception ex)
        {
            Status?.Invoke($"Profile “{name}” unreadable: " + ex.Message);
            return null;
        }
    }

    public void DeleteProfile(string name)
    {
        try
        {
            var p = ProfilePath(name);
            if (File.Exists(p)) File.Delete(p);
            Status?.Invoke($"Profile “{name}” deleted");
        }
        catch (Exception ex)
        {
            Status?.Invoke("Could not delete profile: " + ex.Message);
        }
    }

    public bool ProfileExists(string name) => File.Exists(ProfilePath(name));

    // --------------------------------------------------------------- app state
    private record AppState(string? LastProfile);

    public string? LoadLastProfile()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath))?.LastProfile
                : null;
        }
        catch { return null; }
    }

    public void SaveLastProfile(string? name)
    {
        try { WriteAtomic(StatePath, JsonSerializer.Serialize(new AppState(name), Options)); }
        catch { /* not worth surfacing */ }
    }

    /// <summary>Creates a few starter profiles on first run.</summary>
    public void SeedDefaultProfiles()
    {
        if (ListProfiles().Any()) return;

        var presets = new (string Name, Action<ModConfig> Setup)[]
        {
            ("Classic green", _ => { }),
            ("Red dot", c =>
            {
                c.Crosshair.Shape = "dot";
                c.Crosshair.Dot = 4;
                c.Crosshair.MainColor = Color.FromRgb(255, 60, 60);
            }),
            ("Cyan circle", c =>
            {
                c.Crosshair.Shape = "circle_dot";
                c.Crosshair.Radius = 14;
                c.Crosshair.Thickness = 2;
                c.Crosshair.Dot = 2;
                c.Crosshair.MainColor = Color.FromRgb(0, 230, 255);
            }),
            ("Thin cross", c =>
            {
                c.Crosshair.Shape = "cross";
                c.Crosshair.Gap = 5;
                c.Crosshair.Length = 10;
                c.Crosshair.Thickness = 1;
                c.Crosshair.MainColor = Color.FromRgb(255, 255, 255);
            }),
        };

        foreach (var (name, setup) in presets)
        {
            var cfg = new ModConfig();
            setup(cfg);
            SaveProfile(name, cfg);
        }
    }
}

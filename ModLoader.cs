using System.IO;

namespace Mhodume;

/// <summary>
/// Enables or disables the UE4SS loader by renaming its proxy DLL.
///
/// VHOLUME detects the injection itself: with UE4SS loaded, the game marks the
/// session as untrusted and refuses to validate runs — even runs where the mod
/// changed nothing at all. That is legitimate anti-cheat on a game with online
/// leaderboards, so the answer is a clean switch between "modded" and "timing
/// real runs", not a way around it.
/// </summary>
public static class ModLoader
{
    private const string LoaderName = "dwmapi.dll";
    private const string DisabledName = "dwmapi.dll.off";

    public enum State { Enabled, Disabled, NotInstalled }

    public static string? BinariesDir { get; set; }

    private static string? Path(string name) =>
        BinariesDir is null ? null : System.IO.Path.Combine(BinariesDir, name);

    public static State Current
    {
        get
        {
            var on = Path(LoaderName);
            var off = Path(DisabledName);
            if (on is not null && File.Exists(on)) return State.Enabled;
            if (off is not null && File.Exists(off)) return State.Disabled;
            return State.NotInstalled;
        }
    }

    /// <summary>
    /// Flips the loader. Returns null on success, or a message explaining why
    /// it could not be done.
    /// </summary>
    public static string? Toggle()
    {
        var on = Path(LoaderName);
        var off = Path(DisabledName);
        if (on is null || off is null)
            return "VHOLUME install not found.";

        try
        {
            switch (Current)
            {
                case State.Enabled:
                    if (File.Exists(off)) File.Delete(off);
                    File.Move(on, off);
                    return null;

                case State.Disabled:
                    if (File.Exists(on)) File.Delete(on);
                    File.Move(off, on);
                    return null;

                default:
                    return "UE4SS is not installed in this game folder.";
            }
        }
        catch (IOException)
        {
            return "Could not rename the loader — close VHOLUME first.";
        }
        catch (UnauthorizedAccessException)
        {
            return "Access denied writing to the game folder.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

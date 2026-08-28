using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Mhodume;

public partial class ProfilesPage : UserControl
{
    private ConfigStore? _store;
    private Func<ModConfig>? _current;

    /// <summary>Raised when the user picks a profile that should become active.</summary>
    public event Action<ModConfig, string>? ProfileLoaded;

    public ProfilesPage() => InitializeComponent();


    /// <summary>Wires the page to the store and to the currently edited config.</summary>
    public void Initialize(ConfigStore store, Func<ModConfig> currentConfig)
    {
        _store = store;
        _current = currentConfig;
        Refresh(_store.LoadLastProfile());
    }

    public void Refresh(string? select = null)
    {
        if (_store is null) return;

        // The selection is restored with nobody listening. Subscribing first
        // and selecting after means restoring the selection raises the event,
        // and the handler applies the profile - so simply starting the app
        // wrote the last profile over whatever settings were in use.
        var target = select ?? ProfileList.SelectedItem as string;
        ProfileList.SelectionChanged -= ProfileList_SelectionChanged;

        var names = _store.ListProfiles().ToList();
        ProfileList.ItemsSource = names;

        if (target is not null && names.Contains(target))
            ProfileList.SelectedItem = target;

        ProfileList.SelectionChanged += ProfileList_SelectionChanged;
    }

    private void ProfileList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_store is null || ProfileList.SelectedItem is not string name) return;
        var cfg = _store.LoadProfile(name);
        if (cfg is null) return;

        ShowDetails(name, cfg);
        ProfileLoaded?.Invoke(cfg, name);
    }

    private void ShowDetails(string name, ModConfig cfg)
    {
        DetailTitle.Text = name;

        var c = cfg.Crosshair;
        var shape = c.Shape switch
        {
            "cross"      => "cross",
            "cross_dot"  => "cross with centre dot",
            "dot"        => "dot only",
            "tcross"     => "T cross",
            "circle"     => "circle",
            "circle_dot" => "circle with centre dot",
            _            => c.Shape,
        };

        var col = c.MainColor;
        var hud = cfg.Hud.Manage
            ? $"speedometer {OnOff(cfg.Hud.ShowSpeedometer)}, timer {OnOff(cfg.Hud.ShowTimer)}, " +
              $"splits {OnOff(cfg.Hud.ShowCheckpointTime)}"
            : "left to the game's own options";

        var freecam = cfg.Freecam.Enabled
            ? $"on, {cfg.Freecam.Key}, {cfg.Freecam.Speed:0} cm/s"
            : "off";

        DetailBody.Text =
            $"Crosshair — {shape}, {c.Thickness:0} px thick, {c.Gap:0} px gap, " +
            $"#{col.R:X2}{col.G:X2}{col.B:X2} at {c.OpacityPercent:0} % opacity." +
            (c.Outline ? $" Outlined ({c.OutlineThickness:0} px)." : " No outline.") +
            (c.Tilt ? $" Follows camera tilt at {c.TiltFactor:0.00}×." : " Fixed, no tilt.") +
            $"\n\nHUD — {hud}." +
            $"\n\nFreecam — {freecam}.";
    }

    private static string OnOff(bool b) => b ? "on" : "off";

    // ------------------------------------------------------------------ actions
    private void New_Click(object? sender, RoutedEventArgs e)
    {
        if (_store is null) return;
        var name = PromptName("New profile", "Unnamed");
        if (name is null) return;
        _store.SaveProfile(name, new ModConfig());
        Refresh(ConfigStore.Sanitize(name));
    }

    private void Duplicate_Click(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _current is null) return;
        var source = ProfileList.SelectedItem as string ?? "Profile";
        var name = PromptName("Duplicate profile", source + " copy");
        if (name is null) return;
        _store.SaveProfile(name, _current().Clone());
        Refresh(ConfigStore.Sanitize(name));
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (_store is null || _current is null) return;

        if (ProfileList.SelectedItem is string name)
        {
            _store.SaveProfile(name, _current());
            ShowDetails(name, _current());
        }
        else
        {
            var newName = PromptName("Save as", "My crosshair");
            if (newName is null) return;
            _store.SaveProfile(newName, _current());
            Refresh(ConfigStore.Sanitize(newName));
        }
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_store is null || ProfileList.SelectedItem is not string name) return;

        // TODO(linux): confirmation dialog. Avalonia ships no MessageBox and the
        // guide forbids inventing one, so the delete proceeds on the click.
        _store.DeleteProfile(name);
        Refresh();
        DetailTitle.Text = "No profile selected";
        DetailBody.Text = "Pick a profile on the left to see what it contains.";
    }

    private void Reveal_Click(object? sender, RoutedEventArgs e)
    {
        // TODO(linux): open ConfigStore.ProfilesDir in the file manager. The WPF
        // build shelled out with Process.Start(UseShellExecute), which is
        // Windows shell behaviour and out of scope for this port.
    }

    /// <summary>
    /// Names a new profile.
    ///
    /// The WPF build popped a small modal Window here. Avalonia's dialogs are
    /// asynchronous and the guide forbids inventing a dialog library, so until a
    /// Linux prompt exists this takes the suggested name straight through — New,
    /// Duplicate and Save-as still create a profile, just without asking.
    /// </summary>
    private string? PromptName(string title, string initial)
    {
        // TODO(linux): replace with an inline Avalonia name prompt.
        var name = initial.Trim();
        if (string.IsNullOrWhiteSpace(name)) return null;

        // The WPF prompt asked before replacing an existing profile; without a
        // dialog it simply overwrites, which SaveProfile already does.
        return name;
    }
}

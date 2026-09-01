using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Mhodume;

public partial class TweaksPage : UserControl
{
    /// <summary>One entry in the level dropdown: what the mod loads, what you read.</summary>
    public record LevelItem(string Asset, string Display);

    public TweaksPage()
    {
        InitializeComponent();
        LevelBox.ItemsSource = MapNames.All
            .Select(m => new LevelItem(m.Asset, m.Display))
            .ToList();
    }

    /// <summary>
    /// Jumps straight to a chosen level through the mod's own goto, so it is
    /// recognised as where you asked to be — "stay on the level" does not fight
    /// it, unlike picking a level from the game's own menu.
    /// </summary>
    private void Go_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TweaksConfig cfg) return;
        if (LevelBox.SelectedItem is not LevelItem item)
        {
            GoSaid.Text = "Pick a level first.";
            return;
        }
        cfg.GotoLevel = item.Asset;
        cfg.GotoRequest++;
        GoSaid.Text = $"Going to {item.Display} — needs the game running.";
    }
}

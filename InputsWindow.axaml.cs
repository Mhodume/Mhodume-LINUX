using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Mhodume;

/// <summary>
/// The loaded run's keys, end to end. The pad in the game says what the ghost
/// is doing where you are standing; this says what it did everywhere else,
/// which is the half you cannot see while you are playing.
/// </summary>
public partial class InputsWindow : Window
{
    private TrajectoryConfig? _config;

    public InputsWindow()
    {
        InitializeComponent();
        Timeline.Hovered += ShowMoment;
        Timeline.Picked += WatchFrom;
    }


    /// <summary>
    /// The config the game reads. Given rather than fetched: the window asks
    /// for a viewing by writing into it, and the app is already watching it for
    /// changes to push out.
    /// </summary>
    public void Attach(TrajectoryConfig config)
    {
        _config = config;
        // Zero back means the camera sits where the runner's eyes were.
        EyesRadio.IsChecked = config.WatchBehind <= 0;
        BehindRadio.IsChecked = config.WatchBehind > 0;
    }

    /// <summary>Not called Show: Window already has one, and overloading it
    /// makes "show the run" and "show the window" the same word.</summary>
    public void Present(Trajectory traj, string label)
    {
        RunTitle.Text = string.IsNullOrWhiteSpace(label)
            ? MapNames.Display(traj.Map) : label;

        var seconds = traj.TimeMs / 1000.0;
        var changes = traj.Inputs.Count;
        RunDetail.Text = changes == 0
            ? "This run carries no keys. It was loaded before the app recorded them — load it again."
            : $"{changes} changes over {seconds:0.0} s" +
              (traj.CheckpointMs.Count > 0
                  ? $", {traj.CheckpointMs.Count} checkpoints marked"
                  : "");

        Timeline.Show(traj.Inputs, traj.CheckpointMs, seconds);
        Timeline.PixelsPerSecond = Zoom.Value;

        // Said once, plainly: the game refuses outside training mode, and
        // being flown through a level is not something a counted lap survives.
        Readout.Text = traj.Camera.Count == 0
            ? "This run has no camera track, so it cannot be watched — load it again."
            : "Click the lanes to watch from there. Needs training mode; taints the lap.";
    }

    private void ShowMoment(double? seconds, int mask)
    {
        if (seconds is not double at)
        {
            Readout.Text = "Point at the lanes to read a moment.";
            return;
        }

        var held = GhostInputs.Describe(mask);
        Readout.Text = $"{at,6:0.00} s   {(held.Length > 0 ? held : "nothing held")}";
    }

    // ------------------------------------------------------------- watching
    /// <summary>
    /// Asks the game to spectate from the moment clicked.
    ///
    /// The request is a counter, so clicking the same spot twice is two
    /// viewings rather than one that never restarts.
    /// </summary>
    private void WatchFrom(double seconds)
    {
        if (_config is null) return;
        _config.WatchFrom = seconds;
        _config.WatchRequest++;
        Timeline.MarkPicked(seconds);
        WatchState.Text = $"asked to watch from {seconds:0.00} s";
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        if (_config is null) return;
        _config.WatchFrom = -1;
        _config.WatchRequest++;
        Timeline.MarkPicked(null);
        WatchState.Text = "asked to stop";
    }

    /// <summary>
    /// Behind and above, in centimetres. Four metres back and one and a half up
    /// is roughly where the game's own camera sits, which is the point: from
    /// there you see the runner rather than see through them.
    /// </summary>
    private void Behind_Checked(object? sender, RoutedEventArgs e)
    {
        if (_config is null || BehindRadio.IsChecked != true) return;
        _config.WatchBehind = 400;
        _config.WatchAbove = 150;
    }

    private void Eyes_Checked(object? sender, RoutedEventArgs e)
    {
        if (_config is null || EyesRadio.IsChecked != true) return;
        _config.WatchBehind = 0;
        _config.WatchAbove = 0;
    }

    private void Zoom_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (Timeline is null) return;

        // Zooming about the middle of what is on screen, not about the start:
        // otherwise looking closely at something forty seconds in throws it off
        // the right-hand edge.
        var middle = Timeline.SecondsAt(Scroller.Offset.X + Scroller.Viewport.Width / 2);
        Timeline.PixelsPerSecond = e.NewValue;
        // Recentre after the timeline has re-measured at the new scale, so the
        // offset lands against the new extent (Avalonia has no synchronous
        // UpdateLayout; a Loaded-priority post runs once layout has happened).
        Dispatcher.UIThread.Post(() =>
            Scroller.Offset = Scroller.Offset.WithX(Timeline.XAt(middle) - Scroller.Viewport.Width / 2),
            DispatcherPriority.Loaded);
    }
}

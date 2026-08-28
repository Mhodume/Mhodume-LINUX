using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Mhodume;

/// <summary>
/// A recorded run's keys, laid out along time: one lane per key, filled where
/// it was held.
///
/// Drawn rather than composed out of controls. A long run holds a few hundred
/// held-spans per lane and the whole thing is redrawn on every zoom step, and
/// a few thousand Rectangles in a Canvas costs more to lay out than these cost
/// to paint.
///
/// This is the Avalonia counterpart of the Windows app's WPF FrameworkElement:
/// same maths, drawn through Avalonia's <see cref="DrawingContext"/>.
/// </summary>
public class InputTimeline : Control
{
    private const double LaneHeight = 26;
    private const double LaneGap = 4;
    private const double RulerHeight = 22;
    private const double LabelWidth = 52;

    private IReadOnlyList<InputMoment> _moments = Array.Empty<InputMoment>();
    private IReadOnlyList<int> _checkpoints = Array.Empty<int>();
    private double _duration;
    private double _pixelsPerSecond = 60;
    private double? _cursorSeconds;
    private double? _pickedSeconds;

    /// <summary>Marks the moment the game was last asked to watch from.</summary>
    public void MarkPicked(double? seconds)
    {
        _pickedSeconds = seconds;
        InvalidateVisual();
    }

    private readonly Typeface _mono = new(new FontFamily("Consolas"));

    /// <summary>Raised as the pointer moves over the lanes, with the time under it.</summary>
    public event Action<double?, int>? Hovered;

    /// <summary>Raised when a moment is clicked, with the time chosen.</summary>
    public event Action<double>? Picked;

    public InputTimeline()
    {
        ClipToBounds = true;
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public void Show(IReadOnlyList<InputMoment> moments, IReadOnlyList<int> checkpoints,
                     double durationSeconds)
    {
        _moments = moments;
        _checkpoints = checkpoints;
        _duration = durationSeconds > 0 ? durationSeconds : 1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set
        {
            _pixelsPerSecond = Math.Clamp(value, 8, 600);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Where the run's clock stands, in seconds, for the given x.</summary>
    public double SecondsAt(double x) =>
        Math.Clamp((x - LabelWidth) / _pixelsPerSecond, 0, _duration);

    public double XAt(double seconds) => LabelWidth + seconds * _pixelsPerSecond;

    protected override Size MeasureOverride(Size available)
    {
        var height = GhostInputs.Keys.Length * (LaneHeight + LaneGap) + RulerHeight;
        return new Size(LabelWidth + _duration * _pixelsPerSecond + 24, height);
    }

    // ------------------------------------------------------------- pointer
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var x = e.GetPosition(this).X;
        _cursorSeconds = SecondsAt(x);
        Hovered?.Invoke(_cursorSeconds, MaskAt(_cursorSeconds.Value));
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (_moments.Count == 0) return;
        Picked?.Invoke(SecondsAt(e.GetPosition(this).X));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _cursorSeconds = null;
        Hovered?.Invoke(null, 0);
        InvalidateVisual();
    }

    /// <summary>What was held at a moment. The track only stores changes.</summary>
    public int MaskAt(double seconds)
    {
        int mask = 0;
        foreach (var m in _moments)
        {
            if (m.Seconds > seconds) break;
            mask = m.Mask;
        }
        return mask;
    }

    // -------------------------------------------------------------- render
    public override void Render(DrawingContext dc)
    {
        var bg = Brush("#FF0B0C0F");
        var lane = Brush("#FF14161B");
        var line = Brush("#FF262B34");
        var edge = Brush("#FF3D4450");
        var accent = Brush("#FFFF4A1E");
        var muted = Brush("#FF7C8494");
        var text = Brush("#FFE8EBF0");

        var width = LabelWidth + _duration * _pixelsPerSecond;
        dc.DrawRectangle(bg, null, new Rect(0, 0, Math.Max(width + 24, Bounds.Width), Bounds.Height));

        if (_moments.Count == 0)
        {
            dc.DrawText(Text("No keys in this run — load it again to record them.", 12, muted),
                        new Point(LabelWidth, 12));
            return;
        }

        var pen = new Pen(line, 1);
        var edgePen = new Pen(edge, 1);
        var accentPen = new Pen(accent, 1);

        // ---- the ruler: a mark every second, a labelled one every five or ten
        double step = _pixelsPerSecond >= 40 ? 1 : _pixelsPerSecond >= 15 ? 5 : 10;
        double label = step * 5;
        var rulerY = GhostInputs.Keys.Length * (LaneHeight + LaneGap);

        for (double t = 0; t <= _duration; t += step)
        {
            var x = Math.Round(XAt(t)) + 0.5;
            var tall = Math.Abs(t % label) < 1e-6;
            dc.DrawLine(tall ? edgePen : pen,
                        new Point(x, rulerY), new Point(x, rulerY + (tall ? 8 : 4)));
            if (tall)
                dc.DrawText(Text(Clock(t), 11, muted), new Point(x + 3, rulerY + 6));
        }

        // ---- the lanes
        for (int i = 0; i < GhostInputs.Keys.Length; i++)
        {
            var key = GhostInputs.Keys[i];
            var y = i * (LaneHeight + LaneGap);

            dc.DrawRectangle(lane, null, new Rect(LabelWidth, y, Math.Max(width - LabelWidth, 0), LaneHeight));

            var name = Text(key.Short, 12, text);
            dc.DrawText(name, new Point(LabelWidth - name.Width - 10,
                                        y + (LaneHeight - name.Height) / 2));

            // Held spans: walk the changes, and close the open one at the end.
            double? openedAt = null;
            foreach (var m in _moments)
            {
                var held = GhostInputs.Held(m.Mask, key);
                if (held && openedAt is null) openedAt = m.Seconds;
                else if (!held && openedAt is not null)
                {
                    DrawSpan(dc, accent, openedAt.Value, m.Seconds, y);
                    openedAt = null;
                }
            }
            if (openedAt is not null) DrawSpan(dc, accent, openedAt.Value, _duration, y);
        }

        // ---- checkpoints, across every lane
        foreach (var ms in _checkpoints)
        {
            var x = Math.Round(XAt(ms / 1000.0)) + 0.5;
            dc.DrawLine(edgePen, new Point(x, 0), new Point(x, rulerY));
        }

        // ---- where the pointer is, and where it last asked to watch from
        if (_pickedSeconds is double picked)
        {
            var x = Math.Round(XAt(picked)) + 0.5;
            dc.DrawLine(new Pen(text, 1.5), new Point(x, 0), new Point(x, rulerY + 10));
            dc.DrawText(Text("WATCHING", 10, text), new Point(x + 4, rulerY + 10));
        }

        if (_cursorSeconds is double at)
        {
            var x = Math.Round(XAt(at)) + 0.5;
            dc.DrawLine(accentPen, new Point(x, 0), new Point(x, rulerY + 10));
        }
    }

    private void DrawSpan(DrawingContext dc, IBrush fill, double from, double to, double y)
    {
        var x1 = XAt(from);
        var x2 = XAt(to);
        // A tap can be shorter than a pixel at low zoom, and a tap that vanishes
        // is a tap the run did not make. One pixel is the floor.
        var w = Math.Max(x2 - x1, 1);
        dc.DrawRectangle(fill, null, new Rect(x1, y + 3, w, LaneHeight - 6));
    }

    private static string Clock(double seconds)
    {
        var m = (int)(seconds / 60);
        var s = seconds - m * 60;
        return m > 0 ? $"{m}:{s:00.#}" : $"{s:0.#}";
    }

    private FormattedText Text(string s, double size, IBrush brush) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _mono, size, brush);

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));
}

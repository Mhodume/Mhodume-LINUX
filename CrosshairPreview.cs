using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mhodume;

/// <summary>
/// Crosshair preview. Mirrors the Lua module geometry exactly
/// (buildSegments / drawShape in main.lua): same segments, same render order
/// (outline first), same centre dot. If one side changes, the other must follow.
///
/// This is the Avalonia counterpart of the Windows app's WPF FrameworkElement:
/// same maths, drawn through Avalonia's <see cref="DrawingContext"/>.
/// </summary>
public class CrosshairPreview : Control
{
    public static readonly StyledProperty<CrosshairConfig?> ConfigProperty =
        AvaloniaProperty.Register<CrosshairPreview, CrosshairConfig?>(nameof(Config));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<CrosshairPreview, double>(nameof(Zoom), 2.0);

    public static readonly StyledProperty<double> TiltAngleProperty =
        AvaloniaProperty.Register<CrosshairPreview, double>(nameof(TiltAngle), 0.0);

    public static readonly StyledProperty<string> BackdropProperty =
        AvaloniaProperty.Register<CrosshairPreview, string>(nameof(Backdrop), "dark");

    static CrosshairPreview()
    {
        AffectsRender<CrosshairPreview>(
            ConfigProperty, ZoomProperty, TiltAngleProperty, BackdropProperty);
    }

    public CrosshairConfig? Config
    {
        get => GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>Simulated roll in degrees, to preview the tilt behaviour.</summary>
    public double TiltAngle
    {
        get => GetValue(TiltAngleProperty);
        set => SetValue(TiltAngleProperty, value);
    }

    public string Backdrop
    {
        get => GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    /// <summary>Exact counterpart of buildSegments() on the Lua side.</summary>
    private static List<(double X1, double Y1, double X2, double Y2)> BuildSegments(CrosshairConfig c)
    {
        var segs = new List<(double, double, double, double)>();
        double g = c.Gap, l = c.Length;

        switch (c.Shape)
        {
            case "cross":
            case "cross_dot":
                segs.Add((0, -g, 0, -g - l));
                segs.Add((0, g, 0, g + l));
                segs.Add((-g, 0, -g - l, 0));
                segs.Add((g, 0, g + l, 0));
                break;

            case "tcross":
                segs.Add((0, g, 0, g + l));
                segs.Add((-g, 0, -g - l, 0));
                segs.Add((g, 0, g + l, 0));
                break;

            case "circle":
            case "circle_dot":
                int n = Math.Max(8, c.Segments);
                double r = c.Radius;
                for (int i = 0; i < n; i++)
                {
                    double a1 = i / (double)n * Math.PI * 2;
                    double a2 = (i + 1) / (double)n * Math.PI * 2;
                    segs.Add((Math.Cos(a1) * r, Math.Sin(a1) * r, Math.Cos(a2) * r, Math.Sin(a2) * r));
                }
                break;
        }
        return segs;
    }

    private static (double X, double Y) Rotate(double x, double y, double ang)
    {
        if (ang == 0) return (x, y);
        double c = Math.Cos(ang), s = Math.Sin(ang);
        return (x * c - y * s, x * s + y * c);
    }

    public override void Render(DrawingContext dc)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        DrawBackdrop(dc, w, h);

        var cfg = Config;
        if (cfg is null || !cfg.Enabled) return;

        double z = Math.Max(0.5, Zoom);
        double cx = w / 2 + cfg.OffsetX * z;
        double cy = h / 2 + cfg.OffsetY * z;
        double ang = cfg.Tilt ? TiltAngle * Math.PI / 180.0 * cfg.TiltFactor : 0;

        var segs = BuildSegments(cfg);

        // outline first, exactly like the Lua side
        if (cfg.Outline && cfg.OutlineThickness > 0)
        {
            var pen = new Pen(new SolidColorBrush(cfg.EdgeColor),
                              (cfg.Thickness + 2 * cfg.OutlineThickness) * z);
            foreach (var s in segs)
            {
                var (x1, y1) = Rotate(s.X1, s.Y1, ang);
                var (x2, y2) = Rotate(s.X2, s.Y2, ang);
                dc.DrawLine(pen, new Point(cx + x1 * z, cy + y1 * z), new Point(cx + x2 * z, cy + y2 * z));
            }
        }

        var mainPen = new Pen(new SolidColorBrush(cfg.MainColor), cfg.Thickness * z);
        foreach (var s in segs)
        {
            var (x1, y1) = Rotate(s.X1, s.Y1, ang);
            var (x2, y2) = Rotate(s.X2, s.Y2, ang);
            dc.DrawLine(mainPen, new Point(cx + x1 * z, cy + y1 * z), new Point(cx + x2 * z, cy + y2 * z));
        }

        // centre dot
        double d = cfg.Dot;
        bool wantsDot = d > 0 && cfg.Shape is "dot" or "cross_dot" or "circle_dot";
        if (wantsDot)
        {
            if (cfg.Outline && cfg.OutlineThickness > 0)
            {
                double o = cfg.OutlineThickness;
                dc.DrawRectangle(new SolidColorBrush(cfg.EdgeColor), null, new Rect(
                    cx - (d * 0.5 + o) * z, cy - (d * 0.5 + o) * z, (d + 2 * o) * z, (d + 2 * o) * z));
            }
            dc.DrawRectangle(new SolidColorBrush(cfg.MainColor), null, new Rect(
                cx - d * 0.5 * z, cy - d * 0.5 * z, d * z, d * z));
        }
    }

    private void DrawBackdrop(DrawingContext dc, double w, double h)
    {
        var area = new Rect(0, 0, w, h);

        switch (Backdrop)
        {
            case "light":
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xD2)), null, area);
                break;

            case "checker":
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x3A, 0x3E, 0x46)), null, area);
                var light = new SolidColorBrush(Color.FromRgb(0x4A, 0x4F, 0x59));
                const double s = 16;
                for (int y = 0; y * s < h; y++)
                for (int x = 0; x * s < w; x++)
                    if ((x + y) % 2 == 0)
                        dc.DrawRectangle(light, null, new Rect(x * s, y * s, s, s));
                break;
            }

            default: // dark: gradient roughly matching an in-game scene
            {
                var g = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(0x1B, 0x1E, 0x24), 0),
                        new GradientStop(Color.FromRgb(0x2E, 0x33, 0x3C), 1),
                    },
                };
                dc.DrawRectangle(g, null, area);
                break;
            }
        }

        // faint centre guides
        var guide = new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 1);
        dc.DrawLine(guide, new Point(0, h / 2), new Point(w, h / 2));
        dc.DrawLine(guide, new Point(w / 2, 0), new Point(w / 2, h));
    }
}

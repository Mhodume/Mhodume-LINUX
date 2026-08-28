using System.Text.Json.Serialization;
using Avalonia.Media;

namespace Mhodume;

/// <summary>
/// Crosshair appearance and behaviour. The JSON names match exactly the keys
/// read by the Lua module (mod/Scripts/main.lua) — changing one without the
/// other silently breaks the in-game rendering.
/// </summary>
public class CrosshairConfig : ObservableObject
{
    // ---------------------------------------------------------------- general
    private bool _enabled = true;
    private bool _hideNative = true;
    private string _shape = "cross_dot";

    [JsonPropertyName("enabled")]
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    [JsonPropertyName("hideNative")]
    public bool HideNative { get => _hideNative; set => Set(ref _hideNative, value); }

    [JsonPropertyName("shape")]
    public string Shape { get => _shape; set => Set(ref _shape, value); }

    // -------------------------------------------------------------- geometry
    private double _gap = 8;
    private double _length = 14;
    private double _thickness = 3;
    private double _dot = 3;
    private double _radius = 12;
    private int _segments = 32;
    private double _offsetX;
    private double _offsetY;

    [JsonPropertyName("gap")]
    public double Gap { get => _gap; set => Set(ref _gap, Math.Round(value)); }

    [JsonPropertyName("length")]
    public double Length { get => _length; set => Set(ref _length, Math.Round(value)); }

    [JsonPropertyName("thickness")]
    public double Thickness { get => _thickness; set => Set(ref _thickness, Math.Round(value)); }

    [JsonPropertyName("dot")]
    public double Dot { get => _dot; set => Set(ref _dot, Math.Round(value)); }

    [JsonPropertyName("radius")]
    public double Radius { get => _radius; set => Set(ref _radius, Math.Round(value)); }

    [JsonPropertyName("segments")]
    public int Segments { get => _segments; set => Set(ref _segments, value); }

    [JsonPropertyName("offsetX")]
    public double OffsetX { get => _offsetX; set => Set(ref _offsetX, Math.Round(value)); }

    [JsonPropertyName("offsetY")]
    public double OffsetY { get => _offsetY; set => Set(ref _offsetY, Math.Round(value)); }

    // ----------------------------------------------------------------- colour
    // The Lua side expects [R, G, B, A] in the 0..1 range.
    private double[] _color = { 0.0, 1.0, 0.4, 1.0 };
    private double[] _outlineColor = { 0.0, 0.0, 0.0, 1.0 };
    private bool _outline = true;
    private double _outlineThickness = 1;

    [JsonPropertyName("color")]
    public double[] Color
    {
        get => _color;
        set { _color = Normalize(value, 0, 1, 0.4, 1); OnPropertyChanged(); OnPropertyChanged(nameof(MainColor)); }
    }

    [JsonPropertyName("outlineColor")]
    public double[] OutlineColor
    {
        get => _outlineColor;
        set { _outlineColor = Normalize(value, 0, 0, 0, 1); OnPropertyChanged(); OnPropertyChanged(nameof(EdgeColor)); }
    }

    [JsonPropertyName("outline")]
    public bool Outline { get => _outline; set => Set(ref _outline, value); }

    [JsonPropertyName("outlineThickness")]
    public double OutlineThickness { get => _outlineThickness; set => Set(ref _outlineThickness, Math.Round(value)); }

    // --------------------------------------------------------------- movement
    private bool _tilt;
    private double _tiltFactor = 1.0;

    [JsonPropertyName("tilt")]
    public bool Tilt { get => _tilt; set => Set(ref _tilt, value); }

    [JsonPropertyName("tiltFactor")]
    public double TiltFactor { get => _tiltFactor; set => Set(ref _tiltFactor, Math.Round(value, 2)); }

    // ---------------------------------------------- bridges to WPF colours
    [JsonIgnore]
    public Color MainColor
    {
        get => ToColor(_color);
        set { _color = FromColor(value); OnPropertyChanged(); OnPropertyChanged(nameof(Color)); }
    }

    [JsonIgnore]
    public Color EdgeColor
    {
        get => ToColor(_outlineColor);
        set { _outlineColor = FromColor(value); OnPropertyChanged(); OnPropertyChanged(nameof(OutlineColor)); }
    }

    /// <summary>Main colour alpha, surfaced separately as a 0-100 % slider.</summary>
    [JsonIgnore]
    public double OpacityPercent
    {
        get => Math.Round(_color[3] * 100);
        set
        {
            _color[3] = Math.Clamp(value / 100.0, 0, 1);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(MainColor));
        }
    }

    // ---------------------------------------------------------------- helpers
    private static double[] Normalize(double[]? v, double r, double g, double b, double a)
        => v is { Length: >= 4 }
            ? new[] { Clamp01(v[0]), Clamp01(v[1]), Clamp01(v[2]), Clamp01(v[3]) }
            : new[] { r, g, b, a };

    private static double Clamp01(double d) => double.IsFinite(d) ? Math.Clamp(d, 0, 1) : 0;

    private static Color ToColor(double[] c) => Avalonia.Media.Color.FromArgb(
        (byte)Math.Round(Clamp01(c[3]) * 255),
        (byte)Math.Round(Clamp01(c[0]) * 255),
        (byte)Math.Round(Clamp01(c[1]) * 255),
        (byte)Math.Round(Clamp01(c[2]) * 255));

    private static double[] FromColor(Color c) =>
        new[] { c.R / 255.0, c.G / 255.0, c.B / 255.0, c.A / 255.0 };

    public CrosshairConfig Clone() => new()
    {
        Enabled = Enabled, HideNative = HideNative, Shape = Shape,
        Gap = Gap, Length = Length, Thickness = Thickness, Dot = Dot,
        Radius = Radius, Segments = Segments, OffsetX = OffsetX, OffsetY = OffsetY,
        Color = (double[])Color.Clone(), OutlineColor = (double[])OutlineColor.Clone(),
        Outline = Outline, OutlineThickness = OutlineThickness,
        Tilt = Tilt, TiltFactor = TiltFactor,
    };
}

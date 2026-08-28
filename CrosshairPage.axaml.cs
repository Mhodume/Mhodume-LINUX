using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Mhodume;

public partial class CrosshairPage : UserControl
{
    public record ShapeOption(string Key, string Label);

    private static readonly ShapeOption[] Shapes =
    {
        new("cross",      "Cross"),
        new("cross_dot",  "Cross + dot"),
        new("dot",        "Dot only"),
        new("tcross",     "T cross"),
        new("circle",     "Circle"),
        new("circle_dot", "Circle + dot"),
    };

    private static readonly string[] SwatchColors =
    {
        "#00E676", "#00E5FF", "#FFFFFF", "#FFEB3B",
        "#FF3D3D", "#FF00E5", "#FF9100", "#7C4DFF",
        "#000000", "#9E9E9E",
    };

    private CrosshairConfig? _config;
    private bool _settingShape;

    public CrosshairPage()
    {
        InitializeComponent();

        ShapeBox.ItemsSource = Shapes;
        BackdropBox.ItemsSource = new[] { "Dark", "Light", "Checker" };
        BackdropBox.SelectedIndex = 0;
        BuildSwatches();

        // Preview zoom follows the zoom slider.
        Preview.Bind(CrosshairPreview.ZoomProperty,
                     ZoomSlider.GetObservable(Slider.ValueProperty));

        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_config is not null) _config.PropertyChanged -= Config_PropertyChanged;
        _config = DataContext as CrosshairConfig;
        if (_config is not null) _config.PropertyChanged += Config_PropertyChanged;

        Preview.Config = _config;
        SyncShapeBox();
        UpdateHexBox();
        UpdateCircleVisibility();
        Preview.InvalidateVisual();
    }

    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Preview.InvalidateVisual();

        if (e.PropertyName == nameof(CrosshairConfig.Shape))
        {
            SyncShapeBox();
            UpdateCircleVisibility();
        }

        if (e.PropertyName is nameof(CrosshairConfig.Color) or nameof(CrosshairConfig.MainColor))
            UpdateHexBox();
    }

    // ------------------------------------------------------------------ shape
    private void SyncShapeBox()
    {
        if (_config is null) return;
        _settingShape = true;
        ShapeBox.SelectedItem = Array.Find(Shapes, s => s.Key == _config.Shape) ?? Shapes[0];
        _settingShape = false;
    }

    private void ShapeBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_settingShape || _config is null) return;
        if (ShapeBox.SelectedItem is ShapeOption opt) _config.Shape = opt.Key;
    }

    private void UpdateCircleVisibility()
    {
        CirclePanel.IsVisible = _config?.Shape is "circle" or "circle_dot";
    }

    private void BackdropBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        Preview.Backdrop = (BackdropBox.SelectedItem as string) switch
        {
            "Light"   => "light",
            "Checker" => "checker",
            _         => "dark",
        };
    }

    // ------------------------------------------------------------------ colour
    private void BuildSwatches()
    {
        foreach (var hex in SwatchColors)
        {
            var color = Color.Parse(hex);
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(color),
                BorderBrush = this.FindResource("Edge") as IBrush,
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            swatch.PointerReleased += (_, _) => ApplyColor(color);
            Swatches.Items.Add(swatch);
        }
    }

    private void ApplyColor(Color c)
    {
        if (_config is null) return;
        var alpha = _config.MainColor.A;      // keep the opacity the slider is at
        _config.MainColor = Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    private void UpdateHexBox()
    {
        if (_config is null) return;
        var c = _config.MainColor;
        var text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        if (!string.Equals(HexBox.Text, text, StringComparison.OrdinalIgnoreCase))
            HexBox.Text = text;
    }

    private void HexBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyHex();
    }

    private void HexBox_LostFocus(object? sender, RoutedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (_config is null) return;
        var text = (HexBox.Text ?? "").Trim();
        if (text.Length == 0) { UpdateHexBox(); return; }
        if (!text.StartsWith('#')) text = "#" + text;
        try { ApplyColor(Color.Parse(text)); }
        catch { UpdateHexBox(); }              // reject silently, show the real value back
    }
}

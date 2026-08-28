using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Mhodume;

public partial class SpeedPage : UserControl
{
    private static readonly string[] SwatchColors =
    {
        "#FFFFFF", "#00E676", "#00E5FF", "#FFEB3B",
        "#FF3D3D", "#FF9100", "#7C4DFF", "#9E9E9E",
    };

    private SpeedConfig? _config;

    public SpeedPage()
    {
        InitializeComponent();
        BuildSwatches();

        DataContextChanged += (_, _) => _config = DataContext as SpeedConfig;
    }

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
            ToolTip.SetTip(swatch, hex);
            swatch.PointerReleased += (_, _) =>
            {
                if (_config is not null) _config.TextColor = color;
            };
            Swatches.Items.Add(swatch);
        }
    }
}

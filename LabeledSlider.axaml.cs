using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mhodume;

/// <summary>
/// A slider with a caption and a live value readout — the shape every numeric
/// setting on the crosshair page takes. Keeps the page XAML to one line per
/// setting instead of a three-control block each time.
/// </summary>
public partial class LabeledSlider : UserControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<LabeledSlider, string>(nameof(Header), "");

    public static readonly StyledProperty<double> MinProperty =
        AvaloniaProperty.Register<LabeledSlider, double>(nameof(Min), 0);

    public static readonly StyledProperty<double> MaxProperty =
        AvaloniaProperty.Register<LabeledSlider, double>(nameof(Max), 100);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<LabeledSlider, double>(nameof(Step), 1);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<LabeledSlider, double>(
            nameof(Value), 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public LabeledSlider() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public double Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
    public double Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

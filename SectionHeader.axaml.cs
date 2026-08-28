using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mhodume;

/// <summary>
/// The slab that titles a group of settings: a hard rule top and bottom, an
/// accent tick, one word in mono. The Avalonia stand-in for the Windows app's
/// templated <c>SectionHeader</c> Label style.
/// </summary>
public partial class SectionHeader : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SectionHeader, string>(nameof(Text), "");

    public static readonly StyledProperty<bool> FirstProperty =
        AvaloniaProperty.Register<SectionHeader, bool>(nameof(First), false);

    public SectionHeader()
    {
        InitializeComponent();
        Margin = new Thickness(0, 28, 0, 16);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == FirstProperty)
                Margin = First ? new Thickness(0, 0, 0, 16) : new Thickness(0, 28, 0, 16);
        };
    }


    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The first header on a page has nothing above it to sit apart from.</summary>
    public bool First { get => GetValue(FirstProperty); set => SetValue(FirstProperty, value); }
}

using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Mhodume;

/// <summary>
/// Draws a level's own name while the bound value stays the asset name, which
/// is what the save file, the ghost folder and the mod all key by. Combo boxes
/// hold the key and show the name.
/// </summary>
public class MapNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        MapNames.Display(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

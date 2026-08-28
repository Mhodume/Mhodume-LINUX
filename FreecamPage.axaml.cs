using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mhodume;

public partial class FreecamPage : UserControl
{
    /// <summary>
    /// Keys the mod binds for the freecam toggle. The Lua side registers all of
    /// them at load — UE4SS binds cannot be removed once set — and ignores any
    /// press that is not the configured one. So this list must stay in step
    /// with FREECAM_KEYS in main.lua.
    ///
    /// F6 and F7 are deliberately absent: the mod uses them for diagnostics and
    /// for training mode. F8 used to be here too, for a config reload that has
    /// not been needed since the file started being polled.
    /// </summary>
    public record KeyOption(string Code, string Label);

    private static readonly KeyOption[] Keys =
    {
        new("F1",  "F1"),
        new("F2",  "F2"),
        new("F3",  "F3"),
        new("F4",  "F4"),
        new("F5",  "F5"),
        new("F9",  "F9"),
        new("F11", "F11"),
        new("F12", "F12"),
        new("INS", "Insert"),
        new("HOME", "Home"),
        new("END", "End"),
        new("PAGE_UP", "Page Up"),
        new("PAGE_DOWN", "Page Down"),
        new("PAUSE", "Pause"),
        new("SCROLL_LOCK", "Scroll Lock"),
    };

    private FreecamConfig? _config;
    private bool _settingKey;

    public FreecamPage()
    {
        InitializeComponent();
        KeyBox.ItemsSource = Keys;
        DataContextChanged += OnDataContextChanged;
    }


    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_config is not null) _config.PropertyChanged -= Config_PropertyChanged;
        _config = DataContext as FreecamConfig;
        if (_config is not null) _config.PropertyChanged += Config_PropertyChanged;

        SyncKeyBox();
    }

    private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FreecamConfig.Key)) SyncKeyBox();
    }

    // Avalonia's ComboBox has no SelectedValue/SelectedValuePath, so the
    // Code <-> Key mapping the WPF binding did is done here by hand.
    private void SyncKeyBox()
    {
        if (_config is null) return;
        _settingKey = true;
        KeyBox.SelectedItem = Array.Find(Keys, k => k.Code == _config.Key) ?? Keys[0];
        _settingKey = false;
    }

    private void KeyBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_settingKey || _config is null) return;
        if (KeyBox.SelectedItem is KeyOption opt) _config.Key = opt.Code;
    }
}

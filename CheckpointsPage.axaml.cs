using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Mhodume;

public partial class CheckpointsPage : UserControl
{
    /// <summary>
    /// Keys the mod binds. One shared set — nothing claims a key of its own,
    /// which is what broke drawing when F10 collided with the game's console.
    /// F10 is absent on purpose.
    /// </summary>
    private static readonly string[] BindableKeys =
    {
        "F1", "F2", "F3", "F4", "F5", "F9", "F11", "F12",
        "INS", "HOME", "END", "PAGE_UP", "PAGE_DOWN", "PAUSE", "SCROLL_LOCK",
    };

    private static readonly string[] TextColors =
    {
        "#FFFFFF", "#00E676", "#00E5FF", "#FFEB3B",
        "#FF3D3D", "#FF9100", "#7C4DFF", "#9E9E9E",
    };

    private static readonly string[] MarkerColors =
    {
        "#FFD900", "#00E5FF", "#FFFFFF", "#FF9100",
        "#FF3D3D", "#00E676", "#FF00E5", "#7C4DFF",
    };

    private CheckpointsConfig? _config;
    private string? _map;
    private bool _loading;

    public CheckpointsPage()
    {
        InitializeComponent();

        KeyBox.ItemsSource = BindableKeys;
        BuildSwatches(TextSwatches, TextColors, c => { if (_config is not null) _config.TextColor = c; });
        BuildSwatches(MarkerSwatches, MarkerColors, c => { if (_config is not null) _config.MarkerBrush = c; });

        DataContextChanged += (_, _) =>
        {
            _config = DataContext as CheckpointsConfig;
            RefreshDrillBar();
        };

        // The list is read from disk, and the mod writes to the same files, so
        // it is refreshed on arrival rather than cached.
        this.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !e.GetNewValue<bool>()) return;
            RefreshMaps();
            RefreshDrillBar();
        };
    }


    private static void BuildSwatches(ItemsControl host, string[] hexes, Action<Color> pick)
    {
        foreach (var hex in hexes)
        {
            var color = Color.Parse(hex);
            var swatch = new Border
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(color),
                BorderBrush = Application.Current?.FindResource("Edge") as IBrush,
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(swatch, hex);
            swatch.PointerReleased += (_, _) => pick(color);
            host.Items.Add(swatch);
        }
    }

    // ------------------------------------------------------------- listing
    /// <summary>
    /// Fills the map list, preferring the one being played so the page opens on
    /// what you are working on rather than on whatever sorts first.
    /// </summary>
    private void RefreshMaps()
    {
        _loading = true;
        try
        {
            var maps = CheckpointStore.Maps().ToList();
            var playing = ConfigStore.ReadCurrentMap();
            if (playing is not null && !maps.Contains(playing, StringComparer.OrdinalIgnoreCase))
                maps.Insert(0, playing);

            MapBox.ItemsSource = maps;

            var wanted = playing ?? _map ?? maps.FirstOrDefault();
            if (wanted is not null && maps.Contains(wanted, StringComparer.OrdinalIgnoreCase))
                MapBox.SelectedItem = maps.First(m => string.Equals(m, wanted, StringComparison.OrdinalIgnoreCase));
            else
                MapBox.SelectedItem = maps.FirstOrDefault();

            _map = MapBox.SelectedItem as string;
        }
        finally
        {
            _loading = false;
        }
        RefreshSections();
    }

    private void RefreshSections()
    {
        var sections = _map is null
            ? new List<CheckpointSection>()
            : CheckpointStore.SectionsFor(_map);

        SectionList.ItemsSource = sections;

        var any = sections.Count > 0;
        EmptyNote.IsVisible = !any;
        ClearTimesButton.IsEnabled = any;
        DeleteMapButton.IsEnabled = any;
        ExportButton.IsEnabled = any;

        EmptyNote.Text = _map is null
            ? "No checkpoints anywhere yet. Play a level and press the capture key to drop your first."
            : $"Nothing on {_map} yet. Press the capture key in game to drop one, then another where the section ends.";
    }

    private void MapBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _map = MapBox.SelectedItem as string;
        RefreshSections();
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e) => RefreshMaps();

    // ------------------------------------------------------------ removing
    private void RemoveSection_Click(object? sender, RoutedEventArgs e)
    {
        if (_map is null || sender is not Button { Tag: int number }) return;

        // TODO(linux): confirmation dialog. On Windows this asked before removing
        // ("Both its checkpoints go, and the times recorded on it."); Avalonia has
        // no MessageBox, so the removal proceeds directly.
        CheckpointStore.DeleteSection(_map, number);
        RefreshSections();
    }

    private void ClearTimes_Click(object? sender, RoutedEventArgs e)
    {
        if (_map is null) return;
        // TODO(linux): confirmation dialog. On Windows this asked before clearing
        // ("The checkpoints stay where they are."); it proceeds directly here.
        CheckpointStore.ClearTimes(_map);
        RefreshSections();
    }

    private void DeleteMap_Click(object? sender, RoutedEventArgs e)
    {
        if (_map is null) return;
        // TODO(linux): confirmation dialog. On Windows this warned before deleting
        // ("Their times go with them."); it proceeds directly here.
        CheckpointStore.DeleteMap(_map);
        RefreshMaps();
    }

    // ------------------------------------------------------------- sharing
    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (_map is null) return;

        // TODO(linux): file-save picker. WPF used a Win32 SaveFileDialog; Avalonia's
        // StorageProvider is async and out of scope for this port, so there is no
        // path to write CheckpointStore.Export(_map, path) to yet.
        SetStatus("Exporting the whole map needs a file picker that isn't wired up on Linux yet.");
    }

    // ------------------------------------------------------------ drilling
    private void Drill_Click(object? sender, RoutedEventArgs e)
    {
        if (_config is null || sender is not Button { Tag: int number }) return;

        _config.TrainSection = number;
        RefreshDrillBar();
        SetStatus($"Drilling section {number}. Finish it and you go straight back.");
    }

    /// <summary>
    /// One trip to a section. The counter is bumped rather than a flag set, so
    /// pressing Go twice for the same section takes you twice.
    /// </summary>
    private void GoTo_Click(object? sender, RoutedEventArgs e)
    {
        if (_config is null || sender is not Button { Tag: int number }) return;

        _config.GoSection = number;
        _config.GoRequest += 1;
        SetStatus($"Going to section {number}. Needs training mode in game.");
    }

    private void StopDrill_Click(object? sender, RoutedEventArgs e)
    {
        if (_config is null) return;
        _config.TrainSection = 0;
        RefreshDrillBar();
    }

    private void RefreshDrillBar()
    {
        var section = _config?.TrainSection ?? 0;
        DrillBar.IsVisible = section > 0;
        DrillTitle.Text = $"Drilling section {section}";
    }

    private void ExportSection_Click(object? sender, RoutedEventArgs e)
    {
        if (_map is null || sender is not Button { Tag: int number }) return;

        // TODO(linux): file-save picker. WPF used a Win32 SaveFileDialog; Avalonia's
        // StorageProvider is async and out of scope for this port, so there is no
        // path to write CheckpointStore.ExportSection(_map, number, path) to yet.
        SetStatus($"Exporting section {number} needs a file picker that isn't wired up on Linux yet.");
    }

    /// <summary>
    /// Says what happened where it will be seen. The share drawer is folded
    /// away most of the time, so a note written only there goes unread.
    /// </summary>
    private void SetStatus(string text)
    {
        ShareNote.Text = text;
        ImportNote.Text = text;
        ImportNote.IsVisible = true;
    }

    private void Import_Click(object? sender, RoutedEventArgs e)
    {
        // Replacing renumbers the sections, which is why it also clears the
        // times: they would otherwise be attached to stretches that moved.
        //
        // TODO(linux): file-open picker and the replace/append confirmation.
        // WPF used a Win32 OpenFileDialog and a MessageBox; Avalonia's
        // StorageProvider is async and has no MessageBox, both out of scope for
        // this port. Once wired, the call is:
        //     var (maps, points) = CheckpointStore.Import(path, replace);
        //     RefreshMaps();
        SetStatus("Importing needs a file picker that isn't wired up on Linux yet.");
    }
}

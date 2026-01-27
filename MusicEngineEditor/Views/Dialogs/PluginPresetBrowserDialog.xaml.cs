// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Cross-plugin preset browser for managing presets across all loaded plugins.
/// </summary>
public partial class PluginPresetBrowserDialog : Window
{
    private readonly ObservableCollection<PluginInfo> _plugins = new();
    private readonly ObservableCollection<PresetInfo> _allPresets = new();
    private readonly ObservableCollection<PresetInfo> _filteredPresets = new();
    private readonly ObservableCollection<string> _allTags = new();
    private readonly HashSet<string> _selectedTags = new();

    private string _searchText = string.Empty;
    private string _categoryFilter = "All";
    private PluginInfo? _selectedPlugin;
    private PresetInfo? _selectedPreset;

    public PresetInfo? SelectedPresetResult { get; private set; }

    public event EventHandler<PresetInfo>? PresetLoadRequested;
    public event EventHandler<PresetInfo>? PresetPreviewRequested;

    public PluginPresetBrowserDialog()
    {
        InitializeComponent();

        PluginListBox.ItemsSource = _plugins;
        PresetListBox.ItemsSource = _filteredPresets;
        TagsPanel.ItemsSource = _allTags;

        LoadSampleData();
    }

    private void LoadSampleData()
    {
        // Sample plugins with presets
        var plugins = new[]
        {
            new PluginInfo
            {
                Id = "1",
                Name = "Serum",
                Vendor = "Xfer Records",
                Presets = new List<PresetInfo>
                {
                    new PresetInfo { Id = "1", Name = "Init", PluginId = "1", PluginName = "Serum", IsFactory = true, Category = "Init" },
                    new PresetInfo { Id = "2", Name = "Fat Bass", PluginId = "1", PluginName = "Serum", IsFactory = true, Category = "Bass", Tags = new List<string> { "Bass", "Heavy" } },
                    new PresetInfo { Id = "3", Name = "Supersaw Lead", PluginId = "1", PluginName = "Serum", IsFactory = true, Category = "Lead", Tags = new List<string> { "Lead", "Bright" } },
                    new PresetInfo { Id = "4", Name = "My Custom Pad", PluginId = "1", PluginName = "Serum", IsUser = true, Category = "Pad", Tags = new List<string> { "Pad", "Ambient" } }
                }
            },
            new PluginInfo
            {
                Id = "2",
                Name = "Massive X",
                Vendor = "Native Instruments",
                Presets = new List<PresetInfo>
                {
                    new PresetInfo { Id = "5", Name = "Default", PluginId = "2", PluginName = "Massive X", IsFactory = true, Category = "Init" },
                    new PresetInfo { Id = "6", Name = "Deep Sub", PluginId = "2", PluginName = "Massive X", IsFactory = true, Category = "Bass", Tags = new List<string> { "Bass", "Sub" } },
                    new PresetInfo { Id = "7", Name = "Pluck Arp", PluginId = "2", PluginName = "Massive X", IsFactory = true, Category = "Arp", Tags = new List<string> { "Arp", "Pluck" } }
                }
            },
            new PluginInfo
            {
                Id = "3",
                Name = "Vital",
                Vendor = "Matt Tytel",
                Presets = new List<PresetInfo>
                {
                    new PresetInfo { Id = "8", Name = "Basic", PluginId = "3", PluginName = "Vital", IsFactory = true, Category = "Init" },
                    new PresetInfo { Id = "9", Name = "Warm Pad", PluginId = "3", PluginName = "Vital", IsFactory = true, Category = "Pad", Tags = new List<string> { "Pad", "Warm" }, IsFavorite = true },
                    new PresetInfo { Id = "10", Name = "Growl Bass", PluginId = "3", PluginName = "Vital", IsUser = true, Category = "Bass", Tags = new List<string> { "Bass", "Growl" } }
                }
            }
        };

        foreach (var plugin in plugins)
        {
            _plugins.Add(plugin);
            foreach (var preset in plugin.Presets)
            {
                _allPresets.Add(preset);
                foreach (var tag in preset.Tags)
                {
                    if (!_allTags.Contains(tag))
                    {
                        _allTags.Add(tag);
                    }
                }
            }
        }

        PluginCountText.Text = _plugins.Count.ToString();
        ApplyFilters();
    }

    public void LoadPlugins(IEnumerable<PluginInfo> plugins)
    {
        _plugins.Clear();
        _allPresets.Clear();
        _allTags.Clear();

        foreach (var plugin in plugins)
        {
            _plugins.Add(plugin);
            foreach (var preset in plugin.Presets)
            {
                _allPresets.Add(preset);
                foreach (var tag in preset.Tags)
                {
                    if (!_allTags.Contains(tag))
                    {
                        _allTags.Add(tag);
                    }
                }
            }
        }

        PluginCountText.Text = _plugins.Count.ToString();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _filteredPresets.Clear();

        var filtered = _allPresets.AsEnumerable();

        // Plugin filter
        if (_selectedPlugin != null)
        {
            filtered = filtered.Where(p => p.PluginId == _selectedPlugin.Id);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(search) ||
                p.Category.ToLowerInvariant().Contains(search) ||
                p.Tags.Any(t => t.ToLowerInvariant().Contains(search)) ||
                p.PluginName.ToLowerInvariant().Contains(search));
        }

        // Category filter
        switch (_categoryFilter)
        {
            case "Favorites":
                filtered = filtered.Where(p => p.IsFavorite);
                break;
            case "Factory":
                filtered = filtered.Where(p => p.IsFactory);
                break;
            case "User":
                filtered = filtered.Where(p => p.IsUser);
                break;
        }

        // Tag filter
        if (_selectedTags.Count > 0)
        {
            filtered = filtered.Where(p => p.Tags.Any(t => _selectedTags.Contains(t)));
        }

        foreach (var preset in filtered.OrderBy(p => p.Name))
        {
            _filteredPresets.Add(preset);
        }

        PresetCountText.Text = $" - {_filteredPresets.Count} presets";
    }

    #region Event Handlers

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text ?? string.Empty;
        ApplyFilters();
    }

    private void PluginListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlugin = PluginListBox.SelectedItem as PluginInfo;
        ApplyFilters();
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilter.SelectedItem is ComboBoxItem item)
        {
            _categoryFilter = item.Content?.ToString() ?? "All";
            ApplyFilters();
        }
    }

    private void TagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.Content is string tag)
        {
            if (btn.IsChecked == true)
                _selectedTags.Add(tag);
            else
                _selectedTags.Remove(tag);

            ApplyFilters();
        }
    }

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Re-apply filters with new sort
        ApplyFilters();
    }

    private void PresetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPreset = PresetListBox.SelectedItem as PresetInfo;

        if (_selectedPreset != null)
        {
            SelectedPresetInfo.Text = $"{_selectedPreset.Name} - {_selectedPreset.PluginName}";

            if (PreviewCheckBox.IsChecked == true)
            {
                PresetPreviewRequested?.Invoke(this, _selectedPreset);
            }
        }
        else
        {
            SelectedPresetInfo.Text = "No preset selected";
        }
    }

    private void PresetListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_selectedPreset != null)
        {
            LoadPreset();
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PresetInfo preset)
        {
            preset.IsFavorite = !preset.IsFavorite;
        }
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPreset != null)
        {
            PresetPreviewRequested?.Invoke(this, _selectedPreset);
        }
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        LoadPreset();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void LoadPreset()
    {
        if (_selectedPreset == null)
        {
            MessageBox.Show("Please select a preset to load.", "No Preset Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedPresetResult = _selectedPreset;
        PresetLoadRequested?.Invoke(this, _selectedPreset);
        DialogResult = true;
        Close();
    }

    #endregion
}

#region Models

/// <summary>
/// Information about a plugin.
/// </summary>
public class PluginInfo : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _vendor = string.Empty;
    private List<PresetInfo> _presets = new();

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Vendor { get => _vendor; set { _vendor = value; OnPropertyChanged(); } }
    public List<PresetInfo> Presets { get => _presets; set { _presets = value; OnPropertyChanged(); OnPropertyChanged(nameof(PresetCount)); } }

    public int PresetCount => Presets.Count;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Information about a preset.
/// </summary>
public class PresetInfo : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _pluginId = string.Empty;
    private string _pluginName = string.Empty;
    private string _category = string.Empty;
    private List<string> _tags = new();
    private bool _isFactory;
    private bool _isUser;
    private bool _isFavorite;
    private string? _filePath;
    private DateTime _createdAt;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string PluginId { get => _pluginId; set { _pluginId = value; OnPropertyChanged(); } }
    public string PluginName { get => _pluginName; set { _pluginName = value; OnPropertyChanged(); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCategory)); } }
    public List<string> Tags { get => _tags; set { _tags = value; OnPropertyChanged(); } }
    public bool IsFactory { get => _isFactory; set { _isFactory = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeDisplay)); OnPropertyChanged(nameof(TypeBadgeBrush)); } }
    public bool IsUser { get => _isUser; set { _isUser = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeDisplay)); OnPropertyChanged(nameof(TypeBadgeBrush)); } }
    public bool IsFavorite { get => _isFavorite; set { _isFavorite = value; OnPropertyChanged(); OnPropertyChanged(nameof(FavoriteIcon)); OnPropertyChanged(nameof(FavoriteColor)); } }
    public string? FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }

    public bool HasCategory => !string.IsNullOrEmpty(Category);

    public string TypeDisplay => IsUser ? "User" : "Factory";

    public SolidColorBrush TypeBadgeBrush => IsUser
        ? new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF))
        : new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));

    public string FavoriteIcon => IsFavorite ? "*" : "*";

    public SolidColorBrush FavoriteColor => IsFavorite
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00))
        : new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Unified Preset Browser panel control.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using MusicEngine.Core;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Unified Preset Browser panel for browsing synths, effects, patterns, and grooves.
/// </summary>
public partial class PresetBrowserPanel : UserControl
{
    private PresetBrowserService? _service;
    private ObservableCollection<PresetInfo> _allPresets = new();
    private ObservableCollection<PresetInfo> _filteredPresets = new();
    private PresetInfo? _selectedPreset;
    private string _searchText = string.Empty;
    private PresetTargetType? _activeTypeFilter;
    private bool _showFavoritesOnly;
    private List<string> _selectedTags = new();
    private string? _selectedCategory;
    private DispatcherTimer? _searchDebounceTimer;
    private DispatcherTimer? _previewTimer;

    /// <summary>
    /// Event raised when a preset is selected for loading.
    /// </summary>
    public event EventHandler<PresetInfo>? PresetLoadRequested;

    /// <summary>
    /// Event raised when the panel requests to be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Creates a new PresetBrowserPanel instance.
    /// </summary>
    public PresetBrowserPanel()
    {
        InitializeComponent();
        Loaded += PresetBrowserPanel_Loaded;
    }

    private void PresetBrowserPanel_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeService();
        SetupTimers();
    }

    private void InitializeService()
    {
        try
        {
            _service = App.Services.GetService(typeof(PresetBrowserService)) as PresetBrowserService
                       ?? new PresetBrowserService();
        }
        catch
        {
            _service = new PresetBrowserService();
        }

        _service.PresetsChanged += OnPresetsChanged;
        _service.PreviewStateChanged += OnPreviewStateChanged;
        _service.PresetLoaded += OnPresetLoaded;

        LoadPresets();
    }

    private void SetupTimers()
    {
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounceTimer.Tick += (s, e) =>
        {
            _searchDebounceTimer.Stop();
            ApplyFilters();
        };

        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _previewTimer.Tick += (s, e) =>
        {
            if (_service?.IsPreviewPlaying == true)
            {
                PreviewProgressBar.Value = (PreviewProgressBar.Value + 5) % 100;
            }
            else
            {
                _previewTimer.Stop();
                PreviewProgressBar.Value = 0;
            }
        };
    }

    private void OnPresetsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(LoadPresets);
    }

    private void OnPreviewStateChanged(object? sender, bool isPlaying)
    {
        Dispatcher.Invoke(() =>
        {
            if (isPlaying)
            {
                PreviewPlayButton.Content = "Stop";
                _previewTimer?.Start();
            }
            else
            {
                PreviewPlayButton.Content = "Preview";
                _previewTimer?.Stop();
                PreviewProgressBar.Value = 0;
            }
        });
    }

    private void OnPresetLoaded(object? sender, PresetInfo preset)
    {
        PresetLoadRequested?.Invoke(this, preset);
    }

    private void LoadPresets()
    {
        if (_service == null) return;

        _allPresets.Clear();
        var searchResults = _service.Search(null);
        foreach (var preset in searchResults)
        {
            _allPresets.Add(preset);
        }

        ApplyFilters();
        UpdateTreeViewCategories();
    }

    private void UpdateTreeViewCategories()
    {
        // Update tree view with actual categories from presets
        // For now, we keep the static structure defined in XAML
        // In a future enhancement, this could be made dynamic
    }

    private void ApplyFilters()
    {
        if (_service == null) return;

        var options = new PresetFilterOptions
        {
            TargetType = _activeTypeFilter,
            FavoritesOnly = _showFavoritesOnly,
            Category = _selectedCategory,
            Tags = _selectedTags.Count > 0 ? _selectedTags : null,
            SortBy = PresetSortBy.Name,
            SortAscending = true
        };

        var results = _service.Search(_searchText, options);

        _filteredPresets.Clear();
        foreach (var preset in results)
        {
            _filteredPresets.Add(preset);
        }

        PresetListView.ItemsSource = _filteredPresets;
    }

    #region Search

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchTextBox.Text;
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Start();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        _searchText = string.Empty;
        ApplyFilters();
    }

    #endregion

    #region Filter Buttons

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;

        // Reset all filter buttons
        AllFilterButton.IsChecked = false;
        SynthsFilterButton.IsChecked = false;
        EffectsFilterButton.IsChecked = false;
        PatternsFilterButton.IsChecked = false;

        // Set the clicked button
        button.IsChecked = true;

        // Determine filter type
        if (button == AllFilterButton)
        {
            _activeTypeFilter = null;
        }
        else if (button == SynthsFilterButton)
        {
            _activeTypeFilter = PresetTargetType.Synth;
        }
        else if (button == EffectsFilterButton)
        {
            _activeTypeFilter = PresetTargetType.Effect;
        }
        else if (button == PatternsFilterButton)
        {
            // Patterns would need a separate type - for now treat as null
            _activeTypeFilter = null;
        }

        ApplyFilters();
    }

    private void FavoritesFilter_Click(object sender, RoutedEventArgs e)
    {
        _showFavoritesOnly = FavoritesFilterButton.IsChecked == true;
        ApplyFilters();
    }

    private void TagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;

        var tag = button.Content?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        if (button.IsChecked == true)
        {
            if (!_selectedTags.Contains(tag))
            {
                _selectedTags.Add(tag);
            }
        }
        else
        {
            _selectedTags.Remove(tag);
        }

        ApplyFilters();
    }

    #endregion

    #region Tree View

    private void CategoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item)
        {
            var header = GetTreeViewItemHeader(item);

            // Handle special categories
            if (header == "Favorites")
            {
                _showFavoritesOnly = true;
                _selectedCategory = null;
                _activeTypeFilter = null;
            }
            else if (header == "Synths")
            {
                _activeTypeFilter = PresetTargetType.Synth;
                _selectedCategory = null;
                _showFavoritesOnly = false;
            }
            else if (header == "Effects")
            {
                _activeTypeFilter = PresetTargetType.Effect;
                _selectedCategory = null;
                _showFavoritesOnly = false;
            }
            else if (header == "Patterns" || header == "Grooves")
            {
                _activeTypeFilter = null;
                _selectedCategory = header;
                _showFavoritesOnly = false;
            }
            else
            {
                // Sub-category selected
                _selectedCategory = header;
                _showFavoritesOnly = false;

                // Determine parent type
                var parent = item.Parent as TreeViewItem;
                if (parent != null)
                {
                    var parentHeader = GetTreeViewItemHeader(parent);
                    if (parentHeader == "Synths")
                    {
                        _activeTypeFilter = PresetTargetType.Synth;
                    }
                    else if (parentHeader == "Effects")
                    {
                        _activeTypeFilter = PresetTargetType.Effect;
                    }
                }
            }

            UpdateFilterButtonStates();
            ApplyFilters();
        }
    }

    private string GetTreeViewItemHeader(TreeViewItem item)
    {
        if (item.Header is string str)
            return str;

        // Handle cases where header might be a DataTemplate or other content
        return item.Header?.ToString() ?? string.Empty;
    }

    private void UpdateFilterButtonStates()
    {
        AllFilterButton.IsChecked = _activeTypeFilter == null && !_showFavoritesOnly;
        SynthsFilterButton.IsChecked = _activeTypeFilter == PresetTargetType.Synth;
        EffectsFilterButton.IsChecked = _activeTypeFilter == PresetTargetType.Effect;
        FavoritesFilterButton.IsChecked = _showFavoritesOnly;
    }

    #endregion

    #region Preset List

    private void PresetListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPreset = PresetListView.SelectedItem as PresetInfo;
        UpdateSelectedPresetInfo();
    }

    private void PresetListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        LoadSelectedPreset();
    }

    private void UpdateSelectedPresetInfo()
    {
        if (_selectedPreset != null)
        {
            SelectedPresetName.Text = _selectedPreset.Name;
            SelectedPresetAuthor.Text = !string.IsNullOrEmpty(_selectedPreset.Author)
                ? $"by {_selectedPreset.Author}"
                : string.Empty;
            SelectedPresetDescription.Text = _selectedPreset.Description;
            SelectedPresetDate.Text = _selectedPreset.ModifiedDateDisplay;

            LoadButton.IsEnabled = true;
            PreviewPlayButton.IsEnabled = true;
        }
        else
        {
            SelectedPresetName.Text = "No preset selected";
            SelectedPresetAuthor.Text = string.Empty;
            SelectedPresetDescription.Text = string.Empty;
            SelectedPresetDate.Text = string.Empty;

            LoadButton.IsEnabled = false;
            PreviewPlayButton.IsEnabled = false;
        }
    }

    #endregion

    #region Preview

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PresetInfo preset)
        {
            _ = _service?.PreviewPresetAsync(preset);
        }
    }

    private async void PreviewPlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_service == null || _selectedPreset == null) return;

        if (_service.IsPreviewPlaying)
        {
            _service.StopPreview();
        }
        else
        {
            await _service.PreviewPresetAsync(_selectedPreset);
        }
    }

    #endregion

    #region Favorite

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.DataContext is PresetInfo preset)
        {
            _service?.ToggleFavorite(preset);
        }
    }

    #endregion

    #region Load

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        LoadSelectedPreset();
    }

    private void LoadSelectedPreset()
    {
        if (_selectedPreset != null && _service != null)
        {
            _service.LoadPreset(_selectedPreset);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the preset list.
    /// </summary>
    public void Refresh()
    {
        _service?.ScanDefaultDirectories();
    }

    /// <summary>
    /// Sets the filter to show only synth presets.
    /// </summary>
    public void FilterBySynths()
    {
        _activeTypeFilter = PresetTargetType.Synth;
        UpdateFilterButtonStates();
        ApplyFilters();
    }

    /// <summary>
    /// Sets the filter to show only effect presets.
    /// </summary>
    public void FilterByEffects()
    {
        _activeTypeFilter = PresetTargetType.Effect;
        UpdateFilterButtonStates();
        ApplyFilters();
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    public void ClearFilters()
    {
        _activeTypeFilter = null;
        _showFavoritesOnly = false;
        _selectedCategory = null;
        _selectedTags.Clear();
        _searchText = string.Empty;
        SearchTextBox.Text = string.Empty;

        // Reset tag buttons
        foreach (var child in TagsPanel.Children)
        {
            if (child is ToggleButton tagButton)
            {
                tagButton.IsChecked = false;
            }
        }

        UpdateFilterButtonStates();
        ApplyFilters();
    }

    #endregion
}

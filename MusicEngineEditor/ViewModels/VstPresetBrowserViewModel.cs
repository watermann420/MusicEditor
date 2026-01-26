// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for VST preset browser.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents a preset item for display in the preset browser.
/// </summary>
public partial class PresetItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _category = "";

    [ObservableProperty]
    private string _author = "";

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private bool _isFactoryPreset;

    [ObservableProperty]
    private bool _isUserPreset;

    [ObservableProperty]
    private int _index = -1;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets whether this preset is from a file (as opposed to built-in).
    /// </summary>
    public bool IsFilePreset => !string.IsNullOrEmpty(FilePath);

    /// <summary>
    /// Gets the display type for the preset.
    /// </summary>
    public string TypeDisplay => IsFactoryPreset ? "Factory" : (IsUserPreset ? "User" : "Plugin");
}

/// <summary>
/// ViewModel for the VST Preset Browser dialog.
/// </summary>
public partial class VstPresetBrowserViewModel : ViewModelBase
{
    private readonly IVstPlugin _plugin;
    private readonly string _pluginName;
    private readonly string _userPresetsPath;

    [ObservableProperty]
    private ObservableCollection<PresetItemViewModel> _allPresets = new();

    [ObservableProperty]
    private ObservableCollection<PresetItemViewModel> _filteredPresets = new();

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private PresetItemViewModel? _selectedPreset;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private bool _showFactoryPresets = true;

    [ObservableProperty]
    private bool _showUserPresets = true;

    [ObservableProperty]
    private string _newPresetName = "";

    [ObservableProperty]
    private string _currentPresetName = "";

    /// <summary>
    /// Gets or sets whether a preset was loaded (for dialog result).
    /// </summary>
    public bool PresetLoaded { get; private set; }

    public VstPresetBrowserViewModel(IVstPlugin plugin, string pluginName)
    {
        _plugin = plugin;
        _pluginName = pluginName;

        // Set up user presets path
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _userPresetsPath = Path.Combine(appData, "MusicEngineEditor", "Presets", SanitizeFileName(pluginName));

        // Ensure directory exists
        if (!Directory.Exists(_userPresetsPath))
        {
            Directory.CreateDirectory(_userPresetsPath);
        }

        // Initialize current preset name
        CurrentPresetName = plugin.CurrentPresetName;

        // Load presets
        RefreshPresets();
    }

    /// <summary>
    /// Refreshes the list of available presets.
    /// </summary>
    [RelayCommand]
    public void RefreshPresets()
    {
        AllPresets.Clear();
        Categories.Clear();
        Categories.Add("All");

        var categorySet = new HashSet<string>();

        // Load built-in presets from plugin
        var pluginPresets = _plugin.GetPresetNames();
        for (int i = 0; i < pluginPresets.Count; i++)
        {
            var preset = new PresetItemViewModel
            {
                Name = pluginPresets[i],
                Index = i,
                IsFactoryPreset = true,
                Category = "Factory"
            };
            AllPresets.Add(preset);

            if (!string.IsNullOrEmpty(preset.Category))
            {
                categorySet.Add(preset.Category);
            }
        }

        // Load user presets from disk
        LoadUserPresets(categorySet);

        // Load factory presets from common locations
        LoadFactoryPresetsFromDisk(categorySet);

        // Add categories
        foreach (var category in categorySet.OrderBy(c => c))
        {
            Categories.Add(category);
        }

        // Apply filter
        ApplyFilter();
    }

    private void LoadUserPresets(HashSet<string> categorySet)
    {
        if (!Directory.Exists(_userPresetsPath))
            return;

        try
        {
            // Support both .fxp (VST2) and .vstpreset (VST3) files
            var presetFiles = Directory.GetFiles(_userPresetsPath, "*.fxp")
                .Concat(Directory.GetFiles(_userPresetsPath, "*.vstpreset"))
                .OrderBy(f => Path.GetFileNameWithoutExtension(f));

            foreach (var file in presetFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var preset = new PresetItemViewModel
                {
                    Name = name,
                    FilePath = file,
                    IsUserPreset = true,
                    Category = "User"
                };
                AllPresets.Add(preset);
            }

            categorySet.Add("User");
        }
        catch
        {
            // Ignore errors loading user presets
        }
    }

    private void LoadFactoryPresetsFromDisk(HashSet<string> categorySet)
    {
        // Look for factory presets in common locations
        var factoryPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "VST3 Presets", _pluginName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Common Files", "VST3", "Presets", _pluginName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VST3 Presets", _pluginName)
        };

        foreach (var path in factoryPaths.Where(Directory.Exists))
        {
            try
            {
                LoadPresetsFromDirectory(path, true, categorySet);
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    private void LoadPresetsFromDirectory(string directory, bool isFactory, HashSet<string> categorySet, string? parentCategory = null)
    {
        try
        {
            // Load presets from this directory
            var presetFiles = Directory.GetFiles(directory, "*.fxp")
                .Concat(Directory.GetFiles(directory, "*.vstpreset"))
                .OrderBy(f => Path.GetFileNameWithoutExtension(f));

            string category = parentCategory ?? (isFactory ? "Factory" : "User");

            foreach (var file in presetFiles)
            {
                // Skip if already loaded (e.g., same name from plugin)
                var name = Path.GetFileNameWithoutExtension(file);
                if (AllPresets.Any(p => p.Name == name && p.FilePath == file))
                    continue;

                var preset = new PresetItemViewModel
                {
                    Name = name,
                    FilePath = file,
                    IsFactoryPreset = isFactory,
                    IsUserPreset = !isFactory,
                    Category = category
                };
                AllPresets.Add(preset);

                if (!string.IsNullOrEmpty(category))
                {
                    categorySet.Add(category);
                }
            }

            // Recurse into subdirectories (each becomes a category)
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var subCategory = Path.GetFileName(subDir);
                LoadPresetsFromDirectory(subDir, isFactory, categorySet, subCategory);
            }
        }
        catch
        {
            // Ignore errors
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnShowFactoryPresetsChanged(bool value)
    {
        ApplyFilter();
    }

    partial void OnShowUserPresetsChanged(bool value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredPresets.Clear();

        var filtered = AllPresets.AsEnumerable();

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(searchLower) ||
                p.Category.ToLowerInvariant().Contains(searchLower) ||
                p.Author.ToLowerInvariant().Contains(searchLower));
        }

        // Filter by category
        if (SelectedCategory != "All" && !string.IsNullOrEmpty(SelectedCategory))
        {
            filtered = filtered.Where(p => p.Category == SelectedCategory);
        }

        // Filter by type
        if (!ShowFactoryPresets)
        {
            filtered = filtered.Where(p => !p.IsFactoryPreset);
        }

        if (!ShowUserPresets)
        {
            filtered = filtered.Where(p => !p.IsUserPreset);
        }

        foreach (var preset in filtered)
        {
            FilteredPresets.Add(preset);
        }
    }

    /// <summary>
    /// Loads the selected preset (preview mode - can be reverted).
    /// </summary>
    [RelayCommand]
    public void PreviewPreset()
    {
        if (SelectedPreset == null)
            return;

        LoadPresetInternal(SelectedPreset);
    }

    /// <summary>
    /// Loads the selected preset and confirms it.
    /// </summary>
    [RelayCommand]
    public void LoadPreset()
    {
        if (SelectedPreset == null)
            return;

        if (LoadPresetInternal(SelectedPreset))
        {
            PresetLoaded = true;
            CurrentPresetName = SelectedPreset.Name;
        }
    }

    private bool LoadPresetInternal(PresetItemViewModel preset)
    {
        try
        {
            if (preset.Index >= 0)
            {
                // Built-in preset - use index
                _plugin.SetPreset(preset.Index);
                return true;
            }
            else if (!string.IsNullOrEmpty(preset.FilePath))
            {
                // File-based preset
                return _plugin.LoadPreset(preset.FilePath);
            }

            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading preset: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Saves the current plugin state as a new preset.
    /// </summary>
    [RelayCommand]
    public void SavePreset()
    {
        if (string.IsNullOrWhiteSpace(NewPresetName))
        {
            StatusMessage = "Please enter a preset name";
            return;
        }

        try
        {
            var safeName = SanitizeFileName(NewPresetName);
            var extension = _plugin.IsVst3 ? ".vstpreset" : ".fxp";
            var presetPath = Path.Combine(_userPresetsPath, safeName + extension);

            // Check if file exists
            if (File.Exists(presetPath))
            {
                StatusMessage = "A preset with this name already exists";
                return;
            }

            if (_plugin.SavePreset(presetPath))
            {
                StatusMessage = $"Preset saved: {NewPresetName}";
                NewPresetName = "";

                // Refresh to show the new preset
                RefreshPresets();
            }
            else
            {
                StatusMessage = "Failed to save preset";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving preset: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes the selected preset (user presets only).
    /// </summary>
    [RelayCommand]
    public void DeletePreset()
    {
        if (SelectedPreset == null)
            return;

        if (!SelectedPreset.IsUserPreset || string.IsNullOrEmpty(SelectedPreset.FilePath))
        {
            StatusMessage = "Only user presets can be deleted";
            return;
        }

        try
        {
            if (File.Exists(SelectedPreset.FilePath))
            {
                File.Delete(SelectedPreset.FilePath);
                StatusMessage = $"Preset deleted: {SelectedPreset.Name}";

                // Refresh the list
                RefreshPresets();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting preset: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the user presets folder in Explorer.
    /// </summary>
    [RelayCommand]
    public void OpenPresetsFolder()
    {
        try
        {
            if (Directory.Exists(_userPresetsPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", _userPresetsPath);
            }
            else
            {
                Directory.CreateDirectory(_userPresetsPath);
                System.Diagnostics.Process.Start("explorer.exe", _userPresetsPath);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening folder: {ex.Message}";
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Preset" : sanitized;
    }
}

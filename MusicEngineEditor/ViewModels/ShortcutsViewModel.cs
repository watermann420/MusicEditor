using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for a shortcut item in the list
/// </summary>
public partial class ShortcutItemViewModel : ObservableObject
{
    private readonly KeyboardShortcut _shortcut;
    private readonly ShortcutsViewModel _parent;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingText = string.Empty;

    public string Id => _shortcut.Id;
    public string CommandName => _shortcut.CommandName;
    public string Description => _shortcut.Description;
    public bool IsCustomizable => _shortcut.IsCustomizable;
    public ShortcutCategory Category => _shortcut.Category;

    public string DisplayString => _shortcut.DisplayString;

    public Key Key
    {
        get => _shortcut.Key;
        set
        {
            if (_shortcut.Key != value)
            {
                _shortcut.Key = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayString));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    public ModifierKeys Modifiers
    {
        get => _shortcut.Modifiers;
        set
        {
            if (_shortcut.Modifiers != value)
            {
                _shortcut.Modifiers = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayString));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    public bool IsModified => _parent.IsShortcutModified(Id);

    public ShortcutItemViewModel(KeyboardShortcut shortcut, ShortcutsViewModel parent)
    {
        _shortcut = shortcut;
        _parent = parent;
    }

    public KeyboardShortcut GetShortcut() => _shortcut;

    public void RefreshDisplay()
    {
        OnPropertyChanged(nameof(DisplayString));
        OnPropertyChanged(nameof(IsModified));
    }
}

/// <summary>
/// ViewModel for a category group
/// </summary>
public partial class ShortcutCategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    public ShortcutCategory Category { get; }
    public string DisplayName => Category.ToString();
    public ObservableCollection<ShortcutItemViewModel> Shortcuts { get; } = [];

    public ShortcutCategoryViewModel(ShortcutCategory category)
    {
        Category = category;
    }
}

/// <summary>
/// ViewModel for the Shortcuts dialog
/// </summary>
public partial class ShortcutsViewModel : ViewModelBase
{
    private readonly IShortcutService _shortcutService;
    private readonly Dictionary<string, KeyboardShortcut> _originalShortcuts = [];

    /// <summary>
    /// All shortcut categories with their shortcuts
    /// </summary>
    public ObservableCollection<ShortcutCategoryViewModel> Categories { get; } = [];

    /// <summary>
    /// Filtered shortcuts for display
    /// </summary>
    public ObservableCollection<ShortcutCategoryViewModel> FilteredCategories { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ShortcutItemViewModel? _selectedShortcut;

    [ObservableProperty]
    private ShortcutCategory? _selectedCategory;

    [ObservableProperty]
    private string? _conflictMessage;

    [ObservableProperty]
    private bool _hasChanges;

    /// <summary>
    /// Event raised when settings should be applied and dialog closed
    /// </summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Event raised when dialog should be cancelled
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Available categories for filtering
    /// </summary>
    public ObservableCollection<ShortcutCategory?> AvailableCategories { get; } = [];

    public ShortcutsViewModel(IShortcutService shortcutService)
    {
        _shortcutService = shortcutService;
        Initialize();
    }

    private void Initialize()
    {
        // Add "All" option
        AvailableCategories.Add(null);

        // Load categories and shortcuts
        foreach (var category in _shortcutService.GetCategories())
        {
            AvailableCategories.Add(category);

            var categoryVm = new ShortcutCategoryViewModel(category);
            foreach (var shortcut in _shortcutService.GetShortcutsForCategory(category))
            {
                // Store original state
                _originalShortcuts[shortcut.Id] = shortcut.Clone();

                var itemVm = new ShortcutItemViewModel(shortcut, this);
                categoryVm.Shortcuts.Add(itemVm);
            }
            Categories.Add(categoryVm);
        }

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryChanged(ShortcutCategory? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredCategories.Clear();

        var searchLower = SearchText?.ToLowerInvariant() ?? string.Empty;

        foreach (var category in Categories)
        {
            // Skip if category filter is set and doesn't match
            if (SelectedCategory.HasValue && category.Category != SelectedCategory.Value)
                continue;

            var filteredShortcuts = category.Shortcuts
                .Where(s => string.IsNullOrEmpty(searchLower) ||
                            s.CommandName.ToLowerInvariant().Contains(searchLower) ||
                            s.Description.ToLowerInvariant().Contains(searchLower) ||
                            s.DisplayString.ToLowerInvariant().Contains(searchLower))
                .ToList();

            if (filteredShortcuts.Count > 0)
            {
                var filteredCategory = new ShortcutCategoryViewModel(category.Category)
                {
                    IsExpanded = category.IsExpanded
                };

                foreach (var shortcut in filteredShortcuts)
                {
                    filteredCategory.Shortcuts.Add(shortcut);
                }

                FilteredCategories.Add(filteredCategory);
            }
        }
    }

    public bool IsShortcutModified(string shortcutId)
    {
        if (!_originalShortcuts.TryGetValue(shortcutId, out var original))
            return false;

        var current = _shortcutService.GetShortcut(shortcutId);
        return current != null && !current.Equals(original);
    }

    /// <summary>
    /// Updates the selected shortcut with a new key combination
    /// </summary>
    public bool UpdateShortcut(Key key, ModifierKeys modifiers)
    {
        if (SelectedShortcut == null || !SelectedShortcut.IsCustomizable)
            return false;

        // Check for conflicts
        var conflict = _shortcutService.GetConflictingShortcut(key, modifiers, SelectedShortcut.Id);
        if (conflict != null)
        {
            ConflictMessage = $"This shortcut is already used by \"{conflict.Description}\"";
            return false;
        }

        ConflictMessage = null;

        SelectedShortcut.Key = key;
        SelectedShortcut.Modifiers = modifiers;
        SelectedShortcut.RefreshDisplay();

        CheckForChanges();
        return true;
    }

    /// <summary>
    /// Clears the shortcut binding for the selected shortcut
    /// </summary>
    [RelayCommand]
    private void ClearShortcut()
    {
        if (SelectedShortcut == null || !SelectedShortcut.IsCustomizable)
            return;

        SelectedShortcut.Key = Key.None;
        SelectedShortcut.Modifiers = ModifierKeys.None;
        SelectedShortcut.RefreshDisplay();

        ConflictMessage = null;
        CheckForChanges();
    }

    /// <summary>
    /// Resets the selected shortcut to its default
    /// </summary>
    [RelayCommand]
    private void ResetShortcut()
    {
        if (SelectedShortcut == null)
            return;

        _shortcutService.ResetShortcut(SelectedShortcut.Id);

        // Refresh the original to match the reset value
        var shortcut = _shortcutService.GetShortcut(SelectedShortcut.Id);
        if (shortcut != null)
        {
            SelectedShortcut.Key = shortcut.Key;
            SelectedShortcut.Modifiers = shortcut.Modifiers;
            SelectedShortcut.RefreshDisplay();
        }

        ConflictMessage = null;
        CheckForChanges();
    }

    /// <summary>
    /// Resets all shortcuts to defaults
    /// </summary>
    [RelayCommand]
    private void ResetAllToDefaults()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all keyboard shortcuts to their default values?",
            "Reset Shortcuts",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _shortcutService.ResetToDefaults();

        // Refresh all items
        foreach (var category in Categories)
        {
            foreach (var item in category.Shortcuts)
            {
                var shortcut = _shortcutService.GetShortcut(item.Id);
                if (shortcut != null)
                {
                    item.Key = shortcut.Key;
                    item.Modifiers = shortcut.Modifiers;
                    item.RefreshDisplay();
                }
            }
        }

        ConflictMessage = null;
        CheckForChanges();
        StatusMessage = "All shortcuts reset to defaults";
    }

    /// <summary>
    /// Exports shortcuts to a file
    /// </summary>
    [RelayCommand]
    private async Task ExportShortcutsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Keyboard Shortcuts",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "keyboard-shortcuts.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _shortcutService.ExportAsync(dialog.FileName);
                StatusMessage = "Shortcuts exported successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export shortcuts: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Imports shortcuts from a file
    /// </summary>
    [RelayCommand]
    private async Task ImportShortcutsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Keyboard Shortcuts",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _shortcutService.ImportAsync(dialog.FileName);

                // Refresh all items
                foreach (var category in Categories)
                {
                    foreach (var item in category.Shortcuts)
                    {
                        var shortcut = _shortcutService.GetShortcut(item.Id);
                        if (shortcut != null)
                        {
                            item.Key = shortcut.Key;
                            item.Modifiers = shortcut.Modifiers;
                            item.RefreshDisplay();
                        }
                    }
                }

                CheckForChanges();
                StatusMessage = "Shortcuts imported successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import shortcuts: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Applies and saves changes
    /// </summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        IsBusy = true;
        StatusMessage = "Saving shortcuts...";

        try
        {
            await _shortcutService.SaveToFileAsync();
            StatusMessage = "Shortcuts saved";
            ApplyRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save shortcuts: {ex.Message}";
            MessageBox.Show($"Failed to save shortcuts: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Cancels changes and closes dialog
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        // Restore original shortcuts
        foreach (var kvp in _originalShortcuts)
        {
            var shortcut = _shortcutService.GetShortcut(kvp.Key);
            shortcut?.UpdateKeyBinding(kvp.Value);
        }

        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Expands all categories
    /// </summary>
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var category in FilteredCategories)
        {
            category.IsExpanded = true;
        }
        foreach (var category in Categories)
        {
            category.IsExpanded = true;
        }
    }

    /// <summary>
    /// Collapses all categories
    /// </summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var category in FilteredCategories)
        {
            category.IsExpanded = false;
        }
        foreach (var category in Categories)
        {
            category.IsExpanded = false;
        }
    }

    private void CheckForChanges()
    {
        HasChanges = _originalShortcuts.Any(kvp =>
        {
            var current = _shortcutService.GetShortcut(kvp.Key);
            return current != null && !current.Equals(kvp.Value);
        });
    }
}

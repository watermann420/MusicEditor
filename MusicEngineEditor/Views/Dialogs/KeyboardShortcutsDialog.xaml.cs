// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Enhanced keyboard shortcut editor dialog with full customization support.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Enhanced dialog for viewing and editing keyboard shortcuts with full persistence support.
/// </summary>
public partial class KeyboardShortcutsDialog : Window
{
    #region Fields

    private static readonly string ShortcutsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor");

    private static readonly string ShortcutsFilePath = Path.Combine(ShortcutsFolder, "shortcuts.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ObservableCollection<ShortcutDisplayItem> _displayItems = new();
    private readonly Dictionary<string, KeyboardShortcut> _shortcuts = new();
    private readonly Dictionary<string, KeyboardShortcut> _defaultShortcuts = new();
    private readonly Dictionary<string, KeyboardShortcut> _originalShortcuts = new();
    private readonly HashSet<string> _modifiedIds = new();
    private readonly HashSet<string> _conflictingIds = new();

    private readonly IShortcutService? _shortcutService;

    private ShortcutDisplayItem? _editingItem;
    private KeyboardShortcut? _conflictingShortcut;
    private Key _capturedKey = Key.None;
    private ModifierKeys _capturedModifiers = ModifierKeys.None;
    private string _searchFilter = string.Empty;
    private string _categoryFilter = string.Empty;
    private bool _showConflictsOnly;
    private bool _hasUnsavedChanges;
    private bool _isLoadingFromService;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new keyboard shortcuts dialog.
    /// </summary>
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();

        ShortcutsItemsControl.ItemsSource = _displayItems;
        LoadDefaultShortcuts();
        LoadSavedShortcuts();
        RefreshDisplay();
    }

    /// <summary>
    /// Creates a new keyboard shortcuts dialog with a shortcut service.
    /// </summary>
    /// <param name="shortcutService">The shortcut service to use for loading/saving shortcuts.</param>
    public KeyboardShortcutsDialog(IShortcutService shortcutService) : this()
    {
        _shortcutService = shortcutService;
        _isLoadingFromService = true;

        // Load shortcuts from the service
        LoadFromShortcutService();

        _isLoadingFromService = false;
    }

    /// <summary>
    /// Creates a new keyboard shortcuts dialog with pre-loaded shortcuts.
    /// </summary>
    /// <param name="shortcuts">The shortcuts to load.</param>
    public KeyboardShortcutsDialog(IEnumerable<KeyboardShortcut> shortcuts) : this()
    {
        LoadShortcuts(shortcuts);
    }

    #endregion

    #region Event Handlers

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (ShortcutEditorPanel.Visibility == Visibility.Visible && _editingItem != null)
        {
            // Ignore modifier-only key presses
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
            {
                e.Handled = true;
                return;
            }

            // Capture the actual key (handle System key for Alt combinations)
            _capturedKey = e.Key == Key.System ? e.SystemKey : e.Key;
            _capturedModifiers = Keyboard.Modifiers;

            UpdateShortcutInputDisplay();
            DetectConflictsForCapture();

            e.Handled = true;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes to keyboard shortcuts. Do you want to save them before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    SaveShortcutsToFile();
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchFilter = SearchTextBox.Text ?? string.Empty;
        RefreshDisplay();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilterCombo.SelectedItem is ComboBoxItem item)
        {
            _categoryFilter = item.Tag?.ToString() ?? string.Empty;
            RefreshDisplay();
        }
    }

    private void ShowConflictsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _showConflictsOnly = ShowConflictsCheckBox.IsChecked == true;
        RefreshDisplay();
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingFromService) return;

        if (PresetCombo.SelectedItem is ComboBoxItem item && item.Content is string preset)
        {
            if (preset != "Custom")
            {
                LoadPreset(preset);
            }
        }
    }

    private void ShortcutRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ShortcutDisplayItem item && item.IsCustomizable)
        {
            if (e.ClickCount == 2)
            {
                StartEditing(item);
                e.Handled = true;
            }
        }
    }

    private void EditShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ShortcutDisplayItem item)
        {
            StartEditing(item);
        }
    }

    private void ResetShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ShortcutDisplayItem item && item.Shortcut != null)
        {
            ResetSingleShortcut(item.Shortcut.Id);
        }
    }

    private void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        _capturedKey = Key.None;
        _capturedModifiers = ModifierKeys.None;
        ShortcutInputText.Text = "Not Set";
        ShortcutInputText.Foreground = FindResource("SecondaryForegroundBrush") as Brush;
        _conflictingShortcut = null;
        ConflictWarningPanel.Visibility = Visibility.Collapsed;
    }

    private void CancelEditButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEditing();
    }

    private void ApplyShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyShortcutEdit(forceReassign: false);
    }

    private void ReassignAnywayButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyShortcutEdit(forceReassign: true);
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Keyboard Shortcuts"
        };

        if (dialog.ShowDialog() == true)
        {
            ImportShortcuts(dialog.FileName);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export Keyboard Shortcuts",
            FileName = "KeyboardShortcuts.json"
        };

        if (dialog.ShowDialog() == true)
        {
            ExportShortcuts(dialog.FileName);
        }
    }

    private void ResetAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
            "Reset all shortcuts to their default values?\n\nThis will undo all your customizations.",
            "Reset All Shortcuts",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            ResetToDefaults();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveShortcutsToFile();
        MessageBox.Show(
            "Keyboard shortcuts saved successfully.",
            "Shortcuts Saved",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads shortcuts from an enumerable collection.
    /// </summary>
    public void LoadShortcuts(IEnumerable<KeyboardShortcut> shortcuts)
    {
        _shortcuts.Clear();
        _originalShortcuts.Clear();

        foreach (var shortcut in shortcuts)
        {
            _shortcuts[shortcut.Id] = shortcut.Clone();
            _originalShortcuts[shortcut.Id] = shortcut.Clone();

            // Store as default if not already present
            if (!_defaultShortcuts.ContainsKey(shortcut.Id))
            {
                _defaultShortcuts[shortcut.Id] = shortcut.Clone();
            }
        }

        RefreshDisplay();
    }

    /// <summary>
    /// Gets all shortcuts that have been modified from their defaults.
    /// </summary>
    public IEnumerable<KeyboardShortcut> GetModifiedShortcuts()
    {
        return _shortcuts.Values.Where(s => _modifiedIds.Contains(s.Id));
    }

    /// <summary>
    /// Gets all shortcuts.
    /// </summary>
    public IEnumerable<KeyboardShortcut> GetAllShortcuts()
    {
        return _shortcuts.Values;
    }

    /// <summary>
    /// Gets whether there are unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    #endregion

    #region Private Methods - Initialization

    private void LoadDefaultShortcuts()
    {
        var defaults = GetDefaultShortcuts();

        foreach (var shortcut in defaults)
        {
            _shortcuts[shortcut.Id] = shortcut.Clone();
            _defaultShortcuts[shortcut.Id] = shortcut.Clone();
            _originalShortcuts[shortcut.Id] = shortcut.Clone();
        }
    }

    private void LoadSavedShortcuts()
    {
        try
        {
            if (!File.Exists(ShortcutsFilePath))
                return;

            var json = File.ReadAllText(ShortcutsFilePath);
            var collection = JsonSerializer.Deserialize<ShortcutCollection>(json, JsonOptions);

            if (collection?.CustomShortcuts != null)
            {
                foreach (var customShortcut in collection.CustomShortcuts)
                {
                    if (_shortcuts.TryGetValue(customShortcut.Id, out var existing) && existing.IsCustomizable)
                    {
                        existing.Key = customShortcut.Key;
                        existing.Modifiers = customShortcut.Modifiers;
                        _modifiedIds.Add(customShortcut.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load saved shortcuts: {ex.Message}");
        }
    }

    private void LoadFromShortcutService()
    {
        if (_shortcutService == null) return;

        _shortcuts.Clear();
        _defaultShortcuts.Clear();
        _originalShortcuts.Clear();
        _modifiedIds.Clear();

        foreach (var shortcut in _shortcutService.Shortcuts)
        {
            _shortcuts[shortcut.Id] = shortcut.Clone();
            _originalShortcuts[shortcut.Id] = shortcut.Clone();

            // We don't know the original defaults from the service,
            // so we'll assume current values are defaults unless modified
            _defaultShortcuts[shortcut.Id] = shortcut.Clone();
        }

        RefreshDisplay();
    }

    #endregion

    #region Private Methods - Display

    private void RefreshDisplay()
    {
        _displayItems.Clear();
        DetectAllConflicts();

        var filteredShortcuts = _shortcuts.Values.AsEnumerable();

        // Apply category filter
        if (!string.IsNullOrEmpty(_categoryFilter))
        {
            if (Enum.TryParse<ShortcutCategory>(_categoryFilter, out var category))
            {
                filteredShortcuts = filteredShortcuts.Where(s => s.Category == category);
            }
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            var search = _searchFilter.ToLowerInvariant();
            filteredShortcuts = filteredShortcuts.Where(s =>
                s.CommandName.ToLowerInvariant().Contains(search) ||
                s.Description.ToLowerInvariant().Contains(search) ||
                s.DisplayString.ToLowerInvariant().Contains(search) ||
                s.Id.ToLowerInvariant().Contains(search));
        }

        // Apply conflicts filter
        if (_showConflictsOnly)
        {
            filteredShortcuts = filteredShortcuts.Where(s => _conflictingIds.Contains(s.Id));
        }

        // Group by category
        var grouped = filteredShortcuts
            .OrderBy(s => s.Category)
            .ThenBy(s => s.CommandName)
            .GroupBy(s => s.Category);

        foreach (var group in grouped)
        {
            var shortcutsList = group.ToList();

            // Add category header
            _displayItems.Add(new ShortcutDisplayItem
            {
                IsHeader = true,
                CategoryName = group.Key.ToString(),
                ShortcutCount = shortcutsList.Count
            });

            // Add shortcuts
            foreach (var shortcut in shortcutsList)
            {
                var conflictTooltip = string.Empty;
                if (_conflictingIds.Contains(shortcut.Id))
                {
                    var conflicts = GetConflictsForShortcut(shortcut);
                    conflictTooltip = $"Conflicts with: {string.Join(", ", conflicts.Select(c => c.CommandName))}";
                }

                _displayItems.Add(new ShortcutDisplayItem
                {
                    IsShortcut = true,
                    Shortcut = shortcut,
                    HasConflict = _conflictingIds.Contains(shortcut.Id),
                    IsModified = IsShortcutModified(shortcut.Id),
                    ConflictTooltip = conflictTooltip
                });
            }
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        TotalCountText.Text = $"{_shortcuts.Count} shortcuts";

        var conflictCount = _conflictingIds.Count;
        ConflictCountText.Text = conflictCount == 0
            ? "No conflicts"
            : $"{conflictCount} conflict{(conflictCount != 1 ? "s" : "")}";

        ConflictCountText.Foreground = conflictCount > 0
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57))
            : FindResource("SecondaryForegroundBrush") as Brush;

        var modifiedCount = _modifiedIds.Count;
        ModifiedCountText.Text = modifiedCount > 0
            ? $"{modifiedCount} modified"
            : "No modifications";

        ModifiedCountText.Foreground = modifiedCount > 0
            ? new SolidColorBrush(Color.FromRgb(0x8B, 0xC3, 0x4A))
            : FindResource("SecondaryForegroundBrush") as Brush;
    }

    #endregion

    #region Private Methods - Editing

    private void StartEditing(ShortcutDisplayItem item)
    {
        _editingItem = item;
        _capturedKey = item.Shortcut?.Key ?? Key.None;
        _capturedModifiers = item.Shortcut?.Modifiers ?? ModifierKeys.None;
        _conflictingShortcut = null;

        EditingCommandName.Text = item.CommandName;
        UpdateShortcutInputDisplay();
        ConflictWarningPanel.Visibility = Visibility.Collapsed;
        ShortcutEditorPanel.Visibility = Visibility.Visible;

        // Focus the input area
        ShortcutInputBorder.Focus();
    }

    private void CancelEditing()
    {
        _editingItem = null;
        _capturedKey = Key.None;
        _capturedModifiers = ModifierKeys.None;
        _conflictingShortcut = null;
        ShortcutEditorPanel.Visibility = Visibility.Collapsed;
        ConflictWarningPanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyShortcutEdit(bool forceReassign)
    {
        if (_editingItem?.Shortcut == null) return;

        // Handle conflict resolution
        if (_conflictingShortcut != null && !forceReassign)
        {
            return; // User needs to click "Reassign Anyway" or choose a different shortcut
        }

        // If forcing reassign, clear the conflicting shortcut first
        if (_conflictingShortcut != null && forceReassign)
        {
            _conflictingShortcut.Key = Key.None;
            _conflictingShortcut.Ctrl = false;
            _conflictingShortcut.Alt = false;
            _conflictingShortcut.Shift = false;
            _modifiedIds.Add(_conflictingShortcut.Id);
        }

        // Apply the new shortcut
        var shortcut = _shortcuts[_editingItem.Shortcut.Id];
        shortcut.Key = _capturedKey;
        shortcut.Modifiers = _capturedModifiers;

        _modifiedIds.Add(shortcut.Id);
        _hasUnsavedChanges = true;

        CancelEditing();
        RefreshDisplay();

        // Set preset to "Custom" since user modified a shortcut
        PresetCombo.SelectedIndex = 5; // Custom
    }

    private void UpdateShortcutInputDisplay()
    {
        if (_capturedKey == Key.None)
        {
            ShortcutInputText.Text = "Not Set";
            ShortcutInputText.Foreground = FindResource("SecondaryForegroundBrush") as Brush;
            return;
        }

        var parts = new List<string>();
        if ((_capturedModifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((_capturedModifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((_capturedModifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        parts.Add(GetKeyDisplayName(_capturedKey));

        ShortcutInputText.Text = string.Join(" + ", parts);
        ShortcutInputText.Foreground = FindResource("ForegroundBrush") as Brush;
    }

    #endregion

    #region Private Methods - Conflict Detection

    private void DetectAllConflicts()
    {
        _conflictingIds.Clear();

        var shortcutGroups = _shortcuts.Values
            .Where(s => s.Key != Key.None)
            .GroupBy(s => new { s.Key, s.Modifiers })
            .Where(g => g.Count() > 1);

        foreach (var group in shortcutGroups)
        {
            foreach (var shortcut in group)
            {
                _conflictingIds.Add(shortcut.Id);
            }
        }
    }

    private void DetectConflictsForCapture()
    {
        if (_editingItem == null || _capturedKey == Key.None)
        {
            _conflictingShortcut = null;
            ConflictWarningPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _conflictingShortcut = _shortcuts.Values
            .FirstOrDefault(s =>
                s.Id != _editingItem.Shortcut?.Id &&
                s.Key == _capturedKey &&
                s.Modifiers == _capturedModifiers);

        if (_conflictingShortcut != null)
        {
            ShortcutInputText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
            ConflictWarningText.Text = "This shortcut is already assigned to:";
            ConflictCommandText.Text = $"{_conflictingShortcut.CommandName} ({_conflictingShortcut.Description})";
            ConflictWarningPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ShortcutInputText.Foreground = FindResource("ForegroundBrush") as Brush;
            ConflictWarningPanel.Visibility = Visibility.Collapsed;
        }
    }

    private IEnumerable<KeyboardShortcut> GetConflictsForShortcut(KeyboardShortcut shortcut)
    {
        if (shortcut.Key == Key.None)
            return Enumerable.Empty<KeyboardShortcut>();

        return _shortcuts.Values
            .Where(s => s.Id != shortcut.Id &&
                       s.Key == shortcut.Key &&
                       s.Modifiers == shortcut.Modifiers);
    }

    #endregion

    #region Private Methods - Persistence

    private void SaveShortcutsToFile()
    {
        try
        {
            Directory.CreateDirectory(ShortcutsFolder);

            // Only save shortcuts that differ from defaults
            var customShortcuts = new List<KeyboardShortcut>();

            foreach (var shortcut in _shortcuts.Values)
            {
                if (_defaultShortcuts.TryGetValue(shortcut.Id, out var defaultShortcut))
                {
                    if (!shortcut.Equals(defaultShortcut))
                    {
                        customShortcuts.Add(shortcut.Clone());
                    }
                }
            }

            var collection = new ShortcutCollection
            {
                Version = 1,
                CustomShortcuts = customShortcuts
            };

            var json = JsonSerializer.Serialize(collection, JsonOptions);
            File.WriteAllText(ShortcutsFilePath, json);

            // Also update the shortcut service if available
            if (_shortcutService != null)
            {
                foreach (var shortcut in _shortcuts.Values)
                {
                    _shortcutService.UpdateShortcut(shortcut.Id, shortcut.Key, shortcut.Modifiers);
                }
                Task.Run(() => _shortcutService.SaveToFileAsync());
            }

            _hasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save shortcuts: {ex.Message}",
                "Save Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportShortcuts(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var collection = JsonSerializer.Deserialize<ShortcutCollection>(json, JsonOptions);

            if (collection?.CustomShortcuts != null)
            {
                var importCount = 0;

                foreach (var shortcut in collection.CustomShortcuts)
                {
                    if (_shortcuts.TryGetValue(shortcut.Id, out var existing) && existing.IsCustomizable)
                    {
                        existing.Key = shortcut.Key;
                        existing.Modifiers = shortcut.Modifiers;
                        _modifiedIds.Add(shortcut.Id);
                        importCount++;
                    }
                }

                PresetCombo.SelectedIndex = 5; // Custom
                _hasUnsavedChanges = true;
                RefreshDisplay();

                MessageBox.Show(
                    $"Successfully imported {importCount} shortcut(s).",
                    "Import Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to import shortcuts: {ex.Message}",
                "Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportShortcuts(string filePath)
    {
        try
        {
            var collection = new ShortcutCollection
            {
                Version = 1,
                CustomShortcuts = _shortcuts.Values
                    .Where(s => s.IsCustomizable)
                    .Select(s => s.Clone())
                    .ToList()
            };

            var json = JsonSerializer.Serialize(collection, JsonOptions);
            File.WriteAllText(filePath, json);

            MessageBox.Show(
                $"Exported {collection.CustomShortcuts.Count} shortcuts.",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to export shortcuts: {ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region Private Methods - Reset

    private void ResetToDefaults()
    {
        foreach (var defaultShortcut in _defaultShortcuts.Values)
        {
            if (_shortcuts.TryGetValue(defaultShortcut.Id, out var shortcut) && shortcut.IsCustomizable)
            {
                shortcut.Key = defaultShortcut.Key;
                shortcut.Modifiers = defaultShortcut.Modifiers;
            }
        }

        _modifiedIds.Clear();
        _hasUnsavedChanges = true;
        PresetCombo.SelectedIndex = 0; // Default
        RefreshDisplay();
    }

    private void ResetSingleShortcut(string shortcutId)
    {
        if (_defaultShortcuts.TryGetValue(shortcutId, out var defaultShortcut) &&
            _shortcuts.TryGetValue(shortcutId, out var shortcut))
        {
            shortcut.Key = defaultShortcut.Key;
            shortcut.Modifiers = defaultShortcut.Modifiers;
            _modifiedIds.Remove(shortcutId);
            _hasUnsavedChanges = true;
            RefreshDisplay();
        }
    }

    private bool IsShortcutModified(string shortcutId)
    {
        if (!_defaultShortcuts.TryGetValue(shortcutId, out var defaultShortcut) ||
            !_shortcuts.TryGetValue(shortcutId, out var shortcut))
        {
            return false;
        }

        return !shortcut.Equals(defaultShortcut);
    }

    #endregion

    #region Private Methods - Presets

    private void LoadPreset(string presetName)
    {
        var presetShortcuts = GetPresetShortcuts(presetName);
        if (presetShortcuts == null) return;

        // First reset to defaults
        foreach (var defaultShortcut in _defaultShortcuts.Values)
        {
            if (_shortcuts.TryGetValue(defaultShortcut.Id, out var shortcut) && shortcut.IsCustomizable)
            {
                shortcut.Key = defaultShortcut.Key;
                shortcut.Modifiers = defaultShortcut.Modifiers;
            }
        }

        _modifiedIds.Clear();

        // Then apply preset changes
        foreach (var preset in presetShortcuts)
        {
            if (_shortcuts.TryGetValue(preset.Id, out var shortcut) && shortcut.IsCustomizable)
            {
                shortcut.Key = preset.Key;
                shortcut.Modifiers = preset.Modifiers;
                _modifiedIds.Add(shortcut.Id);
            }
        }

        _hasUnsavedChanges = true;
        RefreshDisplay();
    }

    #endregion

    #region Static Methods - Key Display

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemBackslash => "\\",
            Key.OemTilde => "`",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.NumPad0 => "Num 0",
            Key.NumPad1 => "Num 1",
            Key.NumPad2 => "Num 2",
            Key.NumPad3 => "Num 3",
            Key.NumPad4 => "Num 4",
            Key.NumPad5 => "Num 5",
            Key.NumPad6 => "Num 6",
            Key.NumPad7 => "Num 7",
            Key.NumPad8 => "Num 8",
            Key.NumPad9 => "Num 9",
            Key.Multiply => "Num *",
            Key.Add => "Num +",
            Key.Subtract => "Num -",
            Key.Divide => "Num /",
            Key.Decimal => "Num .",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Escape => "Esc",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            _ => key.ToString()
        };
    }

    #endregion

    #region Static Methods - Default Shortcuts

    private static IEnumerable<KeyboardShortcut> GetDefaultShortcuts()
    {
        return new List<KeyboardShortcut>
        {
            // File operations
            new() { Id = "file.new", CommandName = "New Project", Description = "Create a new project", Key = Key.N, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.open", CommandName = "Open Project", Description = "Open an existing project", Key = Key.O, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.save", CommandName = "Save", Description = "Save the current project", Key = Key.S, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.saveAs", CommandName = "Save As", Description = "Save with a new name", Key = Key.S, Ctrl = true, Shift = true, Category = ShortcutCategory.File },
            new() { Id = "file.saveAll", CommandName = "Save All", Description = "Save all open files", Key = Key.S, Ctrl = true, Alt = true, Category = ShortcutCategory.File },
            new() { Id = "file.close", CommandName = "Close Tab", Description = "Close the current tab", Key = Key.W, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.export", CommandName = "Export", Description = "Export audio", Key = Key.E, Ctrl = true, Shift = true, Category = ShortcutCategory.File },
            new() { Id = "file.import", CommandName = "Import", Description = "Import audio file", Key = Key.I, Ctrl = true, Category = ShortcutCategory.File },

            // Edit operations
            new() { Id = "edit.undo", CommandName = "Undo", Description = "Undo the last action", Key = Key.Z, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.redo", CommandName = "Redo", Description = "Redo the last undone action", Key = Key.Y, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.redoAlt", CommandName = "Redo (Alt)", Description = "Redo the last undone action", Key = Key.Z, Ctrl = true, Shift = true, Category = ShortcutCategory.Edit, IsCustomizable = false },
            new() { Id = "edit.cut", CommandName = "Cut", Description = "Cut selection to clipboard", Key = Key.X, Ctrl = true, Category = ShortcutCategory.Edit, IsCustomizable = false },
            new() { Id = "edit.copy", CommandName = "Copy", Description = "Copy selection to clipboard", Key = Key.C, Ctrl = true, Category = ShortcutCategory.Edit, IsCustomizable = false },
            new() { Id = "edit.paste", CommandName = "Paste", Description = "Paste from clipboard", Key = Key.V, Ctrl = true, Category = ShortcutCategory.Edit, IsCustomizable = false },
            new() { Id = "edit.selectAll", CommandName = "Select All", Description = "Select all content", Key = Key.A, Ctrl = true, Category = ShortcutCategory.Edit, IsCustomizable = false },
            new() { Id = "edit.duplicate", CommandName = "Duplicate", Description = "Duplicate selection", Key = Key.D, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.delete", CommandName = "Delete", Description = "Delete selection", Key = Key.Delete, Category = ShortcutCategory.Edit, IsCustomizable = false },
            new() { Id = "edit.find", CommandName = "Find", Description = "Open find dialog", Key = Key.F, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.replace", CommandName = "Replace", Description = "Open find and replace dialog", Key = Key.H, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.findNext", CommandName = "Find Next", Description = "Find next occurrence", Key = Key.F3, Category = ShortcutCategory.Edit },
            new() { Id = "edit.findPrevious", CommandName = "Find Previous", Description = "Find previous occurrence", Key = Key.F3, Shift = true, Category = ShortcutCategory.Edit },

            // View operations
            new() { Id = "view.zoomIn", CommandName = "Zoom In", Description = "Zoom in", Key = Key.OemPlus, Ctrl = true, Category = ShortcutCategory.View },
            new() { Id = "view.zoomOut", CommandName = "Zoom Out", Description = "Zoom out", Key = Key.OemMinus, Ctrl = true, Category = ShortcutCategory.View },
            new() { Id = "view.resetZoom", CommandName = "Reset Zoom", Description = "Reset zoom to 100%", Key = Key.D0, Ctrl = true, Category = ShortcutCategory.View },
            new() { Id = "view.zoomFit", CommandName = "Zoom to Fit", Description = "Fit all in view", Key = Key.F, Ctrl = true, Shift = true, Category = ShortcutCategory.View },
            new() { Id = "view.fullscreen", CommandName = "Fullscreen", Description = "Toggle fullscreen mode", Key = Key.F11, Category = ShortcutCategory.View },
            new() { Id = "view.mixer", CommandName = "Mixer", Description = "Show/hide mixer", Key = Key.M, Category = ShortcutCategory.View },
            new() { Id = "view.pianoRoll", CommandName = "Piano Roll", Description = "Show piano roll editor", Key = Key.P, Category = ShortcutCategory.View },
            new() { Id = "view.arrangement", CommandName = "Arrangement", Description = "Show arrangement view", Key = Key.A, Category = ShortcutCategory.View },
            new() { Id = "view.browser", CommandName = "Browser", Description = "Show/hide browser panel", Key = Key.B, Category = ShortcutCategory.View },

            // Transport operations
            new() { Id = "transport.play", CommandName = "Play/Pause", Description = "Toggle playback", Key = Key.Space, Category = ShortcutCategory.Transport },
            new() { Id = "transport.stop", CommandName = "Stop", Description = "Stop playback", Key = Key.OemPeriod, Category = ShortcutCategory.Transport },
            new() { Id = "transport.record", CommandName = "Record", Description = "Toggle recording", Key = Key.R, Ctrl = true, Category = ShortcutCategory.Transport },
            new() { Id = "transport.loop", CommandName = "Loop", Description = "Toggle loop mode", Key = Key.L, Ctrl = true, Category = ShortcutCategory.Transport },
            new() { Id = "transport.gotoStart", CommandName = "Go to Start", Description = "Move to project start", Key = Key.Home, Category = ShortcutCategory.Transport },
            new() { Id = "transport.gotoEnd", CommandName = "Go to End", Description = "Move to project end", Key = Key.End, Category = ShortcutCategory.Transport },
            new() { Id = "transport.metronome", CommandName = "Metronome", Description = "Toggle metronome", Key = Key.M, Ctrl = true, Category = ShortcutCategory.Transport },
            new() { Id = "transport.tempoTap", CommandName = "Tap Tempo", Description = "Tap to set tempo", Key = Key.T, Category = ShortcutCategory.Transport },

            // Tools operations
            new() { Id = "tools.select", CommandName = "Select Tool", Description = "Switch to selection tool", Key = Key.V, Category = ShortcutCategory.Tools },
            new() { Id = "tools.pencil", CommandName = "Pencil Tool", Description = "Switch to pencil tool", Key = Key.B, Category = ShortcutCategory.Tools },
            new() { Id = "tools.eraser", CommandName = "Eraser Tool", Description = "Switch to eraser tool", Key = Key.E, Category = ShortcutCategory.Tools },
            new() { Id = "tools.split", CommandName = "Split Tool", Description = "Switch to split tool", Key = Key.T, Category = ShortcutCategory.Tools },
            new() { Id = "tools.mute", CommandName = "Mute Tool", Description = "Switch to mute tool", Key = Key.U, Category = ShortcutCategory.Tools },
            new() { Id = "tools.run", CommandName = "Run Script", Description = "Run the current script", Key = Key.F5, Category = ShortcutCategory.Tools },
            new() { Id = "tools.compile", CommandName = "Compile", Description = "Compile the current script", Key = Key.F6, Category = ShortcutCategory.Tools },
            new() { Id = "tools.settings", CommandName = "Settings", Description = "Open settings", Key = Key.OemComma, Ctrl = true, Category = ShortcutCategory.Tools },
            new() { Id = "tools.shortcuts", CommandName = "Keyboard Shortcuts", Description = "Open keyboard shortcuts editor", Key = Key.K, Ctrl = true, Shift = true, Category = ShortcutCategory.Tools },

            // Navigation
            new() { Id = "nav.nextMarker", CommandName = "Next Marker", Description = "Go to next marker", Key = Key.Right, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.prevMarker", CommandName = "Previous Marker", Description = "Go to previous marker", Key = Key.Left, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.gotoBar", CommandName = "Go to Bar", Description = "Jump to specific bar", Key = Key.G, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.gotoLine", CommandName = "Go to Line", Description = "Go to line number", Key = Key.G, Ctrl = true, Shift = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.gotoDefinition", CommandName = "Go to Definition", Description = "Go to symbol definition", Key = Key.F12, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.quickOpen", CommandName = "Quick Open", Description = "Quick open file", Key = Key.P, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.nextTab", CommandName = "Next Tab", Description = "Switch to next tab", Key = Key.Tab, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.previousTab", CommandName = "Previous Tab", Description = "Switch to previous tab", Key = Key.Tab, Ctrl = true, Shift = true, Category = ShortcutCategory.Navigation },

            // Debug operations
            new() { Id = "debug.toggleBreakpoint", CommandName = "Toggle Breakpoint", Description = "Toggle breakpoint", Key = Key.F9, Category = ShortcutCategory.Debug },
            new() { Id = "debug.stepOver", CommandName = "Step Over", Description = "Step over", Key = Key.F10, Category = ShortcutCategory.Debug },
            new() { Id = "debug.stepInto", CommandName = "Step Into", Description = "Step into", Key = Key.F11, Shift = true, Category = ShortcutCategory.Debug },
            new() { Id = "debug.continue", CommandName = "Continue", Description = "Continue execution", Key = Key.F5, Shift = true, Category = ShortcutCategory.Debug },

            // Help
            new() { Id = "help.documentation", CommandName = "Documentation", Description = "Open documentation", Key = Key.F1, Category = ShortcutCategory.Help },
            new() { Id = "help.shortcuts", CommandName = "View Shortcuts", Description = "Show keyboard shortcuts reference", Key = Key.K, Ctrl = true, Category = ShortcutCategory.Help },
            new() { Id = "help.commandPalette", CommandName = "Command Palette", Description = "Open command palette", Key = Key.P, Ctrl = true, Shift = true, Category = ShortcutCategory.Help },
            new() { Id = "help.about", CommandName = "About", Description = "About MusicEngine Editor", Key = Key.F1, Ctrl = true, Category = ShortcutCategory.Help }
        };
    }

    private static IEnumerable<KeyboardShortcut>? GetPresetShortcuts(string presetName)
    {
        return presetName switch
        {
            "Default" => null, // Use defaults
            "Pro Tools" => GetProToolsPreset(),
            "Logic Pro" => GetLogicProPreset(),
            "Cubase" => GetCubasePreset(),
            "Ableton Live" => GetAbletonPreset(),
            _ => null
        };
    }

    private static IEnumerable<KeyboardShortcut> GetProToolsPreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.D3 },
            new() { Id = "transport.play", Key = Key.Space },
            new() { Id = "transport.stop", Key = Key.OemPeriod },
            new() { Id = "transport.gotoStart", Key = Key.Return },
            new() { Id = "edit.undo", Key = Key.Z, Ctrl = true },
            new() { Id = "edit.redo", Key = Key.Z, Ctrl = true, Shift = true },
            new() { Id = "tools.select", Key = Key.Escape },
            new() { Id = "tools.pencil", Key = Key.P },
            new() { Id = "tools.split", Key = Key.B },
            new() { Id = "view.mixer", Key = Key.OemPlus, Ctrl = true, Alt = true }
        };
    }

    private static IEnumerable<KeyboardShortcut> GetLogicProPreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.R },
            new() { Id = "transport.play", Key = Key.Space },
            new() { Id = "transport.stop", Key = Key.D0 },
            new() { Id = "view.mixer", Key = Key.X },
            new() { Id = "view.pianoRoll", Key = Key.P },
            new() { Id = "edit.duplicate", Key = Key.D, Ctrl = true },
            new() { Id = "transport.loop", Key = Key.L },
            new() { Id = "tools.select", Key = Key.T },
            new() { Id = "tools.pencil", Key = Key.P }
        };
    }

    private static IEnumerable<KeyboardShortcut> GetCubasePreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.NumPad0 },
            new() { Id = "transport.play", Key = Key.Space },
            new() { Id = "transport.stop", Key = Key.NumPad0 },
            new() { Id = "view.mixer", Key = Key.F3 },
            new() { Id = "view.pianoRoll", Key = Key.Return, Ctrl = true },
            new() { Id = "edit.duplicate", Key = Key.D, Ctrl = true },
            new() { Id = "transport.loop", Key = Key.Divide },
            new() { Id = "tools.select", Key = Key.D1 },
            new() { Id = "tools.pencil", Key = Key.D8 },
            new() { Id = "tools.eraser", Key = Key.D9 },
            new() { Id = "tools.split", Key = Key.D3 }
        };
    }

    private static IEnumerable<KeyboardShortcut> GetAbletonPreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.F9 },
            new() { Id = "transport.play", Key = Key.Space },
            new() { Id = "view.arrangement", Key = Key.Tab },
            new() { Id = "view.mixer", Key = Key.M, Ctrl = true, Shift = true },
            new() { Id = "edit.duplicate", Key = Key.D, Ctrl = true },
            new() { Id = "transport.loop", Key = Key.L, Ctrl = true },
            new() { Id = "edit.cut", Key = Key.X, Ctrl = true },
            new() { Id = "edit.delete", Key = Key.Delete },
            new() { Id = "transport.gotoStart", Key = Key.Home }
        };
    }

    #endregion
}

/// <summary>
/// Display item for the shortcuts list (can be header or shortcut).
/// </summary>
public class ShortcutDisplayItem : INotifyPropertyChanged
{
    private bool _isHeader;
    private bool _isShortcut;
    private string _categoryName = string.Empty;
    private int _shortcutCount;
    private KeyboardShortcut? _shortcut;
    private bool _hasConflict;
    private bool _isModified;
    private string _conflictTooltip = string.Empty;

    public bool IsHeader
    {
        get => _isHeader;
        set { _isHeader = value; OnPropertyChanged(); }
    }

    public bool IsShortcut
    {
        get => _isShortcut;
        set { _isShortcut = value; OnPropertyChanged(); }
    }

    public string CategoryName
    {
        get => _categoryName;
        set { _categoryName = value; OnPropertyChanged(); }
    }

    public int ShortcutCount
    {
        get => _shortcutCount;
        set { _shortcutCount = value; OnPropertyChanged(); }
    }

    public KeyboardShortcut? Shortcut
    {
        get => _shortcut;
        set
        {
            _shortcut = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CommandName));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(KeyParts));
            OnPropertyChanged(nameof(IsNotSet));
            OnPropertyChanged(nameof(IsCustomizable));
            OnPropertyChanged(nameof(IsNotCustomizable));
        }
    }

    public bool HasConflict
    {
        get => _hasConflict;
        set { _hasConflict = value; OnPropertyChanged(); }
    }

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); }
    }

    public string ConflictTooltip
    {
        get => _conflictTooltip;
        set { _conflictTooltip = value; OnPropertyChanged(); }
    }

    public string CommandName => Shortcut?.CommandName ?? string.Empty;
    public string Description => Shortcut?.Description ?? string.Empty;
    public bool IsNotSet => Shortcut?.Key == Key.None;
    public bool IsCustomizable => Shortcut?.IsCustomizable ?? true;
    public bool IsNotCustomizable => !IsCustomizable;

    public IEnumerable<string> KeyParts
    {
        get
        {
            if (Shortcut == null || Shortcut.Key == Key.None)
                return Enumerable.Empty<string>();

            var parts = new List<string>();
            if (Shortcut.Ctrl) parts.Add("Ctrl");
            if (Shortcut.Alt) parts.Add("Alt");
            if (Shortcut.Shift) parts.Add("Shift");
            parts.Add(GetKeyName(Shortcut.Key));
            return parts;
        }
    }

    private static string GetKeyName(Key key)
    {
        return key switch
        {
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.NumPad0 => "Num 0",
            Key.NumPad1 => "Num 1",
            Key.NumPad2 => "Num 2",
            Key.NumPad3 => "Num 3",
            Key.NumPad4 => "Num 4",
            Key.NumPad5 => "Num 5",
            Key.NumPad6 => "Num 6",
            Key.NumPad7 => "Num 7",
            Key.NumPad8 => "Num 8",
            Key.NumPad9 => "Num 9",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Escape => "Esc",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Delete => "Delete",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            _ => key.ToString()
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

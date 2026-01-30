// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Keyboard shortcut management service.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing keyboard shortcuts
/// </summary>
public class ShortcutService : IShortcutService
{
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

    private readonly List<KeyboardShortcut> _shortcuts = [];
    private readonly Dictionary<string, KeyboardShortcut> _defaultShortcuts = [];

    /// <inheritdoc />
    public event EventHandler<ShortcutTriggeredEventArgs>? ShortcutTriggered;

    /// <inheritdoc />
    public event EventHandler? ShortcutsChanged;

    /// <inheritdoc />
    public IReadOnlyList<KeyboardShortcut> Shortcuts => _shortcuts.AsReadOnly();

    public ShortcutService()
    {
        RegisterDefaultShortcuts();
    }

    /// <summary>
    /// Registers all default shortcuts
    /// </summary>
    private void RegisterDefaultShortcuts()
    {
        var defaults = new List<KeyboardShortcut>
        {
            // File operations
            new()
            {
                Id = "file.new",
                Key = Key.N,
                Ctrl = true,
                CommandName = "NewProject",
                Description = "Create a new project",
                Category = ShortcutCategory.File
            },
            new()
            {
                Id = "file.open",
                Key = Key.O,
                Ctrl = true,
                CommandName = "OpenProject",
                Description = "Open an existing project",
                Category = ShortcutCategory.File
            },
            new()
            {
                Id = "file.save",
                Key = Key.S,
                Ctrl = true,
                CommandName = "Save",
                Description = "Save the current file",
                Category = ShortcutCategory.File
            },
            new()
            {
                Id = "file.saveAll",
                Key = Key.S,
                Ctrl = true,
                Shift = true,
                CommandName = "SaveAll",
                Description = "Save all open files",
                Category = ShortcutCategory.File
            },
            new()
            {
                Id = "file.close",
                Key = Key.W,
                Ctrl = true,
                CommandName = "CloseTab",
                Description = "Close the current tab",
                Category = ShortcutCategory.File
            },
            new()
            {
                Id = "file.export",
                Key = Key.E,
                Ctrl = true,
                Shift = true,
                CommandName = "Export",
                Description = "Export audio",
                Category = ShortcutCategory.File
            },

            // Edit operations
            new()
            {
                Id = "edit.undo",
                Key = Key.Z,
                Ctrl = true,
                CommandName = "Undo",
                Description = "Undo the last action",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.redo",
                Key = Key.Y,
                Ctrl = true,
                CommandName = "Redo",
                Description = "Redo the last undone action",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.redoAlt",
                Key = Key.Z,
                Ctrl = true,
                Shift = true,
                CommandName = "Redo",
                Description = "Redo the last undone action (alternative)",
                Category = ShortcutCategory.Edit,
                IsCustomizable = false
            },
            new()
            {
                Id = "edit.cut",
                Key = Key.X,
                Ctrl = true,
                CommandName = "Cut",
                Description = "Cut selection to clipboard",
                Category = ShortcutCategory.Edit,
                IsCustomizable = false
            },
            new()
            {
                Id = "edit.copy",
                Key = Key.C,
                Ctrl = true,
                CommandName = "Copy",
                Description = "Copy selection to clipboard",
                Category = ShortcutCategory.Edit,
                IsCustomizable = false
            },
            new()
            {
                Id = "edit.paste",
                Key = Key.V,
                Ctrl = true,
                CommandName = "Paste",
                Description = "Paste from clipboard",
                Category = ShortcutCategory.Edit,
                IsCustomizable = false
            },
            new()
            {
                Id = "edit.selectAll",
                Key = Key.A,
                Ctrl = true,
                CommandName = "SelectAll",
                Description = "Select all content",
                Category = ShortcutCategory.Edit,
                IsCustomizable = false
            },
            new()
            {
                Id = "edit.duplicate",
                Key = Key.D,
                Ctrl = true,
                CommandName = "Duplicate",
                Description = "Duplicate selection or line",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.delete",
                Key = Key.Delete,
                CommandName = "Delete",
                Description = "Delete selection",
                Category = ShortcutCategory.Edit,
                IsCustomizable = false
            },
            new()
            {
                Id = "edit.find",
                Key = Key.F,
                Ctrl = true,
                CommandName = "Find",
                Description = "Open find dialog",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.replace",
                Key = Key.H,
                Ctrl = true,
                CommandName = "Replace",
                Description = "Open find and replace dialog",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.findNext",
                Key = Key.F3,
                CommandName = "FindNext",
                Description = "Find next occurrence",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.findPrevious",
                Key = Key.F3,
                Shift = true,
                CommandName = "FindPrevious",
                Description = "Find previous occurrence",
                Category = ShortcutCategory.Edit
            },
            new()
            {
                Id = "edit.comment",
                Key = Key.Oem2,
                Ctrl = true,
                CommandName = "ToggleComment",
                Description = "Toggle line comment",
                Category = ShortcutCategory.Edit
            },

            // View operations
            new()
            {
                Id = "view.zoomIn",
                Key = Key.OemPlus,
                Ctrl = true,
                CommandName = "ZoomIn",
                Description = "Zoom in",
                Category = ShortcutCategory.View
            },
            new()
            {
                Id = "view.zoomOut",
                Key = Key.OemMinus,
                Ctrl = true,
                CommandName = "ZoomOut",
                Description = "Zoom out",
                Category = ShortcutCategory.View
            },
            new()
            {
                Id = "view.resetZoom",
                Key = Key.D0,
                Ctrl = true,
                CommandName = "ResetZoom",
                Description = "Reset zoom to 100%",
                Category = ShortcutCategory.View
            },
            new()
            {
                Id = "view.fullscreen",
                Key = Key.F11,
                CommandName = "ToggleFullscreen",
                Description = "Toggle fullscreen mode",
                Category = ShortcutCategory.View
            },
            new()
            {
                Id = "view.projectExplorer",
                Key = Key.E,
                Ctrl = true,
                Alt = true,
                CommandName = "ToggleProjectExplorer",
                Description = "Toggle project explorer",
                Category = ShortcutCategory.View
            },
            new()
            {
                Id = "view.output",
                Key = Key.O,
                Ctrl = true,
                Alt = true,
                CommandName = "ToggleOutput",
                Description = "Toggle output panel",
                Category = ShortcutCategory.View
            },

            // Transport operations
            new()
            {
                Id = "transport.playStop",
                Key = Key.Space,
                CommandName = "PlayStop",
                Description = "Play / Stop",
                Category = ShortcutCategory.Transport
            },
            new()
            {
                Id = "transport.record",
                Key = Key.R,
                Ctrl = true,
                CommandName = "Record",
                Description = "Start/Stop recording",
                Category = ShortcutCategory.Transport
            },
            new()
            {
                Id = "transport.stop",
                Key = Key.OemPeriod,
                CommandName = "Stop",
                Description = "Stop playback",
                Category = ShortcutCategory.Transport
            },
            new()
            {
                Id = "transport.rewind",
                Key = Key.Home,
                CommandName = "Rewind",
                Description = "Return to start",
                Category = ShortcutCategory.Transport
            },
            new()
            {
                Id = "transport.forward",
                Key = Key.End,
                CommandName = "GoToEnd",
                Description = "Go to end",
                Category = ShortcutCategory.Transport
            },
            new()
            {
                Id = "transport.loop",
                Key = Key.L,
                Ctrl = true,
                CommandName = "ToggleLoop",
                Description = "Toggle loop mode",
                Category = ShortcutCategory.Transport
            },
            new()
            {
                Id = "transport.metronome",
                Key = Key.M,
                Ctrl = true,
                CommandName = "ToggleMetronome",
                Description = "Toggle metronome",
                Category = ShortcutCategory.Transport
            },

            // Tools operations
            new()
            {
                Id = "tools.run",
                Key = Key.F5,
                CommandName = "Run",
                Description = "Run the current script",
                Category = ShortcutCategory.Tools
            },
            new()
            {
                Id = "tools.compile",
                Key = Key.F6,
                CommandName = "Compile",
                Description = "Compile the current script",
                Category = ShortcutCategory.Tools
            },
            new()
            {
                Id = "tools.settings",
                Key = Key.OemComma,
                Ctrl = true,
                CommandName = "OpenSettings",
                Description = "Open settings",
                Category = ShortcutCategory.Tools
            },
            new()
            {
                Id = "tools.shortcuts",
                Key = Key.K,
                Ctrl = true,
                Shift = true,
                CommandName = "OpenShortcuts",
                Description = "Open keyboard shortcuts",
                Category = ShortcutCategory.Tools
            },
            new()
            {
                Id = "tools.mixer",
                Key = Key.M,
                Ctrl = true,
                Shift = true,
                CommandName = "OpenMixer",
                Description = "Open mixer",
                Category = ShortcutCategory.Tools
            },
            new()
            {
                Id = "tools.pianoRoll",
                Key = Key.P,
                Ctrl = true,
                Shift = true,
                CommandName = "OpenPianoRoll",
                Description = "Open piano roll",
                Category = ShortcutCategory.Tools
            },

            // Navigation
            new()
            {
                Id = "nav.goToLine",
                Key = Key.G,
                Ctrl = true,
                CommandName = "GoToLine",
                Description = "Go to line",
                Category = ShortcutCategory.Navigation
            },
            new()
            {
                Id = "nav.goToDefinition",
                Key = Key.F12,
                CommandName = "GoToDefinition",
                Description = "Go to definition",
                Category = ShortcutCategory.Navigation
            },
            new()
            {
                Id = "nav.quickOpen",
                Key = Key.P,
                Ctrl = true,
                CommandName = "QuickOpen",
                Description = "Quick open file",
                Category = ShortcutCategory.Navigation
            },
            new()
            {
                Id = "nav.nextTab",
                Key = Key.Tab,
                Ctrl = true,
                CommandName = "NextTab",
                Description = "Switch to next tab",
                Category = ShortcutCategory.Navigation
            },
            new()
            {
                Id = "nav.previousTab",
                Key = Key.Tab,
                Ctrl = true,
                Shift = true,
                CommandName = "PreviousTab",
                Description = "Switch to previous tab",
                Category = ShortcutCategory.Navigation
            },

            // Debug operations
            new()
            {
                Id = "debug.toggleBreakpoint",
                Key = Key.F9,
                CommandName = "ToggleBreakpoint",
                Description = "Toggle breakpoint",
                Category = ShortcutCategory.Debug
            },
            new()
            {
                Id = "debug.stepOver",
                Key = Key.F10,
                CommandName = "StepOver",
                Description = "Step over",
                Category = ShortcutCategory.Debug
            },
            new()
            {
                Id = "debug.stepInto",
                Key = Key.F11,
                Shift = true,
                CommandName = "StepInto",
                Description = "Step into",
                Category = ShortcutCategory.Debug
            },
            new()
            {
                Id = "debug.continue",
                Key = Key.F5,
                Shift = true,
                CommandName = "Continue",
                Description = "Continue execution",
                Category = ShortcutCategory.Debug
            },

            // Help
            new()
            {
                Id = "help.documentation",
                Key = Key.F1,
                CommandName = "OpenHelp",
                Description = "Open documentation",
                Category = ShortcutCategory.Help
            },
            new()
            {
                Id = "help.about",
                Key = Key.F1,
                Ctrl = true,
                CommandName = "OpenAbout",
                Description = "About MusicEngine Editor",
                Category = ShortcutCategory.Help
            }
        };

        foreach (var shortcut in defaults)
        {
            _shortcuts.Add(shortcut);
            _defaultShortcuts[shortcut.Id] = shortcut.Clone();
        }
    }

    /// <inheritdoc />
    public void RegisterShortcut(KeyboardShortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);

        if (string.IsNullOrWhiteSpace(shortcut.Id))
            throw new ArgumentException("Shortcut must have an ID", nameof(shortcut));

        var existing = _shortcuts.FirstOrDefault(s => s.Id == shortcut.Id);
        if (existing != null)
        {
            existing.UpdateKeyBinding(shortcut);
        }
        else
        {
            _shortcuts.Add(shortcut);
            if (!_defaultShortcuts.ContainsKey(shortcut.Id))
            {
                _defaultShortcuts[shortcut.Id] = shortcut.Clone();
            }
        }

        OnShortcutsChanged();
    }

    /// <inheritdoc />
    public void RegisterShortcuts(IEnumerable<KeyboardShortcut> shortcuts)
    {
        foreach (var shortcut in shortcuts)
        {
            RegisterShortcut(shortcut);
        }
    }

    /// <inheritdoc />
    public bool UnregisterShortcut(string shortcutId)
    {
        var shortcut = _shortcuts.FirstOrDefault(s => s.Id == shortcutId);
        if (shortcut != null)
        {
            _shortcuts.Remove(shortcut);
            _defaultShortcuts.Remove(shortcutId);
            OnShortcutsChanged();
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public KeyboardShortcut? GetShortcut(string shortcutId)
    {
        return _shortcuts.FirstOrDefault(s => s.Id == shortcutId);
    }

    /// <inheritdoc />
    public KeyboardShortcut? GetShortcutByCommand(string commandName)
    {
        return _shortcuts.FirstOrDefault(s => s.CommandName == commandName && s.IsEnabled);
    }

    /// <inheritdoc />
    public IReadOnlyList<KeyboardShortcut> GetShortcutsForCategory(ShortcutCategory category)
    {
        return _shortcuts.Where(s => s.Category == category).OrderBy(s => s.CommandName).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ShortcutCategory> GetCategories()
    {
        return _shortcuts.Select(s => s.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <inheritdoc />
    public bool UpdateShortcut(string shortcutId, Key key, ModifierKeys modifiers)
    {
        var shortcut = GetShortcut(shortcutId);
        if (shortcut == null || !shortcut.IsCustomizable)
            return false;

        // Check for conflicts (excluding the current shortcut)
        if (key != Key.None)
        {
            var conflict = GetConflictingShortcut(key, modifiers, shortcutId);
            if (conflict != null)
                return false;
        }

        shortcut.Key = key;
        shortcut.Modifiers = modifiers;

        OnShortcutsChanged();
        return true;
    }

    /// <inheritdoc />
    public KeyboardShortcut? GetConflictingShortcut(Key key, ModifierKeys modifiers, string? excludeShortcutId = null)
    {
        if (key == Key.None)
            return null;

        return _shortcuts.FirstOrDefault(s =>
            s.Id != excludeShortcutId &&
            s.IsEnabled &&
            s.Key == key &&
            s.Modifiers == modifiers);
    }

    /// <inheritdoc />
    public bool ProcessKeyDown(Key key, ModifierKeys modifiers)
    {
        // Ignore modifier keys alone
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            return false;
        }

        var shortcut = _shortcuts.FirstOrDefault(s => s.Matches(key, modifiers));
        if (shortcut != null)
        {
            var args = new ShortcutTriggeredEventArgs(shortcut);
            ShortcutTriggered?.Invoke(this, args);
            return args.Handled;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task LoadFromFileAsync()
    {
        try
        {
            if (!File.Exists(ShortcutsFilePath))
                return;

            var json = await File.ReadAllTextAsync(ShortcutsFilePath);
            var collection = JsonSerializer.Deserialize<ShortcutCollection>(json, JsonOptions);

            if (collection?.CustomShortcuts != null)
            {
                foreach (var customShortcut in collection.CustomShortcuts)
                {
                    var existing = _shortcuts.FirstOrDefault(s => s.Id == customShortcut.Id);
                    if (existing != null && existing.IsCustomizable)
                    {
                        existing.UpdateKeyBinding(customShortcut);
                    }
                }
            }

            OnShortcutsChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load shortcuts: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task SaveToFileAsync()
    {
        try
        {
            Directory.CreateDirectory(ShortcutsFolder);

            // Only save shortcuts that differ from defaults
            var customShortcuts = new List<KeyboardShortcut>();

            foreach (var shortcut in _shortcuts)
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
            await File.WriteAllTextAsync(ShortcutsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save shortcuts: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public void ResetToDefaults()
    {
        foreach (var shortcut in _shortcuts)
        {
            if (_defaultShortcuts.TryGetValue(shortcut.Id, out var defaultShortcut))
            {
                shortcut.UpdateKeyBinding(defaultShortcut);
            }
        }

        OnShortcutsChanged();
    }

    /// <inheritdoc />
    public void ResetShortcut(string shortcutId)
    {
        var shortcut = GetShortcut(shortcutId);
        if (shortcut != null && _defaultShortcuts.TryGetValue(shortcutId, out var defaultShortcut))
        {
            shortcut.UpdateKeyBinding(defaultShortcut);
            OnShortcutsChanged();
        }
    }

    /// <inheritdoc />
    public async Task ExportAsync(string filePath)
    {
        var collection = new ShortcutCollection
        {
            Version = 1,
            CustomShortcuts = _shortcuts.Select(s => s.Clone()).ToList()
        };

        var json = JsonSerializer.Serialize(collection, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <inheritdoc />
    public async Task ImportAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Shortcut file not found", filePath);

        var json = await File.ReadAllTextAsync(filePath);
        var collection = JsonSerializer.Deserialize<ShortcutCollection>(json, JsonOptions);

        if (collection?.CustomShortcuts != null)
        {
            foreach (var importedShortcut in collection.CustomShortcuts)
            {
                var existing = _shortcuts.FirstOrDefault(s => s.Id == importedShortcut.Id);
                if (existing != null && existing.IsCustomizable)
                {
                    existing.UpdateKeyBinding(importedShortcut);
                }
            }
        }

        OnShortcutsChanged();
    }

    private void OnShortcutsChanged()
    {
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }
}

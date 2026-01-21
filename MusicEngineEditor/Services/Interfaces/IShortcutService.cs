using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing keyboard shortcuts
/// </summary>
public interface IShortcutService
{
    /// <summary>
    /// Event raised when a shortcut is triggered
    /// </summary>
    event EventHandler<ShortcutTriggeredEventArgs>? ShortcutTriggered;

    /// <summary>
    /// Event raised when shortcuts are modified
    /// </summary>
    event EventHandler? ShortcutsChanged;

    /// <summary>
    /// Gets all registered shortcuts
    /// </summary>
    IReadOnlyList<KeyboardShortcut> Shortcuts { get; }

    /// <summary>
    /// Registers a new shortcut
    /// </summary>
    /// <param name="shortcut">The shortcut to register</param>
    void RegisterShortcut(KeyboardShortcut shortcut);

    /// <summary>
    /// Registers multiple shortcuts at once
    /// </summary>
    /// <param name="shortcuts">The shortcuts to register</param>
    void RegisterShortcuts(IEnumerable<KeyboardShortcut> shortcuts);

    /// <summary>
    /// Unregisters a shortcut by its ID
    /// </summary>
    /// <param name="shortcutId">The ID of the shortcut to unregister</param>
    /// <returns>True if the shortcut was found and removed</returns>
    bool UnregisterShortcut(string shortcutId);

    /// <summary>
    /// Gets a shortcut by its ID
    /// </summary>
    /// <param name="shortcutId">The shortcut ID</param>
    /// <returns>The shortcut, or null if not found</returns>
    KeyboardShortcut? GetShortcut(string shortcutId);

    /// <summary>
    /// Gets a shortcut by its command name
    /// </summary>
    /// <param name="commandName">The command name</param>
    /// <returns>The shortcut, or null if not found</returns>
    KeyboardShortcut? GetShortcutByCommand(string commandName);

    /// <summary>
    /// Gets all shortcuts for a specific category
    /// </summary>
    /// <param name="category">The category to filter by</param>
    /// <returns>List of shortcuts in the category</returns>
    IReadOnlyList<KeyboardShortcut> GetShortcutsForCategory(ShortcutCategory category);

    /// <summary>
    /// Gets all available categories
    /// </summary>
    /// <returns>List of categories that have shortcuts</returns>
    IReadOnlyList<ShortcutCategory> GetCategories();

    /// <summary>
    /// Updates a shortcut's key binding
    /// </summary>
    /// <param name="shortcutId">The ID of the shortcut to update</param>
    /// <param name="key">The new key</param>
    /// <param name="modifiers">The new modifiers</param>
    /// <returns>True if successful, false if there's a conflict</returns>
    bool UpdateShortcut(string shortcutId, Key key, ModifierKeys modifiers);

    /// <summary>
    /// Checks if a key combination is already in use
    /// </summary>
    /// <param name="key">The key to check</param>
    /// <param name="modifiers">The modifiers to check</param>
    /// <param name="excludeShortcutId">Optional shortcut ID to exclude from the check</param>
    /// <returns>The conflicting shortcut, or null if no conflict</returns>
    KeyboardShortcut? GetConflictingShortcut(Key key, ModifierKeys modifiers, string? excludeShortcutId = null);

    /// <summary>
    /// Processes a key event and triggers matching shortcuts
    /// </summary>
    /// <param name="key">The key that was pressed</param>
    /// <param name="modifiers">The active modifiers</param>
    /// <returns>True if a shortcut was triggered</returns>
    bool ProcessKeyDown(Key key, ModifierKeys modifiers);

    /// <summary>
    /// Loads shortcuts from the configuration file
    /// </summary>
    Task LoadFromFileAsync();

    /// <summary>
    /// Saves current shortcuts to the configuration file
    /// </summary>
    Task SaveToFileAsync();

    /// <summary>
    /// Resets all shortcuts to their default values
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// Resets a specific shortcut to its default value
    /// </summary>
    /// <param name="shortcutId">The ID of the shortcut to reset</param>
    void ResetShortcut(string shortcutId);

    /// <summary>
    /// Exports shortcuts to a file
    /// </summary>
    /// <param name="filePath">The file path to export to</param>
    Task ExportAsync(string filePath);

    /// <summary>
    /// Imports shortcuts from a file
    /// </summary>
    /// <param name="filePath">The file path to import from</param>
    Task ImportAsync(string filePath);
}

/// <summary>
/// Event arguments for when a shortcut is triggered
/// </summary>
public class ShortcutTriggeredEventArgs : EventArgs
{
    /// <summary>
    /// The shortcut that was triggered
    /// </summary>
    public KeyboardShortcut Shortcut { get; }

    /// <summary>
    /// Gets or sets whether the event was handled
    /// </summary>
    public bool Handled { get; set; }

    public ShortcutTriggeredEventArgs(KeyboardShortcut shortcut)
    {
        Shortcut = shortcut;
    }
}

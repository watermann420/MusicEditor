// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Keyboard shortcut mapping model.

using System;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace MusicEngineEditor.Models;

/// <summary>
/// Categories for keyboard shortcuts
/// </summary>
public enum ShortcutCategory
{
    File,
    Edit,
    View,
    Transport,
    Tools,
    Navigation,
    Debug,
    Help
}

/// <summary>
/// Represents a keyboard shortcut binding
/// </summary>
public class KeyboardShortcut : IEquatable<KeyboardShortcut>
{
    /// <summary>
    /// Unique identifier for the shortcut
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The main key for the shortcut
    /// </summary>
    public Key Key { get; set; } = Key.None;

    /// <summary>
    /// Whether Ctrl modifier is required
    /// </summary>
    public bool Ctrl { get; set; }

    /// <summary>
    /// Whether Alt modifier is required
    /// </summary>
    public bool Alt { get; set; }

    /// <summary>
    /// Whether Shift modifier is required
    /// </summary>
    public bool Shift { get; set; }

    /// <summary>
    /// The command name this shortcut triggers
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the shortcut does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The category this shortcut belongs to
    /// </summary>
    public ShortcutCategory Category { get; set; } = ShortcutCategory.Edit;

    /// <summary>
    /// Whether this shortcut can be customized by the user
    /// </summary>
    public bool IsCustomizable { get; set; } = true;

    /// <summary>
    /// Whether this shortcut is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the modifier keys as ModifierKeys enum
    /// </summary>
    [JsonIgnore]
    public ModifierKeys Modifiers
    {
        get
        {
            var modifiers = ModifierKeys.None;
            if (Ctrl) modifiers |= ModifierKeys.Control;
            if (Alt) modifiers |= ModifierKeys.Alt;
            if (Shift) modifiers |= ModifierKeys.Shift;
            return modifiers;
        }
        set
        {
            Ctrl = (value & ModifierKeys.Control) == ModifierKeys.Control;
            Alt = (value & ModifierKeys.Alt) == ModifierKeys.Alt;
            Shift = (value & ModifierKeys.Shift) == ModifierKeys.Shift;
        }
    }

    /// <summary>
    /// Gets a display string for the shortcut (e.g., "Ctrl+S")
    /// </summary>
    [JsonIgnore]
    public string DisplayString
    {
        get
        {
            if (Key == Key.None)
                return "Not Set";

            var sb = new StringBuilder();

            if (Ctrl) sb.Append("Ctrl+");
            if (Alt) sb.Append("Alt+");
            if (Shift) sb.Append("Shift+");

            sb.Append(GetKeyDisplayName(Key));

            return sb.ToString();
        }
    }

    /// <summary>
    /// Gets a user-friendly display name for a key
    /// </summary>
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
            _ => key.ToString()
        };
    }

    /// <summary>
    /// Checks if this shortcut matches the given key and modifiers
    /// </summary>
    public bool Matches(Key key, ModifierKeys modifiers)
    {
        if (!IsEnabled || Key == Key.None)
            return false;

        return Key == key && Modifiers == modifiers;
    }

    /// <summary>
    /// Creates a copy of this shortcut
    /// </summary>
    public KeyboardShortcut Clone()
    {
        return new KeyboardShortcut
        {
            Id = Id,
            Key = Key,
            Ctrl = Ctrl,
            Alt = Alt,
            Shift = Shift,
            CommandName = CommandName,
            Description = Description,
            Category = Category,
            IsCustomizable = IsCustomizable,
            IsEnabled = IsEnabled
        };
    }

    /// <summary>
    /// Updates this shortcut's key binding from another shortcut
    /// </summary>
    public void UpdateKeyBinding(KeyboardShortcut other)
    {
        Key = other.Key;
        Ctrl = other.Ctrl;
        Alt = other.Alt;
        Shift = other.Shift;
    }

    public bool Equals(KeyboardShortcut? other)
    {
        if (other is null) return false;
        return Key == other.Key && Ctrl == other.Ctrl && Alt == other.Alt && Shift == other.Shift;
    }

    public override bool Equals(object? obj) => Equals(obj as KeyboardShortcut);

    public override int GetHashCode() => HashCode.Combine(Key, Ctrl, Alt, Shift);

    public override string ToString() => $"{CommandName}: {DisplayString}";
}

/// <summary>
/// Represents a collection of keyboard shortcuts for serialization
/// </summary>
public class ShortcutCollection
{
    /// <summary>
    /// Version of the shortcut configuration format
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// List of custom shortcut bindings (only stores modifications from defaults)
    /// </summary>
    public List<KeyboardShortcut> CustomShortcuts { get; set; } = [];
}

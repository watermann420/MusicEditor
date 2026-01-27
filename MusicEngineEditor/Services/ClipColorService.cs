// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a predefined color with metadata.
/// </summary>
public class ClipColorEntry
{
    /// <summary>
    /// Gets or sets the color in hex format.
    /// </summary>
    public string Color { get; set; } = "#4A9EFF";

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = "Blue";

    /// <summary>
    /// Gets or sets the category (e.g., "Audio", "MIDI", "Custom").
    /// </summary>
    public string Category { get; set; } = "Default";

    /// <summary>
    /// Gets or sets whether this is a user-defined color.
    /// </summary>
    public bool IsCustom { get; set; }

    public ClipColorEntry() { }

    public ClipColorEntry(string color, string name, string category = "Default", bool isCustom = false)
    {
        Color = color;
        Name = name;
        Category = category;
        IsCustom = isCustom;
    }
}

/// <summary>
/// Content type for automatic color assignment.
/// </summary>
public enum ClipContentType
{
    Audio,
    Midi,
    Drums,
    Bass,
    Vocals,
    Synth,
    Guitar,
    Piano,
    Strings,
    Effects,
    Other
}

/// <summary>
/// Service for managing clip colors with predefined palettes and custom colors.
/// </summary>
public class ClipColorService : IClipColorService
{
    #region Fields

    private readonly Dictionary<string, string> _clipColors = new();
    private readonly List<ClipColorEntry> _customColors = new();
    private readonly Dictionary<ClipContentType, string> _contentTypeColors = new();

    #endregion

    #region Events

    public event EventHandler<ClipColorChangedEventArgs>? ClipColorChanged;
    public event EventHandler<BatchColorChangedEventArgs>? BatchColorChanged;
    public event EventHandler<CustomColorEventArgs>? CustomColorAdded;
    public event EventHandler<CustomColorEventArgs>? CustomColorRemoved;

    #endregion

    #region Predefined Palettes

    /// <summary>
    /// Gets the predefined color palette.
    /// </summary>
    public ObservableCollection<ClipColorEntry> Palette { get; } = new()
    {
        // Primary Colors
        new ClipColorEntry("#4A9EFF", "Blue", "Primary"),
        new ClipColorEntry("#FF6B6B", "Red", "Primary"),
        new ClipColorEntry("#4ECDC4", "Teal", "Primary"),
        new ClipColorEntry("#FFE66D", "Yellow", "Primary"),
        new ClipColorEntry("#A78BFA", "Purple", "Primary"),
        new ClipColorEntry("#FF8C42", "Orange", "Primary"),
        new ClipColorEntry("#6BCB77", "Green", "Primary"),
        new ClipColorEntry("#FF6B9D", "Pink", "Primary"),

        // Audio Track Colors
        new ClipColorEntry("#00CC66", "Audio Green", "Audio"),
        new ClipColorEntry("#8BC34A", "Light Green", "Audio"),
        new ClipColorEntry("#CDDC39", "Lime", "Audio"),
        new ClipColorEntry("#009688", "Teal", "Audio"),

        // MIDI Track Colors
        new ClipColorEntry("#00D9FF", "MIDI Blue", "MIDI"),
        new ClipColorEntry("#03A9F4", "Light Blue", "MIDI"),
        new ClipColorEntry("#00BCD4", "Cyan", "MIDI"),
        new ClipColorEntry("#3F51B5", "Indigo", "MIDI"),

        // Instrument Colors
        new ClipColorEntry("#E91E63", "Drums", "Instrument"),
        new ClipColorEntry("#9C27B0", "Synth", "Instrument"),
        new ClipColorEntry("#FF5722", "Bass", "Instrument"),
        new ClipColorEntry("#795548", "Guitar", "Instrument"),
        new ClipColorEntry("#607D8B", "Piano", "Instrument"),
        new ClipColorEntry("#9E9E9E", "Strings", "Instrument"),

        // Effects & Misc
        new ClipColorEntry("#FFC107", "Effects", "Effects"),
        new ClipColorEntry("#FF9800", "Ambient", "Effects"),
        new ClipColorEntry("#673AB7", "Pad", "Effects"),

        // Neutral
        new ClipColorEntry("#78909C", "Gray", "Neutral"),
        new ClipColorEntry("#455A64", "Dark Gray", "Neutral"),
        new ClipColorEntry("#B0BEC5", "Light Gray", "Neutral")
    };

    #endregion

    #region Constructor

    public ClipColorService()
    {
        InitializeContentTypeColors();
    }

    private void InitializeContentTypeColors()
    {
        _contentTypeColors[ClipContentType.Audio] = "#00CC66";
        _contentTypeColors[ClipContentType.Midi] = "#00D9FF";
        _contentTypeColors[ClipContentType.Drums] = "#E91E63";
        _contentTypeColors[ClipContentType.Bass] = "#FF5722";
        _contentTypeColors[ClipContentType.Vocals] = "#9C27B0";
        _contentTypeColors[ClipContentType.Synth] = "#673AB7";
        _contentTypeColors[ClipContentType.Guitar] = "#795548";
        _contentTypeColors[ClipContentType.Piano] = "#607D8B";
        _contentTypeColors[ClipContentType.Strings] = "#009688";
        _contentTypeColors[ClipContentType.Effects] = "#FFC107";
        _contentTypeColors[ClipContentType.Other] = "#78909C";
    }

    #endregion

    #region Color Assignment

    /// <summary>
    /// Sets the color for a clip.
    /// </summary>
    public void SetClipColor(string clipId, string color)
    {
        if (string.IsNullOrWhiteSpace(clipId) || string.IsNullOrWhiteSpace(color))
            return;

        var previousColor = _clipColors.TryGetValue(clipId, out var pc) ? pc : null;
        _clipColors[clipId] = color;

        ClipColorChanged?.Invoke(this, new ClipColorChangedEventArgs(clipId, color, previousColor));
    }

    /// <summary>
    /// Gets the color for a clip.
    /// </summary>
    public string GetClipColor(string clipId)
    {
        return _clipColors.TryGetValue(clipId, out var color) ? color : "#00CC66";
    }

    /// <summary>
    /// Gets the color for a content type.
    /// </summary>
    public string GetColorForContentType(ClipContentType contentType)
    {
        return _contentTypeColors.TryGetValue(contentType, out var color) ? color : "#78909C";
    }

    /// <summary>
    /// Sets the color for a content type.
    /// </summary>
    public void SetColorForContentType(ClipContentType contentType, string color)
    {
        _contentTypeColors[contentType] = color;
    }

    /// <summary>
    /// Assigns color to a clip based on its content type.
    /// </summary>
    public void AssignColorByContentType(string clipId, ClipContentType contentType)
    {
        var color = GetColorForContentType(contentType);
        SetClipColor(clipId, color);
    }

    /// <summary>
    /// Assigns colors to multiple clips based on content type.
    /// </summary>
    public void AssignColorsByContentType(IEnumerable<(string clipId, ClipContentType contentType)> clips)
    {
        var changes = new List<(string clipId, string color)>();

        foreach (var (clipId, contentType) in clips)
        {
            var color = GetColorForContentType(contentType);
            _clipColors[clipId] = color;
            changes.Add((clipId, color));
        }

        BatchColorChanged?.Invoke(this, new BatchColorChangedEventArgs(changes));
    }

    #endregion

    #region Batch Operations

    /// <summary>
    /// Sets the same color for multiple clips.
    /// </summary>
    public void SetBatchColor(IEnumerable<string> clipIds, string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return;

        var changes = new List<(string clipId, string color)>();

        foreach (var clipId in clipIds)
        {
            if (string.IsNullOrWhiteSpace(clipId)) continue;
            _clipColors[clipId] = color;
            changes.Add((clipId, color));
        }

        BatchColorChanged?.Invoke(this, new BatchColorChangedEventArgs(changes));
    }

    /// <summary>
    /// Sets colors from a palette for multiple clips (cycling through colors).
    /// </summary>
    public void SetBatchColorsFromPalette(IEnumerable<string> clipIds, string? category = null)
    {
        var palette = category != null
            ? Palette.Where(c => c.Category == category).ToList()
            : Palette.ToList();

        if (palette.Count == 0) palette = Palette.ToList();

        var changes = new List<(string clipId, string color)>();
        int index = 0;

        foreach (var clipId in clipIds)
        {
            if (string.IsNullOrWhiteSpace(clipId)) continue;
            var color = palette[index % palette.Count].Color;
            _clipColors[clipId] = color;
            changes.Add((clipId, color));
            index++;
        }

        BatchColorChanged?.Invoke(this, new BatchColorChangedEventArgs(changes));
    }

    /// <summary>
    /// Clears the color for a clip (returns to default).
    /// </summary>
    public void ClearClipColor(string clipId)
    {
        if (_clipColors.Remove(clipId))
        {
            ClipColorChanged?.Invoke(this, new ClipColorChangedEventArgs(clipId, "#00CC66", null));
        }
    }

    /// <summary>
    /// Clears colors for all clips.
    /// </summary>
    public void ClearAllColors()
    {
        _clipColors.Clear();
    }

    #endregion

    #region Custom Colors

    /// <summary>
    /// Adds a custom color to the palette.
    /// </summary>
    public void AddCustomColor(string color, string name)
    {
        if (string.IsNullOrWhiteSpace(color))
            return;

        // Check if already exists
        if (_customColors.Any(c => c.Color.Equals(color, StringComparison.OrdinalIgnoreCase)))
            return;

        var entry = new ClipColorEntry(color, name, "Custom", true);
        _customColors.Add(entry);
        Palette.Add(entry);

        CustomColorAdded?.Invoke(this, new CustomColorEventArgs(entry));
    }

    /// <summary>
    /// Removes a custom color from the palette.
    /// </summary>
    public bool RemoveCustomColor(string color)
    {
        var entry = _customColors.FirstOrDefault(c => c.Color.Equals(color, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        _customColors.Remove(entry);
        Palette.Remove(entry);

        CustomColorRemoved?.Invoke(this, new CustomColorEventArgs(entry));
        return true;
    }

    /// <summary>
    /// Gets all custom colors.
    /// </summary>
    public IReadOnlyList<ClipColorEntry> GetCustomColors()
    {
        return _customColors.AsReadOnly();
    }

    #endregion

    #region Palette Queries

    /// <summary>
    /// Gets colors by category.
    /// </summary>
    public IReadOnlyList<ClipColorEntry> GetColorsByCategory(string category)
    {
        return Palette.Where(c => c.Category == category).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets all categories.
    /// </summary>
    public IReadOnlyList<string> GetCategories()
    {
        return Palette.Select(c => c.Category).Distinct().ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets a random color from the palette.
    /// </summary>
    public string GetRandomColor(string? category = null)
    {
        var palette = category != null
            ? Palette.Where(c => c.Category == category).ToList()
            : Palette.ToList();

        if (palette.Count == 0) return "#4A9EFF";

        var random = new Random();
        return palette[random.Next(palette.Count)].Color;
    }

    /// <summary>
    /// Gets the next color in sequence from the palette.
    /// </summary>
    public string GetNextColor(int index, string? category = null)
    {
        var palette = category != null
            ? Palette.Where(c => c.Category == category).ToList()
            : Palette.ToList();

        if (palette.Count == 0) return "#4A9EFF";

        return palette[index % palette.Count].Color;
    }

    #endregion

    #region Color Utilities

    /// <summary>
    /// Generates a lighter shade of a color.
    /// </summary>
    public static string GetLighterShade(string hexColor, double factor = 0.3)
    {
        if (!TryParseHexColor(hexColor, out var r, out var g, out var b))
            return hexColor;

        r = (byte)Math.Min(255, r + (255 - r) * factor);
        g = (byte)Math.Min(255, g + (255 - g) * factor);
        b = (byte)Math.Min(255, b + (255 - b) * factor);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Generates a darker shade of a color.
    /// </summary>
    public static string GetDarkerShade(string hexColor, double factor = 0.3)
    {
        if (!TryParseHexColor(hexColor, out var r, out var g, out var b))
            return hexColor;

        r = (byte)(r * (1 - factor));
        g = (byte)(g * (1 - factor));
        b = (byte)(b * (1 - factor));

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Gets a contrasting text color (black or white) for a background color.
    /// </summary>
    public static string GetContrastingTextColor(string hexColor)
    {
        if (!TryParseHexColor(hexColor, out var r, out var g, out var b))
            return "#FFFFFF";

        // Calculate relative luminance
        var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
        return luminance > 0.5 ? "#000000" : "#FFFFFF";
    }

    private static bool TryParseHexColor(string hexColor, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;

        if (string.IsNullOrEmpty(hexColor))
            return false;

        var hex = hexColor.TrimStart('#');
        if (hex.Length != 6)
            return false;

        try
        {
            r = Convert.ToByte(hex.Substring(0, 2), 16);
            g = Convert.ToByte(hex.Substring(2, 2), 16);
            b = Convert.ToByte(hex.Substring(4, 2), 16);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for clip color service.
/// </summary>
public interface IClipColorService
{
    event EventHandler<ClipColorChangedEventArgs>? ClipColorChanged;
    event EventHandler<BatchColorChangedEventArgs>? BatchColorChanged;
    event EventHandler<CustomColorEventArgs>? CustomColorAdded;
    event EventHandler<CustomColorEventArgs>? CustomColorRemoved;

    ObservableCollection<ClipColorEntry> Palette { get; }

    void SetClipColor(string clipId, string color);
    string GetClipColor(string clipId);
    string GetColorForContentType(ClipContentType contentType);
    void SetColorForContentType(ClipContentType contentType, string color);
    void AssignColorByContentType(string clipId, ClipContentType contentType);

    void SetBatchColor(IEnumerable<string> clipIds, string color);
    void SetBatchColorsFromPalette(IEnumerable<string> clipIds, string? category = null);
    void ClearClipColor(string clipId);
    void ClearAllColors();

    void AddCustomColor(string color, string name);
    bool RemoveCustomColor(string color);
    IReadOnlyList<ClipColorEntry> GetCustomColors();

    IReadOnlyList<ClipColorEntry> GetColorsByCategory(string category);
    IReadOnlyList<string> GetCategories();
    string GetRandomColor(string? category = null);
    string GetNextColor(int index, string? category = null);
}

#endregion

#region Event Args

public class ClipColorChangedEventArgs : EventArgs
{
    public string ClipId { get; }
    public string NewColor { get; }
    public string? PreviousColor { get; }

    public ClipColorChangedEventArgs(string clipId, string newColor, string? previousColor)
    {
        ClipId = clipId;
        NewColor = newColor;
        PreviousColor = previousColor;
    }
}

public class BatchColorChangedEventArgs : EventArgs
{
    public IReadOnlyList<(string ClipId, string Color)> Changes { get; }

    public BatchColorChangedEventArgs(IReadOnlyList<(string ClipId, string Color)> changes)
    {
        Changes = changes;
    }
}

public class CustomColorEventArgs : EventArgs
{
    public ClipColorEntry ColorEntry { get; }

    public CustomColorEventArgs(ClipColorEntry colorEntry)
    {
        ColorEntry = colorEntry;
    }
}

#endregion

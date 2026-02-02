// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for managing track colors and color palette presets.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing track colors, presets, and automatic color assignment.
/// </summary>
public class TrackColorService
{
    #region Constants

    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "Settings");

    private static readonly string TrackColorsFile = Path.Combine(SettingsFolder, "track-colors.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current color palette swatches.
    /// </summary>
    public List<TrackColorSwatch> CurrentPalette { get; private set; }

    /// <summary>
    /// Gets the auto color mappings for track types.
    /// </summary>
    public Dictionary<TrackType, string> AutoColors { get; private set; }

    /// <summary>
    /// Gets or sets the color assignment index for cycling through palette colors.
    /// </summary>
    public int CurrentColorIndex { get; set; }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the color palette changes.
    /// </summary>
    public event EventHandler? PaletteChanged;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new TrackColorService and loads settings.
    /// </summary>
    public TrackColorService()
    {
        CurrentPalette = new List<TrackColorSwatch>(TrackColorPicker.DefaultColors);
        AutoColors = new Dictionary<TrackType, string>(TrackColorPicker.AutoColors);
        CurrentColorIndex = 0;

        EnsureDirectoryExists();
        LoadSettings();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the next color from the palette for automatic assignment.
    /// </summary>
    public string GetNextColor()
    {
        if (CurrentPalette.Count == 0)
        {
            return "#4A9EFF";
        }

        var color = CurrentPalette[CurrentColorIndex % CurrentPalette.Count].HexColor;
        CurrentColorIndex++;
        return color;
    }

    /// <summary>
    /// Gets the auto color for a specific track type.
    /// </summary>
    public string GetAutoColor(TrackType trackType)
    {
        return AutoColors.TryGetValue(trackType, out var color) ? color : "#4A9EFF";
    }

    /// <summary>
    /// Sets the auto color for a specific track type.
    /// </summary>
    public void SetAutoColor(TrackType trackType, string hexColor)
    {
        AutoColors[trackType] = hexColor;
        SaveSettings();
    }

    /// <summary>
    /// Updates the color palette with new swatches.
    /// </summary>
    public void SetPalette(List<TrackColorSwatch> swatches)
    {
        CurrentPalette = new List<TrackColorSwatch>(swatches);
        SaveSettings();
        PaletteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets the palette to default colors.
    /// </summary>
    public void ResetPaletteToDefaults()
    {
        CurrentPalette = new List<TrackColorSwatch>(TrackColorPicker.DefaultColors);
        AutoColors = new Dictionary<TrackType, string>(TrackColorPicker.AutoColors);
        CurrentColorIndex = 0;
        SaveSettings();
        PaletteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies automatic colors to a collection of tracks.
    /// </summary>
    public void ApplyAutoColorsToTracks(IEnumerable<TrackInfo> tracks)
    {
        foreach (var track in tracks)
        {
            track.Color = GetAutoColor(track.TrackType);
        }
    }

    /// <summary>
    /// Applies palette colors sequentially to a collection of tracks.
    /// </summary>
    public void ApplyPaletteToTracks(IEnumerable<TrackInfo> tracks)
    {
        CurrentColorIndex = 0;
        foreach (var track in tracks)
        {
            track.Color = GetNextColor();
        }
    }

    /// <summary>
    /// Converts a hex color string to a WPF Color.
    /// </summary>
    public static Color HexToColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.DodgerBlue;
        }
    }

    /// <summary>
    /// Converts a WPF Color to a hex string.
    /// </summary>
    public static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>
    /// Gets a contrasting text color (black or white) for a given background color.
    /// </summary>
    public static Color GetContrastingTextColor(string hexColor)
    {
        var color = HexToColor(hexColor);
        return GetContrastingTextColor(color);
    }

    /// <summary>
    /// Gets a contrasting text color (black or white) for a given background color.
    /// </summary>
    public static Color GetContrastingTextColor(Color backgroundColor)
    {
        // Calculate luminance using relative luminance formula
        var luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
        return luminance > 0.5 ? Colors.Black : Colors.White;
    }

    /// <summary>
    /// Creates a lighter or darker version of a color.
    /// </summary>
    public static Color AdjustBrightness(Color color, double factor)
    {
        var r = (byte)Math.Clamp(color.R * factor, 0, 255);
        var g = (byte)Math.Clamp(color.G * factor, 0, 255);
        var b = (byte)Math.Clamp(color.B * factor, 0, 255);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// Creates a semi-transparent version of a color.
    /// </summary>
    public static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    #endregion

    #region Private Methods

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(SettingsFolder))
        {
            Directory.CreateDirectory(SettingsFolder);
        }
    }

    private void LoadSettings()
    {
        if (!File.Exists(TrackColorsFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(TrackColorsFile);
            var settings = JsonSerializer.Deserialize<TrackColorSettings>(json, JsonOptions);

            if (settings != null)
            {
                if (settings.PaletteColors?.Count > 0)
                {
                    CurrentPalette = settings.PaletteColors
                        .Select((hex, i) => new TrackColorSwatch(hex, GetColorName(i)))
                        .ToList();
                }

                if (settings.AutoColors != null)
                {
                    foreach (var kvp in settings.AutoColors)
                    {
                        if (Enum.TryParse<TrackType>(kvp.Key, out var trackType))
                        {
                            AutoColors[trackType] = kvp.Value;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load track color settings: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new TrackColorSettings
            {
                PaletteColors = CurrentPalette.Select(s => s.HexColor).ToList(),
                AutoColors = AutoColors.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)
            };

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(TrackColorsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save track color settings: {ex.Message}");
        }
    }

    private static string GetColorName(int index)
    {
        var defaultNames = new[]
        {
            "Red", "Orange", "Yellow", "Lime", "Green", "Teal", "Cyan", "Blue",
            "Indigo", "Purple", "Magenta", "Pink", "Brown", "Gray", "White", "Black"
        };

        return index < defaultNames.Length ? defaultNames[index] : $"Color {index + 1}";
    }

    #endregion
}

/// <summary>
/// Settings model for track color persistence.
/// </summary>
internal class TrackColorSettings
{
    [JsonPropertyName("paletteColors")]
    public List<string>? PaletteColors { get; set; }

    [JsonPropertyName("autoColors")]
    public Dictionary<string, string>? AutoColors { get; set; }
}

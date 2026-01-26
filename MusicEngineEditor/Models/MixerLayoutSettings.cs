// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using System.IO;
using System.Text.Json;

namespace MusicEngineEditor.Models;

/// <summary>
/// Mixer console layout types.
/// </summary>
public enum MixerLayoutType
{
    /// <summary>
    /// Large faders with full controls.
    /// </summary>
    LargeFaders,

    /// <summary>
    /// Small/narrow faders with compact controls.
    /// </summary>
    SmallFaders,

    /// <summary>
    /// Meters only view (no faders, just level meters).
    /// </summary>
    MetersOnly
}

/// <summary>
/// Settings for mixer console layout, persisted to JSON.
/// </summary>
public class MixerLayoutSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "mixer_layout.json");

    /// <summary>
    /// Gets or sets the current layout type.
    /// </summary>
    public MixerLayoutType CurrentLayout { get; set; } = MixerLayoutType.LargeFaders;

    /// <summary>
    /// Gets or sets whether to show the bus section.
    /// </summary>
    public bool ShowBuses { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show the sends section.
    /// </summary>
    public bool ShowSends { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to show effect slots.
    /// </summary>
    public bool ShowEffectSlots { get; set; } = true;

    /// <summary>
    /// Gets or sets the channel width for large faders layout.
    /// </summary>
    public double LargeFaderWidth { get; set; } = 90;

    /// <summary>
    /// Gets or sets the channel width for small faders layout.
    /// </summary>
    public double SmallFaderWidth { get; set; } = 50;

    /// <summary>
    /// Gets or sets the channel width for meters only layout.
    /// </summary>
    public double MetersOnlyWidth { get; set; } = 36;

    /// <summary>
    /// Loads settings from the settings file.
    /// </summary>
    /// <returns>The loaded settings or a new instance if file doesn't exist.</returns>
    public static MixerLayoutSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<MixerLayoutSettings>(json) ?? new MixerLayoutSettings();
            }
        }
        catch
        {
            // Return default on any error
        }

        return new MixerLayoutSettings();
    }

    /// <summary>
    /// Saves settings to the settings file.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silently fail on save errors
        }
    }

    /// <summary>
    /// Gets the channel width for the current layout.
    /// </summary>
    public double GetChannelWidth()
    {
        return CurrentLayout switch
        {
            MixerLayoutType.LargeFaders => LargeFaderWidth,
            MixerLayoutType.SmallFaders => SmallFaderWidth,
            MixerLayoutType.MetersOnly => MetersOnlyWidth,
            _ => LargeFaderWidth
        };
    }

    /// <summary>
    /// Gets a display name for the layout type.
    /// </summary>
    public static string GetLayoutDisplayName(MixerLayoutType layout)
    {
        return layout switch
        {
            MixerLayoutType.LargeFaders => "Large Faders",
            MixerLayoutType.SmallFaders => "Small Faders",
            MixerLayoutType.MetersOnly => "Meters Only",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets all available layout types.
    /// </summary>
    public static MixerLayoutType[] AvailableLayouts =>
        [MixerLayoutType.LargeFaders, MixerLayoutType.SmallFaders, MixerLayoutType.MetersOnly];
}

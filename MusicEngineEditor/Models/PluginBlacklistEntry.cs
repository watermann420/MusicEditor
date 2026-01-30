// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a blacklisted plugin entry.
/// </summary>
public class PluginBlacklistEntry : INotifyPropertyChanged
{
    private string _pluginName = string.Empty;
    private string _pluginPath = string.Empty;
    private string _reason = string.Empty;
    private DateTime _dateAdded = DateTime.UtcNow;
    private string _pluginType = string.Empty;

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public string PluginName
    {
        get => _pluginName;
        set => SetField(ref _pluginName, value);
    }

    /// <summary>
    /// Gets or sets the full path to the plugin file.
    /// </summary>
    public string PluginPath
    {
        get => _pluginPath;
        set => SetField(ref _pluginPath, value);
    }

    /// <summary>
    /// Gets or sets the reason for blacklisting.
    /// </summary>
    public string Reason
    {
        get => _reason;
        set => SetField(ref _reason, value);
    }

    /// <summary>
    /// Gets or sets the date when the plugin was blacklisted.
    /// </summary>
    public DateTime DateAdded
    {
        get => _dateAdded;
        set => SetField(ref _dateAdded, value);
    }

    /// <summary>
    /// Gets or sets the plugin type (VST2, VST3, etc.).
    /// </summary>
    public string PluginType
    {
        get => _pluginType;
        set => SetField(ref _pluginType, value);
    }

    /// <summary>
    /// Gets the formatted date string for display.
    /// </summary>
    public string DateAddedDisplay => DateAdded.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>
    /// Gets the file name from the path.
    /// </summary>
    public string FileName => Path.GetFileName(PluginPath);

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a field value and raises PropertyChanged if the value changed.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Container for plugin blacklist data, persisted to JSON.
/// </summary>
public class PluginBlacklistData
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "plugin_blacklist.json");

    /// <summary>
    /// Gets or sets the list of blacklisted plugins.
    /// </summary>
    public List<PluginBlacklistEntry> BlacklistedPlugins { get; set; } = [];

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Loads the blacklist from the settings file.
    /// </summary>
    /// <returns>The loaded blacklist or a new instance if file doesn't exist.</returns>
    public static PluginBlacklistData Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<PluginBlacklistData>(json) ?? new PluginBlacklistData();
            }
        }
        catch
        {
            // Return default on any error
        }

        return new PluginBlacklistData();
    }

    /// <summary>
    /// Saves the blacklist to the settings file.
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

            LastUpdated = DateTime.UtcNow;
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
    /// Adds a plugin to the blacklist.
    /// </summary>
    /// <param name="entry">The blacklist entry to add.</param>
    public void Add(PluginBlacklistEntry entry)
    {
        if (entry == null) return;

        // Check if already blacklisted
        foreach (var existing in BlacklistedPlugins)
        {
            if (existing.PluginPath.Equals(entry.PluginPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        BlacklistedPlugins.Add(entry);
        Save();
    }

    /// <summary>
    /// Removes a plugin from the blacklist.
    /// </summary>
    /// <param name="entry">The blacklist entry to remove.</param>
    public void Remove(PluginBlacklistEntry entry)
    {
        if (BlacklistedPlugins.Remove(entry))
        {
            Save();
        }
    }

    /// <summary>
    /// Checks if a plugin path is blacklisted.
    /// </summary>
    /// <param name="pluginPath">The plugin path to check.</param>
    /// <returns>True if blacklisted, false otherwise.</returns>
    public bool IsBlacklisted(string pluginPath)
    {
        if (string.IsNullOrEmpty(pluginPath)) return false;

        foreach (var entry in BlacklistedPlugins)
        {
            if (entry.PluginPath.Equals(pluginPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears all blacklisted plugins.
    /// </summary>
    public void ClearAll()
    {
        BlacklistedPlugins.Clear();
        Save();
    }
}

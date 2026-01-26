// MusicEngineEditor - Browser Favorites Model
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MusicEngineEditor.Models;

/// <summary>
/// Model for storing and managing browser favorites (presets, samples).
/// Persisted to local JSON settings file.
/// </summary>
public class BrowserFavorites
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "favorites.json");

    /// <summary>
    /// Gets or sets the list of favorite preset IDs.
    /// </summary>
    public List<string> FavoritePresetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of favorite sample paths.
    /// </summary>
    public List<string> FavoriteSamplePaths { get; set; } = [];

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Loads favorites from the settings file.
    /// </summary>
    /// <returns>The loaded favorites or a new instance if file doesn't exist.</returns>
    public static BrowserFavorites Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<BrowserFavorites>(json) ?? new BrowserFavorites();
            }
        }
        catch
        {
            // Return default on any error
        }

        return new BrowserFavorites();
    }

    /// <summary>
    /// Saves favorites to the settings file.
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
    /// Adds a preset to favorites.
    /// </summary>
    /// <param name="presetId">The preset ID to add.</param>
    public void AddPresetFavorite(string presetId)
    {
        if (!string.IsNullOrEmpty(presetId) && !FavoritePresetIds.Contains(presetId))
        {
            FavoritePresetIds.Add(presetId);
            Save();
        }
    }

    /// <summary>
    /// Removes a preset from favorites.
    /// </summary>
    /// <param name="presetId">The preset ID to remove.</param>
    public void RemovePresetFavorite(string presetId)
    {
        if (FavoritePresetIds.Remove(presetId))
        {
            Save();
        }
    }

    /// <summary>
    /// Toggles a preset's favorite status.
    /// </summary>
    /// <param name="presetId">The preset ID to toggle.</param>
    /// <returns>True if now a favorite, false otherwise.</returns>
    public bool TogglePresetFavorite(string presetId)
    {
        if (IsPresetFavorite(presetId))
        {
            RemovePresetFavorite(presetId);
            return false;
        }
        else
        {
            AddPresetFavorite(presetId);
            return true;
        }
    }

    /// <summary>
    /// Checks if a preset is a favorite.
    /// </summary>
    /// <param name="presetId">The preset ID to check.</param>
    /// <returns>True if favorite, false otherwise.</returns>
    public bool IsPresetFavorite(string presetId)
    {
        return !string.IsNullOrEmpty(presetId) && FavoritePresetIds.Contains(presetId);
    }

    /// <summary>
    /// Adds a sample to favorites.
    /// </summary>
    /// <param name="samplePath">The sample path to add.</param>
    public void AddSampleFavorite(string samplePath)
    {
        if (!string.IsNullOrEmpty(samplePath) && !FavoriteSamplePaths.Contains(samplePath))
        {
            FavoriteSamplePaths.Add(samplePath);
            Save();
        }
    }

    /// <summary>
    /// Removes a sample from favorites.
    /// </summary>
    /// <param name="samplePath">The sample path to remove.</param>
    public void RemoveSampleFavorite(string samplePath)
    {
        if (FavoriteSamplePaths.Remove(samplePath))
        {
            Save();
        }
    }

    /// <summary>
    /// Toggles a sample's favorite status.
    /// </summary>
    /// <param name="samplePath">The sample path to toggle.</param>
    /// <returns>True if now a favorite, false otherwise.</returns>
    public bool ToggleSampleFavorite(string samplePath)
    {
        if (IsSampleFavorite(samplePath))
        {
            RemoveSampleFavorite(samplePath);
            return false;
        }
        else
        {
            AddSampleFavorite(samplePath);
            return true;
        }
    }

    /// <summary>
    /// Checks if a sample is a favorite.
    /// </summary>
    /// <param name="samplePath">The sample path to check.</param>
    /// <returns>True if favorite, false otherwise.</returns>
    public bool IsSampleFavorite(string samplePath)
    {
        return !string.IsNullOrEmpty(samplePath) && FavoriteSamplePaths.Contains(samplePath);
    }

    /// <summary>
    /// Clears all favorites.
    /// </summary>
    public void ClearAll()
    {
        FavoritePresetIds.Clear();
        FavoriteSamplePaths.Clear();
        Save();
    }
}

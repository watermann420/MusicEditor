// MusicEngineEditor - Recent Project Entry Model
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Text.Json.Serialization;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents an entry in the recent projects list.
/// </summary>
public class RecentProjectEntry
{
    /// <summary>
    /// Gets or sets the full path to the project file.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the project.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the project was last opened.
    /// </summary>
    [JsonPropertyName("lastOpened")]
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the relative time string for display (e.g., "2 hours ago").
    /// </summary>
    [JsonIgnore]
    public string LastOpenedRelative
    {
        get
        {
            var elapsed = DateTime.UtcNow - LastOpened;

            if (elapsed.TotalMinutes < 1)
                return "Just now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes == 1 ? "" : "s")} ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 7)
                return $"{(int)elapsed.TotalDays} day{((int)elapsed.TotalDays == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 30)
                return $"{(int)(elapsed.TotalDays / 7)} week{((int)(elapsed.TotalDays / 7) == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 365)
                return $"{(int)(elapsed.TotalDays / 30)} month{((int)(elapsed.TotalDays / 30) == 1 ? "" : "s")} ago";

            return $"{(int)(elapsed.TotalDays / 365)} year{((int)(elapsed.TotalDays / 365) == 1 ? "" : "s")} ago";
        }
    }

    /// <summary>
    /// Gets whether the project file exists on disk.
    /// </summary>
    [JsonIgnore]
    public bool Exists => System.IO.File.Exists(Path);
}

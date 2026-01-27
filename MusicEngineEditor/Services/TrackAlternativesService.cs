// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

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

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents an alternative version of a track (A/B comparison).
/// </summary>
public class TrackAlternative : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _trackId = string.Empty;
    private string _name = "Version A";
    private string _description = string.Empty;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _modifiedAt = DateTime.UtcNow;
    private bool _isActive;
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the parent track ID.
    /// </summary>
    public string TrackId
    {
        get => _trackId;
        set { _trackId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the alternative name (e.g., "Version A", "With Reverb").
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set { _createdAt = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set { _modifiedAt = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether this is the currently active alternative.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the color for visual identification.
    /// </summary>
    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the track content data.
    /// </summary>
    public TrackAlternativeContent? Content { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Content data stored in a track alternative.
/// </summary>
public class TrackAlternativeContent
{
    /// <summary>
    /// Gets or sets the clips in this alternative.
    /// </summary>
    public List<TrackAltClipData> Clips { get; set; } = new();

    /// <summary>
    /// Gets or sets the automation data.
    /// </summary>
    public List<TrackAltAutomationData> Automation { get; set; } = new();

    /// <summary>
    /// Gets or sets the mixer settings.
    /// </summary>
    public TrackAltMixerData? MixerSettings { get; set; }

    /// <summary>
    /// Gets or sets the effect chain.
    /// </summary>
    public List<TrackAltEffectData> Effects { get; set; } = new();

    /// <summary>
    /// Creates a deep copy of the content.
    /// </summary>
    public TrackAlternativeContent Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<TrackAlternativeContent>(json) ?? new TrackAlternativeContent();
    }
}

public class TrackAltClipData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double StartBeat { get; set; }
    public double LengthBeats { get; set; }
    public string? FilePath { get; set; }
    public bool IsMuted { get; set; }
    public string Color { get; set; } = "#00CC66";
    public double FadeIn { get; set; }
    public double FadeOut { get; set; }
    public double GainDb { get; set; }
}

public class TrackAltAutomationData
{
    public string ParameterId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public List<TrackAltAutomationPoint> Points { get; set; } = new();
}

public class TrackAltAutomationPoint
{
    public double Beat { get; set; }
    public double Value { get; set; }
    public int CurveType { get; set; }
}

public class TrackAltMixerData
{
    public float Volume { get; set; } = 0.8f;
    public float Pan { get; set; }
    public bool IsMuted { get; set; }
    public bool IsSoloed { get; set; }
    public string? OutputBusId { get; set; }
}

public class TrackAltEffectData
{
    public string EffectType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsBypassed { get; set; }
    public float Mix { get; set; } = 1.0f;
    public Dictionary<string, float> Parameters { get; set; } = new();
}

/// <summary>
/// Service for managing A/B track alternative versions.
/// </summary>
public class TrackAlternativesService : ITrackAlternativesService
{
    #region Fields

    private readonly Dictionary<string, List<TrackAlternative>> _trackAlternatives = new();
    private readonly Dictionary<string, string> _activeAlternatives = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] DefaultColors = new[]
    {
        "#4A9EFF", "#FF6B6B", "#4ECDC4", "#FFE66D",
        "#A78BFA", "#FF8C42", "#6BCB77", "#FF6B9D"
    };

    #endregion

    #region Events

    public event EventHandler<TrackAltChangedEventArgs>? AlternativeCreated;
    public event EventHandler<TrackAltChangedEventArgs>? AlternativeDeleted;
    public event EventHandler<TrackAltChangedEventArgs>? AlternativeRenamed;
    public event EventHandler<TrackAltSwitchedEventArgs>? AlternativeSwitched;
    public event EventHandler<TrackAltCopiedEventArgs>? ContentCopied;

    #endregion

    #region Alternative Management

    /// <summary>
    /// Creates a new alternative for a track.
    /// </summary>
    public TrackAlternative CreateAlternative(string trackId, string name, TrackAlternativeContent? content = null)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            throw new ArgumentException("Track ID cannot be empty", nameof(trackId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
        {
            alternatives = new List<TrackAlternative>();
            _trackAlternatives[trackId] = alternatives;
        }

        // Generate unique name
        var finalName = GetUniqueName(alternatives, name);

        // Assign color based on index
        var colorIndex = alternatives.Count % DefaultColors.Length;

        var alternative = new TrackAlternative
        {
            TrackId = trackId,
            Name = finalName,
            Content = content ?? new TrackAlternativeContent(),
            Color = DefaultColors[colorIndex],
            IsActive = alternatives.Count == 0 // First alternative is active by default
        };

        alternatives.Add(alternative);

        // Set as active if first
        if (alternatives.Count == 1)
        {
            _activeAlternatives[trackId] = alternative.Id;
        }

        AlternativeCreated?.Invoke(this, new TrackAltChangedEventArgs(trackId, alternative));

        return alternative;
    }

    /// <summary>
    /// Creates a duplicate of an existing alternative.
    /// </summary>
    public TrackAlternative DuplicateAlternative(string trackId, string sourceAlternativeId, string newName)
    {
        var source = GetAlternative(trackId, sourceAlternativeId);
        if (source == null)
            throw new InvalidOperationException($"Alternative '{sourceAlternativeId}' not found");

        var contentCopy = source.Content?.Clone();
        return CreateAlternative(trackId, newName, contentCopy);
    }

    /// <summary>
    /// Deletes an alternative.
    /// </summary>
    public bool DeleteAlternative(string trackId, string alternativeId)
    {
        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return false;

        var alternative = alternatives.FirstOrDefault(a => a.Id == alternativeId);
        if (alternative == null)
            return false;

        // Cannot delete if it's the only alternative
        if (alternatives.Count <= 1)
            return false;

        alternatives.Remove(alternative);

        // If deleting active alternative, switch to first available
        if (_activeAlternatives.TryGetValue(trackId, out var activeId) && activeId == alternativeId)
        {
            var newActive = alternatives.First();
            newActive.IsActive = true;
            _activeAlternatives[trackId] = newActive.Id;
            AlternativeSwitched?.Invoke(this, new TrackAltSwitchedEventArgs(trackId, alternative, newActive));
        }

        AlternativeDeleted?.Invoke(this, new TrackAltChangedEventArgs(trackId, alternative));

        return true;
    }

    /// <summary>
    /// Renames an alternative.
    /// </summary>
    public bool RenameAlternative(string trackId, string alternativeId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        var alternative = GetAlternative(trackId, alternativeId);
        if (alternative == null)
            return false;

        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return false;

        var finalName = GetUniqueName(alternatives, newName, alternativeId);
        alternative.Name = finalName;
        alternative.ModifiedAt = DateTime.UtcNow;

        AlternativeRenamed?.Invoke(this, new TrackAltChangedEventArgs(trackId, alternative));

        return true;
    }

    /// <summary>
    /// Gets a specific alternative.
    /// </summary>
    public TrackAlternative? GetAlternative(string trackId, string alternativeId)
    {
        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return null;

        return alternatives.FirstOrDefault(a => a.Id == alternativeId);
    }

    /// <summary>
    /// Gets all alternatives for a track.
    /// </summary>
    public IReadOnlyList<TrackAlternative> GetAlternatives(string trackId)
    {
        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return Array.Empty<TrackAlternative>();

        return alternatives.AsReadOnly();
    }

    #endregion

    #region Switching

    /// <summary>
    /// Switches to a different alternative (quick A/B switch).
    /// </summary>
    public TrackAlternative? SwitchToAlternative(string trackId, string alternativeId)
    {
        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return null;

        var newAlternative = alternatives.FirstOrDefault(a => a.Id == alternativeId);
        if (newAlternative == null)
            return null;

        TrackAlternative? previousAlternative = null;

        // Deactivate current
        if (_activeAlternatives.TryGetValue(trackId, out var currentId))
        {
            previousAlternative = alternatives.FirstOrDefault(a => a.Id == currentId);
            if (previousAlternative != null)
            {
                previousAlternative.IsActive = false;
            }
        }

        // Activate new
        newAlternative.IsActive = true;
        _activeAlternatives[trackId] = alternativeId;

        AlternativeSwitched?.Invoke(this, new TrackAltSwitchedEventArgs(trackId, previousAlternative, newAlternative));

        return newAlternative;
    }

    /// <summary>
    /// Gets the currently active alternative for a track.
    /// </summary>
    public TrackAlternative? GetActiveAlternative(string trackId)
    {
        if (!_activeAlternatives.TryGetValue(trackId, out var activeId))
            return null;

        return GetAlternative(trackId, activeId);
    }

    /// <summary>
    /// Cycles to the next alternative.
    /// </summary>
    public TrackAlternative? CycleToNextAlternative(string trackId)
    {
        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return null;

        if (alternatives.Count <= 1)
            return alternatives.FirstOrDefault();

        var currentIndex = -1;
        if (_activeAlternatives.TryGetValue(trackId, out var activeId))
        {
            currentIndex = alternatives.FindIndex(a => a.Id == activeId);
        }

        var nextIndex = (currentIndex + 1) % alternatives.Count;
        return SwitchToAlternative(trackId, alternatives[nextIndex].Id);
    }

    /// <summary>
    /// Cycles to the previous alternative.
    /// </summary>
    public TrackAlternative? CycleToPreviousAlternative(string trackId)
    {
        if (!_trackAlternatives.TryGetValue(trackId, out var alternatives))
            return null;

        if (alternatives.Count <= 1)
            return alternatives.FirstOrDefault();

        var currentIndex = 0;
        if (_activeAlternatives.TryGetValue(trackId, out var activeId))
        {
            currentIndex = alternatives.FindIndex(a => a.Id == activeId);
        }

        var prevIndex = (currentIndex - 1 + alternatives.Count) % alternatives.Count;
        return SwitchToAlternative(trackId, alternatives[prevIndex].Id);
    }

    #endregion

    #region Content Operations

    /// <summary>
    /// Copies content from one alternative to another.
    /// </summary>
    public bool CopyContent(string trackId, string sourceAlternativeId, string targetAlternativeId)
    {
        var source = GetAlternative(trackId, sourceAlternativeId);
        var target = GetAlternative(trackId, targetAlternativeId);

        if (source == null || target == null || source.Content == null)
            return false;

        target.Content = source.Content.Clone();
        target.ModifiedAt = DateTime.UtcNow;

        ContentCopied?.Invoke(this, new TrackAltCopiedEventArgs(trackId, source, target));

        return true;
    }

    /// <summary>
    /// Updates the content of an alternative.
    /// </summary>
    public bool UpdateContent(string trackId, string alternativeId, TrackAlternativeContent content)
    {
        var alternative = GetAlternative(trackId, alternativeId);
        if (alternative == null)
            return false;

        alternative.Content = content;
        alternative.ModifiedAt = DateTime.UtcNow;

        return true;
    }

    /// <summary>
    /// Saves current track state to the active alternative.
    /// </summary>
    public bool SaveCurrentToActive(string trackId, TrackAlternativeContent content)
    {
        var active = GetActiveAlternative(trackId);
        if (active == null)
        {
            // Create first alternative
            CreateAlternative(trackId, "Version A", content);
            return true;
        }

        active.Content = content;
        active.ModifiedAt = DateTime.UtcNow;
        return true;
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Saves all alternatives to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var data = new TrackAltPersistenceData
        {
            TrackAlternatives = _trackAlternatives,
            ActiveAlternatives = new Dictionary<string, string>(_activeAlternatives)
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads alternatives from a file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<TrackAltPersistenceData>(json, JsonOptions);

        if (data != null)
        {
            _trackAlternatives.Clear();
            _activeAlternatives.Clear();

            foreach (var kvp in data.TrackAlternatives)
            {
                _trackAlternatives[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in data.ActiveAlternatives)
            {
                _activeAlternatives[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Clears all alternative data.
    /// </summary>
    public void Clear()
    {
        _trackAlternatives.Clear();
        _activeAlternatives.Clear();
    }

    #endregion

    #region Private Methods

    private static string GetUniqueName(List<TrackAlternative> alternatives, string baseName, string? excludeId = null)
    {
        var name = baseName;
        var counter = 1;

        while (alternatives.Any(a => a.Name == name && a.Id != excludeId))
        {
            name = $"{baseName} ({counter++})";
        }

        return name;
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for track alternatives service.
/// </summary>
public interface ITrackAlternativesService
{
    event EventHandler<TrackAltChangedEventArgs>? AlternativeCreated;
    event EventHandler<TrackAltChangedEventArgs>? AlternativeDeleted;
    event EventHandler<TrackAltChangedEventArgs>? AlternativeRenamed;
    event EventHandler<TrackAltSwitchedEventArgs>? AlternativeSwitched;
    event EventHandler<TrackAltCopiedEventArgs>? ContentCopied;

    TrackAlternative CreateAlternative(string trackId, string name, TrackAlternativeContent? content = null);
    TrackAlternative DuplicateAlternative(string trackId, string sourceAlternativeId, string newName);
    bool DeleteAlternative(string trackId, string alternativeId);
    bool RenameAlternative(string trackId, string alternativeId, string newName);
    TrackAlternative? GetAlternative(string trackId, string alternativeId);
    IReadOnlyList<TrackAlternative> GetAlternatives(string trackId);

    TrackAlternative? SwitchToAlternative(string trackId, string alternativeId);
    TrackAlternative? GetActiveAlternative(string trackId);
    TrackAlternative? CycleToNextAlternative(string trackId);
    TrackAlternative? CycleToPreviousAlternative(string trackId);

    bool CopyContent(string trackId, string sourceAlternativeId, string targetAlternativeId);
    bool UpdateContent(string trackId, string alternativeId, TrackAlternativeContent content);
    bool SaveCurrentToActive(string trackId, TrackAlternativeContent content);

    Task SaveToFileAsync(string filePath);
    Task LoadFromFileAsync(string filePath);
    void Clear();
}

#endregion

#region Event Args

public class TrackAltChangedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackAlternative Alternative { get; }

    public TrackAltChangedEventArgs(string trackId, TrackAlternative alternative)
    {
        TrackId = trackId;
        Alternative = alternative;
    }
}

public class TrackAltSwitchedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackAlternative? PreviousAlternative { get; }
    public TrackAlternative NewAlternative { get; }

    public TrackAltSwitchedEventArgs(string trackId, TrackAlternative? previous, TrackAlternative newAlt)
    {
        TrackId = trackId;
        PreviousAlternative = previous;
        NewAlternative = newAlt;
    }
}

public class TrackAltCopiedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackAlternative Source { get; }
    public TrackAlternative Target { get; }

    public TrackAltCopiedEventArgs(string trackId, TrackAlternative source, TrackAlternative target)
    {
        TrackId = trackId;
        Source = source;
        Target = target;
    }
}

#endregion

#region Persistence Data

internal class TrackAltPersistenceData
{
    public Dictionary<string, List<TrackAlternative>> TrackAlternatives { get; set; } = new();
    public Dictionary<string, string> ActiveAlternatives { get; set; } = new();
}

#endregion

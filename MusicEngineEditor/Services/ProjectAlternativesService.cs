using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing multiple alternative versions within a single project file.
/// Allows creating, switching, comparing, and merging different versions of the project.
/// </summary>
public sealed class ProjectAlternativesService : INotifyPropertyChanged
{
    private static readonly Lazy<ProjectAlternativesService> _instance = new(
        () => new ProjectAlternativesService(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

    public static ProjectAlternativesService Instance => _instance.Value;

    private readonly Dictionary<string, ProjectAlternativeCollection> _projectAlternatives = new();
    private readonly Dictionary<string, string> _activeAlternatives = new();
    private readonly List<AlternativeHistoryEntry> _globalHistory = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ProjectAltChangedEventArgs>? AlternativeCreated;
    public event EventHandler<ProjectAltChangedEventArgs>? AlternativeDeleted;
    public event EventHandler<ProjectAltSwitchedEventArgs>? AlternativeSwitched;
    public event EventHandler<AlternativeCompareEventArgs>? AlternativesCompared;
    public event EventHandler<AlternativeMergeEventArgs>? AlternativesMerged;

    private ProjectAlternativesService()
    {
    }

    #region Public Methods

    /// <summary>
    /// Creates a new alternative for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="name">The name of the alternative.</param>
    /// <param name="data">The alternative data.</param>
    /// <param name="description">Optional description.</param>
    /// <returns>The created alternative.</returns>
    public ProjectAlternative CreateAlternative(string projectId, string name, AlternativeData data, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("Project ID cannot be empty", nameof(projectId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Alternative name cannot be empty", nameof(name));

        var collection = GetOrCreateCollection(projectId);
        var finalName = GetUniqueName(collection, name);

        var alternative = new ProjectAlternative
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            Name = finalName,
            Description = description ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Data = data,
            Color = GetNextColor(collection.Alternatives.Count)
        };

        collection.Alternatives.Add(alternative);

        // Set as active if first alternative
        if (collection.Alternatives.Count == 1 || !_activeAlternatives.ContainsKey(projectId))
        {
            _activeAlternatives[projectId] = alternative.Id;
        }

        AddHistoryEntry(projectId, alternative.Id, AlternativeAction.Created, $"Created alternative '{finalName}'");

        AlternativeCreated?.Invoke(this, new ProjectAltChangedEventArgs(projectId, alternative));
        NotifyPropertyChanged(nameof(HasAlternatives));

        return alternative;
    }

    /// <summary>
    /// Creates a duplicate of an existing alternative.
    /// </summary>
    public ProjectAlternative DuplicateAlternative(string projectId, string sourceAlternativeId, string newName)
    {
        var source = GetAlternative(projectId, sourceAlternativeId);
        if (source == null)
            throw new InvalidOperationException($"Alternative '{sourceAlternativeId}' not found");

        var dataCopy = source.Data?.Clone() ?? new AlternativeData();
        return CreateAlternative(projectId, newName, dataCopy, $"Duplicated from '{source.Name}'");
    }

    /// <summary>
    /// Switches to a different alternative.
    /// </summary>
    public ProjectAlternative? SwitchToAlternative(string projectId, string alternativeId)
    {
        var alternative = GetAlternative(projectId, alternativeId);
        if (alternative == null)
            return null;

        ProjectAlternative? previous = null;
        if (_activeAlternatives.TryGetValue(projectId, out var previousId))
        {
            previous = GetAlternative(projectId, previousId);
        }

        _activeAlternatives[projectId] = alternativeId;

        AddHistoryEntry(projectId, alternativeId, AlternativeAction.Switched, $"Switched to '{alternative.Name}'");

        AlternativeSwitched?.Invoke(this, new ProjectAltSwitchedEventArgs(projectId, previous, alternative));

        return alternative;
    }

    /// <summary>
    /// Deletes an alternative.
    /// </summary>
    public bool DeleteAlternative(string projectId, string alternativeId)
    {
        if (!_projectAlternatives.TryGetValue(projectId, out var collection))
            return false;

        var alternative = collection.Alternatives.FirstOrDefault(a => a.Id == alternativeId);
        if (alternative == null)
            return false;

        // Cannot delete if it's the only alternative
        if (collection.Alternatives.Count <= 1)
            return false;

        collection.Alternatives.Remove(alternative);

        // If deleting active alternative, switch to another
        if (_activeAlternatives.TryGetValue(projectId, out var activeId) && activeId == alternativeId)
        {
            var newActive = collection.Alternatives.First();
            _activeAlternatives[projectId] = newActive.Id;
            AlternativeSwitched?.Invoke(this, new ProjectAltSwitchedEventArgs(projectId, alternative, newActive));
        }

        AddHistoryEntry(projectId, alternativeId, AlternativeAction.Deleted, $"Deleted alternative '{alternative.Name}'");

        AlternativeDeleted?.Invoke(this, new ProjectAltChangedEventArgs(projectId, alternative));
        NotifyPropertyChanged(nameof(HasAlternatives));

        return true;
    }

    /// <summary>
    /// Compares two alternatives and returns the differences.
    /// </summary>
    public AlternativeComparison CompareAlternatives(string projectId, string alternativeId1, string alternativeId2)
    {
        var alt1 = GetAlternative(projectId, alternativeId1);
        var alt2 = GetAlternative(projectId, alternativeId2);

        if (alt1 == null || alt2 == null)
            throw new InvalidOperationException("One or both alternatives not found");

        var comparison = new AlternativeComparison
        {
            ProjectId = projectId,
            Alternative1 = alt1,
            Alternative2 = alt2,
            ComparedAt = DateTime.UtcNow
        };

        var data1 = alt1.Data;
        var data2 = alt2.Data;

        if (data1 != null && data2 != null)
        {
            // Compare tracks
            if (data1.TrackCount != data2.TrackCount)
            {
                comparison.Differences.Add(new AlternativeDifference
                {
                    Category = "Tracks",
                    Property = "Track Count",
                    Value1 = $"{data1.TrackCount} tracks",
                    Value2 = $"{data2.TrackCount} tracks"
                });
            }

            // Compare clips
            if (data1.ClipCount != data2.ClipCount)
            {
                comparison.Differences.Add(new AlternativeDifference
                {
                    Category = "Clips",
                    Property = "Clip Count",
                    Value1 = $"{data1.ClipCount} clips",
                    Value2 = $"{data2.ClipCount} clips"
                });
            }

            // Compare mixer settings
            if (data1.MasterVolume != data2.MasterVolume)
            {
                comparison.Differences.Add(new AlternativeDifference
                {
                    Category = "Mixer",
                    Property = "Master Volume",
                    Value1 = $"{data1.MasterVolume:F1} dB",
                    Value2 = $"{data2.MasterVolume:F1} dB"
                });
            }

            // Compare tempo
            if (Math.Abs(data1.Tempo - data2.Tempo) > 0.01)
            {
                comparison.Differences.Add(new AlternativeDifference
                {
                    Category = "Project",
                    Property = "Tempo",
                    Value1 = $"{data1.Tempo:F1} BPM",
                    Value2 = $"{data2.Tempo:F1} BPM"
                });
            }

            // Compare effects
            if (data1.EffectCount != data2.EffectCount)
            {
                comparison.Differences.Add(new AlternativeDifference
                {
                    Category = "Effects",
                    Property = "Effect Count",
                    Value1 = $"{data1.EffectCount} effects",
                    Value2 = $"{data2.EffectCount} effects"
                });
            }

            // Compare automation
            if (data1.AutomationPointCount != data2.AutomationPointCount)
            {
                comparison.Differences.Add(new AlternativeDifference
                {
                    Category = "Automation",
                    Property = "Automation Points",
                    Value1 = $"{data1.AutomationPointCount} points",
                    Value2 = $"{data2.AutomationPointCount} points"
                });
            }
        }

        AlternativesCompared?.Invoke(this, new AlternativeCompareEventArgs(comparison));

        return comparison;
    }

    /// <summary>
    /// Merges changes from one alternative into another.
    /// </summary>
    public AlternativeMergeResult MergeAlternatives(string projectId, string sourceId, string targetId, MergeOptions options)
    {
        var source = GetAlternative(projectId, sourceId);
        var target = GetAlternative(projectId, targetId);

        if (source == null || target == null)
            throw new InvalidOperationException("One or both alternatives not found");

        var result = new AlternativeMergeResult
        {
            ProjectId = projectId,
            SourceId = sourceId,
            TargetId = targetId,
            MergedAt = DateTime.UtcNow,
            Success = true
        };

        var sourceData = source.Data;
        var targetData = target.Data;

        if (sourceData == null || targetData == null)
        {
            result.Success = false;
            result.ErrorMessage = "Missing data in source or target alternative";
            return result;
        }

        try
        {
            // Merge based on options
            if (options.MergeTracks)
            {
                targetData.Tracks = sourceData.Tracks?.ToList() ?? new List<AlternativeTrackData>();
                result.MergedItems.Add("Tracks");
            }

            if (options.MergeClips)
            {
                targetData.Clips = sourceData.Clips?.ToList() ?? new List<ProjectAltClipData>();
                result.MergedItems.Add("Clips");
            }

            if (options.MergeMixerSettings)
            {
                targetData.MasterVolume = sourceData.MasterVolume;
                targetData.MixerSettings = sourceData.MixerSettings?.ToDictionary(kv => kv.Key, kv => kv.Value);
                result.MergedItems.Add("Mixer Settings");
            }

            if (options.MergeEffects)
            {
                targetData.Effects = sourceData.Effects?.ToList() ?? new List<ProjectAltEffectData>();
                result.MergedItems.Add("Effects");
            }

            if (options.MergeAutomation)
            {
                targetData.Automation = sourceData.Automation?.ToList() ?? new List<ProjectAltAutomationData>();
                result.MergedItems.Add("Automation");
            }

            target.ModifiedAt = DateTime.UtcNow;

            AddHistoryEntry(projectId, targetId, AlternativeAction.Merged,
                $"Merged from '{source.Name}': {string.Join(", ", result.MergedItems)}");

            AlternativesMerged?.Invoke(this, new AlternativeMergeEventArgs(result));
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Gets a specific alternative.
    /// </summary>
    public ProjectAlternative? GetAlternative(string projectId, string alternativeId)
    {
        if (!_projectAlternatives.TryGetValue(projectId, out var collection))
            return null;

        return collection.Alternatives.FirstOrDefault(a => a.Id == alternativeId);
    }

    /// <summary>
    /// Gets the currently active alternative for a project.
    /// </summary>
    public ProjectAlternative? GetActiveAlternative(string projectId)
    {
        if (!_activeAlternatives.TryGetValue(projectId, out var alternativeId))
            return null;

        return GetAlternative(projectId, alternativeId);
    }

    /// <summary>
    /// Gets all alternatives for a project.
    /// </summary>
    public IReadOnlyList<ProjectAlternative> GetAlternatives(string projectId)
    {
        if (!_projectAlternatives.TryGetValue(projectId, out var collection))
            return Array.Empty<ProjectAlternative>();

        return collection.Alternatives.AsReadOnly();
    }

    /// <summary>
    /// Gets the history of alternative changes for a project.
    /// </summary>
    public IReadOnlyList<AlternativeHistoryEntry> GetHistory(string projectId)
    {
        return _globalHistory
            .Where(h => h.ProjectId == projectId)
            .OrderByDescending(h => h.Timestamp)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Updates the data for an alternative.
    /// </summary>
    public bool UpdateAlternativeData(string projectId, string alternativeId, AlternativeData data)
    {
        var alternative = GetAlternative(projectId, alternativeId);
        if (alternative == null)
            return false;

        alternative.Data = data;
        alternative.ModifiedAt = DateTime.UtcNow;

        AddHistoryEntry(projectId, alternativeId, AlternativeAction.Modified, $"Updated '{alternative.Name}'");

        return true;
    }

    /// <summary>
    /// Renames an alternative.
    /// </summary>
    public bool RenameAlternative(string projectId, string alternativeId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        var alternative = GetAlternative(projectId, alternativeId);
        if (alternative == null)
            return false;

        var collection = _projectAlternatives[projectId];
        var finalName = GetUniqueName(collection, newName, alternativeId);

        var oldName = alternative.Name;
        alternative.Name = finalName;
        alternative.ModifiedAt = DateTime.UtcNow;

        AddHistoryEntry(projectId, alternativeId, AlternativeAction.Renamed, $"Renamed from '{oldName}' to '{finalName}'");

        return true;
    }

    /// <summary>
    /// Gets whether any project has alternatives.
    /// </summary>
    public bool HasAlternatives => _projectAlternatives.Values.Any(c => c.Alternatives.Count > 1);

    /// <summary>
    /// Saves alternatives to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var saveData = new ProjectAltPersistenceData
        {
            ProjectAlternatives = _projectAlternatives,
            ActiveAlternatives = _activeAlternatives,
            History = _globalHistory.TakeLast(500).ToList()
        };

        var json = JsonSerializer.Serialize(saveData, JsonOptions);
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
        var loadData = JsonSerializer.Deserialize<ProjectAltPersistenceData>(json, JsonOptions);

        if (loadData != null)
        {
            _projectAlternatives.Clear();
            _activeAlternatives.Clear();
            _globalHistory.Clear();

            foreach (var kvp in loadData.ProjectAlternatives)
            {
                _projectAlternatives[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in loadData.ActiveAlternatives)
            {
                _activeAlternatives[kvp.Key] = kvp.Value;
            }

            _globalHistory.AddRange(loadData.History);

            NotifyPropertyChanged(nameof(HasAlternatives));
        }
    }

    /// <summary>
    /// Clears all alternatives data.
    /// </summary>
    public void Clear()
    {
        _projectAlternatives.Clear();
        _activeAlternatives.Clear();
        _globalHistory.Clear();
        NotifyPropertyChanged(nameof(HasAlternatives));
    }

    #endregion

    #region Private Methods

    private ProjectAlternativeCollection GetOrCreateCollection(string projectId)
    {
        if (!_projectAlternatives.TryGetValue(projectId, out var collection))
        {
            collection = new ProjectAlternativeCollection { ProjectId = projectId };
            _projectAlternatives[projectId] = collection;
        }
        return collection;
    }

    private static string GetUniqueName(ProjectAlternativeCollection collection, string baseName, string? excludeId = null)
    {
        var name = baseName;
        var counter = 1;

        while (collection.Alternatives.Any(a => a.Name == name && a.Id != excludeId))
        {
            name = $"{baseName} ({counter++})";
        }

        return name;
    }

    private static string GetNextColor(int index)
    {
        var colors = new[]
        {
            "#4B6EAF", "#6AAB73", "#9C7CE8", "#E8A73C",
            "#E85C5C", "#5CBFE8", "#E85CAF", "#7CE89C"
        };

        return colors[index % colors.Length];
    }

    private void AddHistoryEntry(string projectId, string alternativeId, AlternativeAction action, string description)
    {
        _globalHistory.Add(new AlternativeHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            AlternativeId = alternativeId,
            Action = action,
            Description = description,
            Timestamp = DateTime.UtcNow
        });

        // Keep history manageable
        if (_globalHistory.Count > 1000)
        {
            _globalHistory.RemoveRange(0, 500);
        }
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

#region Models

/// <summary>
/// Represents a single alternative version of a project.
/// </summary>
public class ProjectAlternative : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _projectId = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private DateTime _createdAt;
    private DateTime _modifiedAt;
    private string _color = "#4B6EAF";
    private AlternativeData? _data;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string ProjectId { get => _projectId; set { _projectId = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public DateTime ModifiedAt { get => _modifiedAt; set { _modifiedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModifiedDisplay)); } }
    public string Color { get => _color; set { _color = value; OnPropertyChanged(); } }
    public AlternativeData? Data { get => _data; set { _data = value; OnPropertyChanged(); } }

    public string ModifiedDisplay => $"Modified: {ModifiedAt:g}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Data stored in an alternative.
/// </summary>
public class AlternativeData
{
    public double Tempo { get; set; } = 120.0;
    public double MasterVolume { get; set; } = 0.0;
    public int TrackCount => Tracks?.Count ?? 0;
    public int ClipCount => Clips?.Count ?? 0;
    public int EffectCount => Effects?.Count ?? 0;
    public int AutomationPointCount => Automation?.Sum(a => a.Points?.Count ?? 0) ?? 0;

    public List<AlternativeTrackData>? Tracks { get; set; }
    public List<ProjectAltClipData>? Clips { get; set; }
    public List<ProjectAltEffectData>? Effects { get; set; }
    public List<ProjectAltAutomationData>? Automation { get; set; }
    public Dictionary<string, object>? MixerSettings { get; set; }
    public Dictionary<string, object>? CustomData { get; set; }

    public AlternativeData Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<AlternativeData>(json) ?? new AlternativeData();
    }
}

public class AlternativeTrackData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Volume { get; set; }
    public double Pan { get; set; }
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }
}

public class ProjectAltClipData
{
    public string Id { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double StartBeat { get; set; }
    public double DurationBeats { get; set; }
}

public class ProjectAltEffectData
{
    public string Id { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBypassed { get; set; }
}

public class ProjectAltAutomationData
{
    public string TrackId { get; set; } = string.Empty;
    public string ParameterId { get; set; } = string.Empty;
    public List<ProjectAltAutomationPoint>? Points { get; set; }
}

public class ProjectAltAutomationPoint
{
    public double Beat { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Collection of alternatives for a project.
/// </summary>
public class ProjectAlternativeCollection
{
    public string ProjectId { get; set; } = string.Empty;
    public List<ProjectAlternative> Alternatives { get; set; } = new();
}

/// <summary>
/// History entry for alternative changes.
/// </summary>
public class AlternativeHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AlternativeId { get; set; } = string.Empty;
    public AlternativeAction Action { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public enum AlternativeAction
{
    Created,
    Deleted,
    Renamed,
    Modified,
    Switched,
    Merged
}

/// <summary>
/// Result of comparing two alternatives.
/// </summary>
public class AlternativeComparison
{
    public string ProjectId { get; set; } = string.Empty;
    public ProjectAlternative? Alternative1 { get; set; }
    public ProjectAlternative? Alternative2 { get; set; }
    public DateTime ComparedAt { get; set; }
    public List<AlternativeDifference> Differences { get; set; } = new();
    public bool HasDifferences => Differences.Count > 0;
}

public class AlternativeDifference
{
    public string Category { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string Value1 { get; set; } = string.Empty;
    public string Value2 { get; set; } = string.Empty;
}

/// <summary>
/// Options for merging alternatives.
/// </summary>
public class MergeOptions
{
    public bool MergeTracks { get; set; } = true;
    public bool MergeClips { get; set; } = true;
    public bool MergeMixerSettings { get; set; } = true;
    public bool MergeEffects { get; set; } = true;
    public bool MergeAutomation { get; set; } = true;
}

/// <summary>
/// Result of a merge operation.
/// </summary>
public class AlternativeMergeResult
{
    public string ProjectId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public DateTime MergedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> MergedItems { get; set; } = new();
}

internal class ProjectAltPersistenceData
{
    public Dictionary<string, ProjectAlternativeCollection> ProjectAlternatives { get; set; } = new();
    public Dictionary<string, string> ActiveAlternatives { get; set; } = new();
    public List<AlternativeHistoryEntry> History { get; set; } = new();
}

#endregion

#region Event Args

public class ProjectAltChangedEventArgs : EventArgs
{
    public string ProjectId { get; }
    public ProjectAlternative Alternative { get; }

    public ProjectAltChangedEventArgs(string projectId, ProjectAlternative alternative)
    {
        ProjectId = projectId;
        Alternative = alternative;
    }
}

public class ProjectAltSwitchedEventArgs : EventArgs
{
    public string ProjectId { get; }
    public ProjectAlternative? PreviousAlternative { get; }
    public ProjectAlternative NewAlternative { get; }

    public ProjectAltSwitchedEventArgs(string projectId, ProjectAlternative? previous, ProjectAlternative newAlternative)
    {
        ProjectId = projectId;
        PreviousAlternative = previous;
        NewAlternative = newAlternative;
    }
}

public class AlternativeCompareEventArgs : EventArgs
{
    public AlternativeComparison Comparison { get; }

    public AlternativeCompareEventArgs(AlternativeComparison comparison)
    {
        Comparison = comparison;
    }
}

public class AlternativeMergeEventArgs : EventArgs
{
    public AlternativeMergeResult Result { get; }

    public AlternativeMergeEventArgs(AlternativeMergeResult result)
    {
        Result = result;
    }
}

#endregion

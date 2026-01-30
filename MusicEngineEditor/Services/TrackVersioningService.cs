// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

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
/// Service for managing multiple versions (playlists) per track.
/// Supports version history, comparison, and switching between versions.
/// </summary>
public class TrackVersioningService : ITrackVersioningService
{
    #region Fields

    private readonly Dictionary<string, TrackVersionCollection> _trackVersions = new();
    private readonly Dictionary<string, string> _activeVersions = new();
    private readonly List<VersionHistoryEntry> _globalHistory = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #endregion

    #region Events

    public event EventHandler<VersionChangedEventArgs>? VersionCreated;
    public event EventHandler<VersionChangedEventArgs>? VersionDeleted;
    public event EventHandler<VersionChangedEventArgs>? VersionRenamed;
    public event EventHandler<VersionSwitchedEventArgs>? VersionSwitched;
    public event EventHandler<VersionCompareEventArgs>? VersionCompared;

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a new version for a track.
    /// </summary>
    public TrackVersion CreateVersion(string trackId, string name, TrackVersionData data)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            throw new ArgumentException("Track ID cannot be empty", nameof(trackId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Version name cannot be empty", nameof(name));

        var collection = GetOrCreateCollection(trackId);

        // Ensure unique name
        var finalName = GetUniqueVersionName(collection, name);

        var version = new TrackVersion
        {
            Id = Guid.NewGuid().ToString(),
            TrackId = trackId,
            Name = finalName,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Data = data
        };

        collection.Versions.Add(version);

        // If this is the first version, make it active
        if (collection.Versions.Count == 1 || !_activeVersions.ContainsKey(trackId))
        {
            _activeVersions[trackId] = version.Id;
        }

        AddHistoryEntry(trackId, version.Id, VersionAction.Created, $"Created version '{finalName}'");

        VersionCreated?.Invoke(this, new VersionChangedEventArgs(trackId, version));

        return version;
    }

    /// <summary>
    /// Creates a duplicate of an existing version.
    /// </summary>
    public TrackVersion DuplicateVersion(string trackId, string sourceVersionId, string newName)
    {
        var source = GetVersion(trackId, sourceVersionId);
        if (source == null)
            throw new InvalidOperationException($"Version '{sourceVersionId}' not found for track '{trackId}'");

        var dataCopy = source.Data?.Clone() ?? new TrackVersionData();
        return CreateVersion(trackId, newName, dataCopy);
    }

    /// <summary>
    /// Deletes a version.
    /// </summary>
    public bool DeleteVersion(string trackId, string versionId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
            return false;

        var version = collection.Versions.FirstOrDefault(v => v.Id == versionId);
        if (version == null)
            return false;

        // Don't delete if it's the only version
        if (collection.Versions.Count <= 1)
            return false;

        collection.Versions.Remove(version);

        // If deleting active version, switch to another
        if (_activeVersions.TryGetValue(trackId, out var activeId) && activeId == versionId)
        {
            var newActive = collection.Versions.First();
            _activeVersions[trackId] = newActive.Id;
            VersionSwitched?.Invoke(this, new VersionSwitchedEventArgs(trackId, version, newActive));
        }

        AddHistoryEntry(trackId, versionId, VersionAction.Deleted, $"Deleted version '{version.Name}'");

        VersionDeleted?.Invoke(this, new VersionChangedEventArgs(trackId, version));

        return true;
    }

    /// <summary>
    /// Renames a version.
    /// </summary>
    public bool RenameVersion(string trackId, string versionId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        var version = GetVersion(trackId, versionId);
        if (version == null)
            return false;

        var collection = _trackVersions[trackId];
        var finalName = GetUniqueVersionName(collection, newName, versionId);

        var oldName = version.Name;
        version.Name = finalName;
        version.ModifiedAt = DateTime.UtcNow;

        AddHistoryEntry(trackId, versionId, VersionAction.Renamed, $"Renamed version from '{oldName}' to '{finalName}'");

        VersionRenamed?.Invoke(this, new VersionChangedEventArgs(trackId, version));

        return true;
    }

    /// <summary>
    /// Switches to a different version.
    /// </summary>
    public TrackVersion? SwitchVersion(string trackId, string versionId)
    {
        var version = GetVersion(trackId, versionId);
        if (version == null)
            return null;

        TrackVersion? previousVersion = null;
        if (_activeVersions.TryGetValue(trackId, out var previousId))
        {
            previousVersion = GetVersion(trackId, previousId);
        }

        _activeVersions[trackId] = versionId;

        AddHistoryEntry(trackId, versionId, VersionAction.Switched, $"Switched to version '{version.Name}'");

        VersionSwitched?.Invoke(this, new VersionSwitchedEventArgs(trackId, previousVersion, version));

        return version;
    }

    /// <summary>
    /// Gets a specific version.
    /// </summary>
    public TrackVersion? GetVersion(string trackId, string versionId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
            return null;

        return collection.Versions.FirstOrDefault(v => v.Id == versionId);
    }

    /// <summary>
    /// Gets the currently active version for a track.
    /// </summary>
    public TrackVersion? GetActiveVersion(string trackId)
    {
        if (!_activeVersions.TryGetValue(trackId, out var versionId))
            return null;

        return GetVersion(trackId, versionId);
    }

    /// <summary>
    /// Gets all versions for a track.
    /// </summary>
    public IReadOnlyList<TrackVersion> GetVersions(string trackId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
            return Array.Empty<TrackVersion>();

        return collection.Versions.AsReadOnly();
    }

    /// <summary>
    /// Gets version history for a track.
    /// </summary>
    public IReadOnlyList<VersionHistoryEntry> GetVersionHistory(string trackId)
    {
        return _globalHistory
            .Where(h => h.TrackId == trackId)
            .OrderByDescending(h => h.Timestamp)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Compares two versions and returns the differences.
    /// </summary>
    public VersionComparison CompareVersions(string trackId, string versionId1, string versionId2)
    {
        var version1 = GetVersion(trackId, versionId1);
        var version2 = GetVersion(trackId, versionId2);

        if (version1 == null || version2 == null)
            throw new InvalidOperationException("One or both versions not found");

        var comparison = new VersionComparison
        {
            TrackId = trackId,
            Version1 = version1,
            Version2 = version2,
            ComparedAt = DateTime.UtcNow
        };

        // Compare data properties
        var data1 = version1.Data;
        var data2 = version2.Data;

        if (data1 != null && data2 != null)
        {
            // Compare notes with detailed diff
            comparison.NoteDiffs = CompareNotes(data1.NoteEvents, data2.NoteEvents);
            if (comparison.NoteDiffs.Added.Count > 0 || comparison.NoteDiffs.Removed.Count > 0 || comparison.NoteDiffs.Modified.Count > 0)
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "NoteEvents",
                    Value1 = $"{data1.NoteEvents?.Count ?? 0} notes",
                    Value2 = $"{data2.NoteEvents?.Count ?? 0} notes"
                });
            }

            // Compare clips with detailed diff
            comparison.ClipDiffs = CompareClips(data1.Clips, data2.Clips);
            if (comparison.ClipDiffs.Added.Count > 0 || comparison.ClipDiffs.Removed.Count > 0 || comparison.ClipDiffs.Modified.Count > 0)
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "Clips",
                    Value1 = $"{data1.Clips?.Count ?? 0} clips",
                    Value2 = $"{data2.Clips?.Count ?? 0} clips"
                });
            }

            // Compare automation with detailed diff
            comparison.AutomationDiffs = CompareAutomation(data1.AutomationLanes, data2.AutomationLanes);
            if (comparison.AutomationDiffs.Added.Count > 0 || comparison.AutomationDiffs.Removed.Count > 0 || comparison.AutomationDiffs.Modified.Count > 0)
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "AutomationLanes",
                    Value1 = $"{data1.AutomationLanes?.Count ?? 0} lanes",
                    Value2 = $"{data2.AutomationLanes?.Count ?? 0} lanes"
                });
            }

            // Compare effects with detailed diff
            comparison.EffectDiffs = CompareEffects(data1.Effects, data2.Effects);
            if (comparison.EffectDiffs.Added.Count > 0 || comparison.EffectDiffs.Removed.Count > 0 || comparison.EffectDiffs.Modified.Count > 0)
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "Effects",
                    Value1 = $"{data1.Effects?.Count ?? 0} effects",
                    Value2 = $"{data2.Effects?.Count ?? 0} effects"
                });
            }

            // Compare custom parameters
            comparison.ParameterDiffs = CompareParameters(data1.CustomData, data2.CustomData);
        }

        VersionCompared?.Invoke(this, new VersionCompareEventArgs(comparison));

        return comparison;
    }

    /// <summary>
    /// Compares two sets of MIDI notes and returns detailed differences.
    /// </summary>
    private static NoteComparison CompareNotes(IList<NoteEventData>? notes1, IList<NoteEventData>? notes2)
    {
        var result = new NoteComparison();

        var list1 = notes1?.ToList() ?? new List<NoteEventData>();
        var list2 = notes2?.ToList() ?? new List<NoteEventData>();

        // Create lookup by note identity (note number + start beat)
        var notes1Dict = list1.ToDictionary(n => $"{n.NoteNumber}:{n.StartBeat:F4}", n => n);
        var notes2Dict = list2.ToDictionary(n => $"{n.NoteNumber}:{n.StartBeat:F4}", n => n);

        // Find added notes (in notes2 but not in notes1)
        foreach (var note in list2)
        {
            var key = $"{note.NoteNumber}:{note.StartBeat:F4}";
            if (!notes1Dict.ContainsKey(key))
            {
                result.Added.Add(new NoteDiffItem
                {
                    NoteNumber = note.NoteNumber,
                    NoteName = GetNoteName(note.NoteNumber),
                    NewStartBeat = note.StartBeat,
                    NewDuration = note.DurationBeats,
                    NewVelocity = note.Velocity
                });
            }
        }

        // Find removed notes (in notes1 but not in notes2)
        foreach (var note in list1)
        {
            var key = $"{note.NoteNumber}:{note.StartBeat:F4}";
            if (!notes2Dict.ContainsKey(key))
            {
                result.Removed.Add(new NoteDiffItem
                {
                    NoteNumber = note.NoteNumber,
                    NoteName = GetNoteName(note.NoteNumber),
                    OldStartBeat = note.StartBeat,
                    OldDuration = note.DurationBeats,
                    OldVelocity = note.Velocity
                });
            }
        }

        // Find modified notes
        foreach (var note1 in list1)
        {
            var key = $"{note1.NoteNumber}:{note1.StartBeat:F4}";
            if (notes2Dict.TryGetValue(key, out var note2))
            {
                var changes = new List<string>();
                if (Math.Abs(note1.DurationBeats - note2.DurationBeats) > 0.001)
                    changes.Add($"Duration: {note1.DurationBeats:F2} -> {note2.DurationBeats:F2}");
                if (note1.Velocity != note2.Velocity)
                    changes.Add($"Velocity: {note1.Velocity} -> {note2.Velocity}");

                if (changes.Count > 0)
                {
                    result.Modified.Add(new NoteDiffItem
                    {
                        NoteNumber = note1.NoteNumber,
                        NoteName = GetNoteName(note1.NoteNumber),
                        OldStartBeat = note1.StartBeat,
                        NewStartBeat = note2.StartBeat,
                        OldDuration = note1.DurationBeats,
                        NewDuration = note2.DurationBeats,
                        OldVelocity = note1.Velocity,
                        NewVelocity = note2.Velocity,
                        Changes = changes
                    });
                }
                else
                {
                    result.Unchanged.Add(new NoteDiffItem
                    {
                        NoteNumber = note1.NoteNumber,
                        NoteName = GetNoteName(note1.NoteNumber),
                        OldStartBeat = note1.StartBeat,
                        OldDuration = note1.DurationBeats,
                        OldVelocity = note1.Velocity
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Compares two sets of clips and returns detailed differences.
    /// </summary>
    private static ClipComparison CompareClips(IList<ClipData>? clips1, IList<ClipData>? clips2)
    {
        var result = new ClipComparison();

        var list1 = clips1?.ToList() ?? new List<ClipData>();
        var list2 = clips2?.ToList() ?? new List<ClipData>();

        var clips1Dict = list1.ToDictionary(c => c.Id, c => c);
        var clips2Dict = list2.ToDictionary(c => c.Id, c => c);

        // Find added clips
        foreach (var clip in list2)
        {
            if (!clips1Dict.ContainsKey(clip.Id))
            {
                result.Added.Add(new ClipDiffItem
                {
                    ClipId = clip.Id,
                    ClipName = clip.Name,
                    NewStartBeat = clip.StartBeat,
                    NewDuration = clip.DurationBeats,
                    NewAudioPath = clip.AudioFilePath
                });
            }
        }

        // Find removed clips
        foreach (var clip in list1)
        {
            if (!clips2Dict.ContainsKey(clip.Id))
            {
                result.Removed.Add(new ClipDiffItem
                {
                    ClipId = clip.Id,
                    ClipName = clip.Name,
                    OldStartBeat = clip.StartBeat,
                    OldDuration = clip.DurationBeats,
                    OldAudioPath = clip.AudioFilePath
                });
            }
        }

        // Find modified clips
        foreach (var clip1 in list1)
        {
            if (clips2Dict.TryGetValue(clip1.Id, out var clip2))
            {
                var changes = new List<string>();
                if (clip1.Name != clip2.Name)
                    changes.Add($"Name: '{clip1.Name}' -> '{clip2.Name}'");
                if (Math.Abs(clip1.StartBeat - clip2.StartBeat) > 0.001)
                    changes.Add($"Position: {clip1.StartBeat:F2} -> {clip2.StartBeat:F2}");
                if (Math.Abs(clip1.DurationBeats - clip2.DurationBeats) > 0.001)
                    changes.Add($"Duration: {clip1.DurationBeats:F2} -> {clip2.DurationBeats:F2}");
                if (clip1.AudioFilePath != clip2.AudioFilePath)
                    changes.Add($"Audio file changed");

                if (changes.Count > 0)
                {
                    result.Modified.Add(new ClipDiffItem
                    {
                        ClipId = clip1.Id,
                        ClipName = clip2.Name,
                        OldStartBeat = clip1.StartBeat,
                        NewStartBeat = clip2.StartBeat,
                        OldDuration = clip1.DurationBeats,
                        NewDuration = clip2.DurationBeats,
                        OldAudioPath = clip1.AudioFilePath,
                        NewAudioPath = clip2.AudioFilePath,
                        Changes = changes
                    });
                }
                else
                {
                    result.Unchanged.Add(new ClipDiffItem
                    {
                        ClipId = clip1.Id,
                        ClipName = clip1.Name,
                        OldStartBeat = clip1.StartBeat,
                        OldDuration = clip1.DurationBeats
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Compares two sets of effects and returns detailed differences.
    /// </summary>
    private static EffectComparison CompareEffects(IList<EffectData>? effects1, IList<EffectData>? effects2)
    {
        var result = new EffectComparison();

        var list1 = effects1?.ToList() ?? new List<EffectData>();
        var list2 = effects2?.ToList() ?? new List<EffectData>();

        var effects1Dict = list1.ToDictionary(e => e.Id, e => e);
        var effects2Dict = list2.ToDictionary(e => e.Id, e => e);

        // Find added effects
        foreach (var effect in list2)
        {
            if (!effects1Dict.ContainsKey(effect.Id))
            {
                result.Added.Add(new EffectDiffItem
                {
                    EffectId = effect.Id,
                    EffectName = effect.Name,
                    PluginId = effect.PluginId,
                    NewBypassed = effect.IsBypassed
                });
            }
        }

        // Find removed effects
        foreach (var effect in list1)
        {
            if (!effects2Dict.ContainsKey(effect.Id))
            {
                result.Removed.Add(new EffectDiffItem
                {
                    EffectId = effect.Id,
                    EffectName = effect.Name,
                    PluginId = effect.PluginId,
                    OldBypassed = effect.IsBypassed
                });
            }
        }

        // Find modified effects
        foreach (var effect1 in list1)
        {
            if (effects2Dict.TryGetValue(effect1.Id, out var effect2))
            {
                var paramChanges = new List<ParameterDiffItem>();

                // Compare bypass state
                if (effect1.IsBypassed != effect2.IsBypassed)
                {
                    paramChanges.Add(new ParameterDiffItem
                    {
                        ParameterName = "Bypassed",
                        OldValue = effect1.IsBypassed.ToString(),
                        NewValue = effect2.IsBypassed.ToString(),
                        Category = "State"
                    });
                }

                // Compare parameters
                var params1 = effect1.Parameters ?? new Dictionary<string, object>();
                var params2 = effect2.Parameters ?? new Dictionary<string, object>();

                foreach (var param2 in params2)
                {
                    if (!params1.TryGetValue(param2.Key, out var value1))
                    {
                        paramChanges.Add(new ParameterDiffItem
                        {
                            ParameterName = param2.Key,
                            OldValue = null,
                            NewValue = param2.Value?.ToString(),
                            Category = "Parameter"
                        });
                    }
                    else if (value1?.ToString() != param2.Value?.ToString())
                    {
                        paramChanges.Add(new ParameterDiffItem
                        {
                            ParameterName = param2.Key,
                            OldValue = value1?.ToString(),
                            NewValue = param2.Value?.ToString(),
                            Category = "Parameter"
                        });
                    }
                }

                foreach (var param1 in params1)
                {
                    if (!params2.ContainsKey(param1.Key))
                    {
                        paramChanges.Add(new ParameterDiffItem
                        {
                            ParameterName = param1.Key,
                            OldValue = param1.Value?.ToString(),
                            NewValue = null,
                            Category = "Parameter"
                        });
                    }
                }

                if (paramChanges.Count > 0)
                {
                    result.Modified.Add(new EffectDiffItem
                    {
                        EffectId = effect1.Id,
                        EffectName = effect1.Name,
                        PluginId = effect1.PluginId,
                        OldBypassed = effect1.IsBypassed,
                        NewBypassed = effect2.IsBypassed,
                        ParameterChanges = paramChanges
                    });
                }
                else
                {
                    result.Unchanged.Add(new EffectDiffItem
                    {
                        EffectId = effect1.Id,
                        EffectName = effect1.Name,
                        PluginId = effect1.PluginId
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Compares two sets of automation lanes and returns detailed differences.
    /// </summary>
    private static AutomationComparison CompareAutomation(IList<AutomationLaneData>? lanes1, IList<AutomationLaneData>? lanes2)
    {
        var result = new AutomationComparison();

        var list1 = lanes1?.ToList() ?? new List<AutomationLaneData>();
        var list2 = lanes2?.ToList() ?? new List<AutomationLaneData>();

        var lanes1Dict = list1.ToDictionary(l => l.ParameterId, l => l);
        var lanes2Dict = list2.ToDictionary(l => l.ParameterId, l => l);

        // Find added lanes
        foreach (var lane in list2)
        {
            if (!lanes1Dict.ContainsKey(lane.ParameterId))
            {
                result.Added.Add(new AutomationDiffItem
                {
                    ParameterId = lane.ParameterId,
                    ParameterName = lane.ParameterName,
                    NewPointCount = lane.Points?.Count ?? 0
                });
            }
        }

        // Find removed lanes
        foreach (var lane in list1)
        {
            if (!lanes2Dict.ContainsKey(lane.ParameterId))
            {
                result.Removed.Add(new AutomationDiffItem
                {
                    ParameterId = lane.ParameterId,
                    ParameterName = lane.ParameterName,
                    OldPointCount = lane.Points?.Count ?? 0
                });
            }
        }

        // Find modified lanes
        foreach (var lane1 in list1)
        {
            if (lanes2Dict.TryGetValue(lane1.ParameterId, out var lane2))
            {
                var pointChanges = CompareAutomationPoints(lane1.Points, lane2.Points);

                if (pointChanges.Count > 0)
                {
                    result.Modified.Add(new AutomationDiffItem
                    {
                        ParameterId = lane1.ParameterId,
                        ParameterName = lane1.ParameterName,
                        OldPointCount = lane1.Points?.Count ?? 0,
                        NewPointCount = lane2.Points?.Count ?? 0,
                        PointChanges = pointChanges
                    });
                }
                else
                {
                    result.Unchanged.Add(new AutomationDiffItem
                    {
                        ParameterId = lane1.ParameterId,
                        ParameterName = lane1.ParameterName,
                        OldPointCount = lane1.Points?.Count ?? 0
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Compares automation points between two lanes.
    /// </summary>
    private static List<AutomationPointChange> CompareAutomationPoints(IList<AutomationPointData>? points1, IList<AutomationPointData>? points2)
    {
        var result = new List<AutomationPointChange>();

        var list1 = points1?.ToList() ?? new List<AutomationPointData>();
        var list2 = points2?.ToList() ?? new List<AutomationPointData>();

        var points1Dict = list1.ToDictionary(p => p.Beat, p => p);
        var points2Dict = list2.ToDictionary(p => p.Beat, p => p);

        // Find added points
        foreach (var point in list2)
        {
            if (!points1Dict.ContainsKey(point.Beat))
            {
                result.Add(new AutomationPointChange
                {
                    Beat = point.Beat,
                    NewValue = point.Value,
                    ChangeType = PointChangeType.Added
                });
            }
        }

        // Find removed points
        foreach (var point in list1)
        {
            if (!points2Dict.ContainsKey(point.Beat))
            {
                result.Add(new AutomationPointChange
                {
                    Beat = point.Beat,
                    OldValue = point.Value,
                    ChangeType = PointChangeType.Removed
                });
            }
        }

        // Find modified points
        foreach (var point1 in list1)
        {
            if (points2Dict.TryGetValue(point1.Beat, out var point2))
            {
                if (Math.Abs(point1.Value - point2.Value) > 0.001)
                {
                    result.Add(new AutomationPointChange
                    {
                        Beat = point1.Beat,
                        OldValue = point1.Value,
                        NewValue = point2.Value,
                        ChangeType = PointChangeType.Modified
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Compares custom parameter dictionaries.
    /// </summary>
    private static ParameterComparison CompareParameters(Dictionary<string, object>? params1, Dictionary<string, object>? params2)
    {
        var result = new ParameterComparison();

        var dict1 = params1 ?? new Dictionary<string, object>();
        var dict2 = params2 ?? new Dictionary<string, object>();

        // Find added parameters
        foreach (var param in dict2)
        {
            if (!dict1.ContainsKey(param.Key))
            {
                result.Added.Add(new ParameterDiffItem
                {
                    ParameterName = param.Key,
                    NewValue = param.Value?.ToString(),
                    Category = "Custom"
                });
            }
        }

        // Find removed parameters
        foreach (var param in dict1)
        {
            if (!dict2.ContainsKey(param.Key))
            {
                result.Removed.Add(new ParameterDiffItem
                {
                    ParameterName = param.Key,
                    OldValue = param.Value?.ToString(),
                    Category = "Custom"
                });
            }
        }

        // Find modified parameters
        foreach (var param1 in dict1)
        {
            if (dict2.TryGetValue(param1.Key, out var value2))
            {
                if (param1.Value?.ToString() != value2?.ToString())
                {
                    result.Modified.Add(new ParameterDiffItem
                    {
                        ParameterName = param1.Key,
                        OldValue = param1.Value?.ToString(),
                        NewValue = value2?.ToString(),
                        Category = "Custom"
                    });
                }
                else
                {
                    result.Unchanged.Add(new ParameterDiffItem
                    {
                        ParameterName = param1.Key,
                        OldValue = param1.Value?.ToString(),
                        Category = "Custom"
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the note name for a MIDI note number.
    /// </summary>
    private static string GetNoteName(int noteNumber)
    {
        var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var octave = (noteNumber / 12) - 1;
        var noteName = noteNames[noteNumber % 12];
        return $"{noteName}{octave}";
    }

    /// <summary>
    /// Updates the data for a version.
    /// </summary>
    public bool UpdateVersionData(string trackId, string versionId, TrackVersionData data)
    {
        var version = GetVersion(trackId, versionId);
        if (version == null)
            return false;

        version.Data = data;
        version.ModifiedAt = DateTime.UtcNow;

        AddHistoryEntry(trackId, versionId, VersionAction.Modified, $"Updated version '{version.Name}'");

        return true;
    }

    /// <summary>
    /// Saves versions to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var saveData = new VersioningPersistenceData
        {
            TrackVersions = _trackVersions,
            ActiveVersions = _activeVersions,
            History = _globalHistory
        };

        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads versions from a file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath);
        var loadData = JsonSerializer.Deserialize<VersioningPersistenceData>(json, JsonOptions);

        if (loadData != null)
        {
            _trackVersions.Clear();
            _activeVersions.Clear();
            _globalHistory.Clear();

            foreach (var kvp in loadData.TrackVersions)
            {
                _trackVersions[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in loadData.ActiveVersions)
            {
                _activeVersions[kvp.Key] = kvp.Value;
            }

            _globalHistory.AddRange(loadData.History);
        }
    }

    /// <summary>
    /// Clears all version data.
    /// </summary>
    public void Clear()
    {
        _trackVersions.Clear();
        _activeVersions.Clear();
        _globalHistory.Clear();
    }

    #endregion

    #region Private Methods

    private TrackVersionCollection GetOrCreateCollection(string trackId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
        {
            collection = new TrackVersionCollection { TrackId = trackId };
            _trackVersions[trackId] = collection;
        }
        return collection;
    }

    private static string GetUniqueVersionName(TrackVersionCollection collection, string baseName, string? excludeId = null)
    {
        var name = baseName;
        var counter = 1;

        while (collection.Versions.Any(v => v.Name == name && v.Id != excludeId))
        {
            name = $"{baseName} ({counter++})";
        }

        return name;
    }

    private void AddHistoryEntry(string trackId, string versionId, VersionAction action, string description)
    {
        _globalHistory.Add(new VersionHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            TrackId = trackId,
            VersionId = versionId,
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

    private static bool SequenceEqual<T>(IList<T>? list1, IList<T>? list2)
    {
        if (list1 == null && list2 == null) return true;
        if (list1 == null || list2 == null) return false;
        if (list1.Count != list2.Count) return false;

        var json1 = JsonSerializer.Serialize(list1, JsonOptions);
        var json2 = JsonSerializer.Serialize(list2, JsonOptions);
        return json1 == json2;
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for track versioning service.
/// </summary>
public interface ITrackVersioningService
{
    event EventHandler<VersionChangedEventArgs>? VersionCreated;
    event EventHandler<VersionChangedEventArgs>? VersionDeleted;
    event EventHandler<VersionChangedEventArgs>? VersionRenamed;
    event EventHandler<VersionSwitchedEventArgs>? VersionSwitched;

    TrackVersion CreateVersion(string trackId, string name, TrackVersionData data);
    TrackVersion DuplicateVersion(string trackId, string sourceVersionId, string newName);
    bool DeleteVersion(string trackId, string versionId);
    bool RenameVersion(string trackId, string versionId, string newName);
    TrackVersion? SwitchVersion(string trackId, string versionId);
    TrackVersion? GetVersion(string trackId, string versionId);
    TrackVersion? GetActiveVersion(string trackId);
    IReadOnlyList<TrackVersion> GetVersions(string trackId);
    IReadOnlyList<VersionHistoryEntry> GetVersionHistory(string trackId);
    VersionComparison CompareVersions(string trackId, string versionId1, string versionId2);
    bool UpdateVersionData(string trackId, string versionId, TrackVersionData data);
    Task SaveToFileAsync(string filePath);
    Task LoadFromFileAsync(string filePath);
    void Clear();
}

#endregion

#region Models

/// <summary>
/// Represents a single version of a track.
/// </summary>
public class TrackVersion : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _trackId = string.Empty;
    private string _name = string.Empty;
    private DateTime _createdAt;
    private DateTime _modifiedAt;
    private string _description = string.Empty;
    private TrackVersionData? _data;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string TrackId { get => _trackId; set { _trackId = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public DateTime ModifiedAt { get => _modifiedAt; set { _modifiedAt = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public TrackVersionData? Data { get => _data; set { _data = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Data stored in a track version.
/// </summary>
public class TrackVersionData
{
    public List<NoteEventData>? NoteEvents { get; set; }
    public List<ClipData>? Clips { get; set; }
    public List<AutomationLaneData>? AutomationLanes { get; set; }
    public List<EffectData>? Effects { get; set; }
    public Dictionary<string, object>? CustomData { get; set; }

    public TrackVersionData Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<TrackVersionData>(json) ?? new TrackVersionData();
    }
}

public class NoteEventData
{
    public int NoteNumber { get; set; }
    public double StartBeat { get; set; }
    public double DurationBeats { get; set; }
    public int Velocity { get; set; }
}

public class ClipData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double StartBeat { get; set; }
    public double DurationBeats { get; set; }
    public string AudioFilePath { get; set; } = string.Empty;
}

public class AutomationLaneData
{
    public string ParameterId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public List<AutomationPointData>? Points { get; set; }
}

public class AutomationPointData
{
    public double Beat { get; set; }
    public double Value { get; set; }
    public int CurveType { get; set; }
}

public class EffectData
{
    public string Id { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBypassed { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Collection of versions for a single track.
/// </summary>
public class TrackVersionCollection
{
    public string TrackId { get; set; } = string.Empty;
    public List<TrackVersion> Versions { get; set; } = new();
}

/// <summary>
/// History entry for version changes.
/// </summary>
public class VersionHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public VersionAction Action { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public enum VersionAction
{
    Created,
    Deleted,
    Renamed,
    Modified,
    Switched
}

/// <summary>
/// Result of comparing two versions.
/// </summary>
public class VersionComparison
{
    public string TrackId { get; set; } = string.Empty;
    public TrackVersion? Version1 { get; set; }
    public TrackVersion? Version2 { get; set; }
    public DateTime ComparedAt { get; set; }
    public List<VersionDifference> Differences { get; set; } = new();

    public bool HasDifferences => Differences.Count > 0;

    // Detailed comparison results
    public ParameterComparison? ParameterDiffs { get; set; }
    public ClipComparison? ClipDiffs { get; set; }
    public EffectComparison? EffectDiffs { get; set; }
    public AutomationComparison? AutomationDiffs { get; set; }
    public NoteComparison? NoteDiffs { get; set; }

    // Summary counts
    public int TotalAdded => (ParameterDiffs?.Added.Count ?? 0) + (ClipDiffs?.Added.Count ?? 0) +
                             (EffectDiffs?.Added.Count ?? 0) + (AutomationDiffs?.Added.Count ?? 0) +
                             (NoteDiffs?.Added.Count ?? 0);

    public int TotalRemoved => (ParameterDiffs?.Removed.Count ?? 0) + (ClipDiffs?.Removed.Count ?? 0) +
                               (EffectDiffs?.Removed.Count ?? 0) + (AutomationDiffs?.Removed.Count ?? 0) +
                               (NoteDiffs?.Removed.Count ?? 0);

    public int TotalModified => (ParameterDiffs?.Modified.Count ?? 0) + (ClipDiffs?.Modified.Count ?? 0) +
                                (EffectDiffs?.Modified.Count ?? 0) + (AutomationDiffs?.Modified.Count ?? 0) +
                                (NoteDiffs?.Modified.Count ?? 0);

    public int TotalUnchanged => (ParameterDiffs?.Unchanged.Count ?? 0) + (ClipDiffs?.Unchanged.Count ?? 0) +
                                 (EffectDiffs?.Unchanged.Count ?? 0) + (AutomationDiffs?.Unchanged.Count ?? 0) +
                                 (NoteDiffs?.Unchanged.Count ?? 0);
}

public class VersionDifference
{
    public string Property { get; set; } = string.Empty;
    public string Value1 { get; set; } = string.Empty;
    public string Value2 { get; set; } = string.Empty;
}

/// <summary>
/// Comparison results for track parameters (volume, pan, etc.).
/// </summary>
public class ParameterComparison
{
    public List<ParameterDiffItem> Added { get; set; } = new();
    public List<ParameterDiffItem> Removed { get; set; } = new();
    public List<ParameterDiffItem> Modified { get; set; } = new();
    public List<ParameterDiffItem> Unchanged { get; set; } = new();
}

public class ParameterDiffItem
{
    public string ParameterName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Category { get; set; } = "General"; // Volume, Pan, Send, etc.
}

/// <summary>
/// Comparison results for clips/regions.
/// </summary>
public class ClipComparison
{
    public List<ClipDiffItem> Added { get; set; } = new();
    public List<ClipDiffItem> Removed { get; set; } = new();
    public List<ClipDiffItem> Modified { get; set; } = new();
    public List<ClipDiffItem> Unchanged { get; set; } = new();
}

public class ClipDiffItem
{
    public string ClipId { get; set; } = string.Empty;
    public string ClipName { get; set; } = string.Empty;
    public double? OldStartBeat { get; set; }
    public double? NewStartBeat { get; set; }
    public double? OldDuration { get; set; }
    public double? NewDuration { get; set; }
    public string? OldAudioPath { get; set; }
    public string? NewAudioPath { get; set; }
    public List<string> Changes { get; set; } = new();
}

/// <summary>
/// Comparison results for effects.
/// </summary>
public class EffectComparison
{
    public List<EffectDiffItem> Added { get; set; } = new();
    public List<EffectDiffItem> Removed { get; set; } = new();
    public List<EffectDiffItem> Modified { get; set; } = new();
    public List<EffectDiffItem> Unchanged { get; set; } = new();
}

public class EffectDiffItem
{
    public string EffectId { get; set; } = string.Empty;
    public string EffectName { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public bool? OldBypassed { get; set; }
    public bool? NewBypassed { get; set; }
    public List<ParameterDiffItem> ParameterChanges { get; set; } = new();
}

/// <summary>
/// Comparison results for automation lanes.
/// </summary>
public class AutomationComparison
{
    public List<AutomationDiffItem> Added { get; set; } = new();
    public List<AutomationDiffItem> Removed { get; set; } = new();
    public List<AutomationDiffItem> Modified { get; set; } = new();
    public List<AutomationDiffItem> Unchanged { get; set; } = new();
}

public class AutomationDiffItem
{
    public string ParameterId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public int OldPointCount { get; set; }
    public int NewPointCount { get; set; }
    public List<AutomationPointChange> PointChanges { get; set; } = new();
}

public class AutomationPointChange
{
    public double Beat { get; set; }
    public double? OldValue { get; set; }
    public double? NewValue { get; set; }
    public PointChangeType ChangeType { get; set; }
}

public enum PointChangeType
{
    Added,
    Removed,
    Modified
}

/// <summary>
/// Comparison results for MIDI notes.
/// </summary>
public class NoteComparison
{
    public List<NoteDiffItem> Added { get; set; } = new();
    public List<NoteDiffItem> Removed { get; set; } = new();
    public List<NoteDiffItem> Modified { get; set; } = new();
    public List<NoteDiffItem> Unchanged { get; set; } = new();
}

public class NoteDiffItem
{
    public int NoteNumber { get; set; }
    public string NoteName { get; set; } = string.Empty;
    public double? OldStartBeat { get; set; }
    public double? NewStartBeat { get; set; }
    public double? OldDuration { get; set; }
    public double? NewDuration { get; set; }
    public int? OldVelocity { get; set; }
    public int? NewVelocity { get; set; }
    public List<string> Changes { get; set; } = new();
}

/// <summary>
/// Data for persistence.
/// </summary>
internal class VersioningPersistenceData
{
    public Dictionary<string, TrackVersionCollection> TrackVersions { get; set; } = new();
    public Dictionary<string, string> ActiveVersions { get; set; } = new();
    public List<VersionHistoryEntry> History { get; set; } = new();
}

#endregion

#region Event Args

public class VersionChangedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackVersion Version { get; }

    public VersionChangedEventArgs(string trackId, TrackVersion version)
    {
        TrackId = trackId;
        Version = version;
    }
}

public class VersionSwitchedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackVersion? PreviousVersion { get; }
    public TrackVersion NewVersion { get; }

    public VersionSwitchedEventArgs(string trackId, TrackVersion? previousVersion, TrackVersion newVersion)
    {
        TrackId = trackId;
        PreviousVersion = previousVersion;
        NewVersion = newVersion;
    }
}

public class VersionCompareEventArgs : EventArgs
{
    public VersionComparison Comparison { get; }

    public VersionCompareEventArgs(VersionComparison comparison)
    {
        Comparison = comparison;
    }
}

#endregion

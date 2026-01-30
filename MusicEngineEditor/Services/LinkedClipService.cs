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
/// Represents a group of linked clips that share edits.
/// </summary>
public class ClipLinkGroup : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Link Group";
    private string _color = "#4A9EFF";
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _modifiedAt = DateTime.UtcNow;
    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
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
    /// Gets or sets whether linking is enabled for this group.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the list of linked clip IDs.
    /// </summary>
    public List<string> ClipIds { get; set; } = new();

    /// <summary>
    /// Gets the source clip ID (the master clip).
    /// </summary>
    public string? SourceClipId { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Types of edits that can be propagated to linked clips.
/// </summary>
[Flags]
public enum LinkedEditType
{
    None = 0,
    Notes = 1,
    Automation = 2,
    Effects = 4,
    Gain = 8,
    Fades = 16,
    Color = 32,
    All = Notes | Automation | Effects | Gain | Fades | Color
}

/// <summary>
/// Service for managing linked clips where edits propagate to all linked instances.
/// </summary>
public class LinkedClipService : ILinkedClipService
{
    #region Fields

    private readonly Dictionary<string, ClipLinkGroup> _groups = new();
    private readonly Dictionary<string, string> _clipToGroup = new();
    private LinkedEditType _propagationMask = LinkedEditType.All;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] DefaultColors = new[]
    {
        "#FF6B6B", "#4ECDC4", "#FFE66D", "#A78BFA",
        "#FF8C42", "#6BCB77", "#4A9EFF", "#FF6B9D"
    };

    #endregion

    #region Events

    public event EventHandler<ClipLinkGroupEventArgs>? GroupCreated;
    public event EventHandler<ClipLinkGroupEventArgs>? GroupDeleted;
    public event EventHandler<ClipLinkedEventArgs>? ClipLinked;
    public event EventHandler<ClipLinkedEventArgs>? ClipUnlinked;
    public event EventHandler<LinkedEditEventArgs>? EditPropagated;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets all link groups.
    /// </summary>
    public ObservableCollection<ClipLinkGroup> Groups { get; } = new();

    /// <summary>
    /// Gets or sets which edit types are propagated to linked clips.
    /// </summary>
    public LinkedEditType PropagationMask
    {
        get => _propagationMask;
        set => _propagationMask = value;
    }

    #endregion

    #region Group Management

    /// <summary>
    /// Creates a new link group from the given clips.
    /// </summary>
    public ClipLinkGroup CreateLinkGroup(IEnumerable<string> clipIds, string? name = null)
    {
        var clipList = clipIds.ToList();
        if (clipList.Count < 2)
            throw new ArgumentException("At least 2 clips are required to create a link group", nameof(clipIds));

        // Remove clips from any existing groups
        foreach (var clipId in clipList)
        {
            UnlinkClip(clipId);
        }

        var colorIndex = _groups.Count % DefaultColors.Length;

        var group = new ClipLinkGroup
        {
            Name = name ?? $"Link Group {_groups.Count + 1}",
            Color = DefaultColors[colorIndex],
            ClipIds = clipList,
            SourceClipId = clipList.First()
        };

        _groups[group.Id] = group;

        foreach (var clipId in clipList)
        {
            _clipToGroup[clipId] = group.Id;
        }

        RefreshGroupsCollection();
        GroupCreated?.Invoke(this, new ClipLinkGroupEventArgs(group));

        return group;
    }

    /// <summary>
    /// Deletes a link group (unlinks all clips).
    /// </summary>
    public bool DeleteLinkGroup(string groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return false;

        // Remove all clip associations
        foreach (var clipId in group.ClipIds)
        {
            _clipToGroup.Remove(clipId);
        }

        _groups.Remove(groupId);
        RefreshGroupsCollection();
        GroupDeleted?.Invoke(this, new ClipLinkGroupEventArgs(group));

        return true;
    }

    /// <summary>
    /// Gets a link group by ID.
    /// </summary>
    public ClipLinkGroup? GetGroup(string groupId)
    {
        return _groups.TryGetValue(groupId, out var group) ? group : null;
    }

    /// <summary>
    /// Gets the link group containing a clip.
    /// </summary>
    public ClipLinkGroup? GetClipGroup(string clipId)
    {
        if (!_clipToGroup.TryGetValue(clipId, out var groupId))
            return null;

        return _groups.GetValueOrDefault(groupId);
    }

    /// <summary>
    /// Checks if a clip is linked.
    /// </summary>
    public bool IsClipLinked(string clipId)
    {
        return _clipToGroup.ContainsKey(clipId);
    }

    #endregion

    #region Clip Linking

    /// <summary>
    /// Links a clip to an existing group.
    /// </summary>
    public bool LinkClipToGroup(string clipId, string groupId)
    {
        if (!_groups.TryGetValue(groupId, out var group))
            return false;

        // Remove from current group if any
        UnlinkClip(clipId);

        group.ClipIds.Add(clipId);
        _clipToGroup[clipId] = groupId;
        group.ModifiedAt = DateTime.UtcNow;

        ClipLinked?.Invoke(this, new ClipLinkedEventArgs(clipId, group));

        return true;
    }

    /// <summary>
    /// Links multiple clips together (creates a new group or adds to existing).
    /// </summary>
    public ClipLinkGroup LinkClips(IEnumerable<string> clipIds)
    {
        var clipList = clipIds.ToList();

        // Check if any clip is already in a group
        foreach (var clipId in clipList)
        {
            if (_clipToGroup.TryGetValue(clipId, out var existingGroupId))
            {
                // Add remaining clips to existing group
                var existingGroup = _groups[existingGroupId];
                foreach (var id in clipList.Where(id => id != clipId && !existingGroup.ClipIds.Contains(id)))
                {
                    UnlinkClip(id);
                    existingGroup.ClipIds.Add(id);
                    _clipToGroup[id] = existingGroupId;
                    ClipLinked?.Invoke(this, new ClipLinkedEventArgs(id, existingGroup));
                }
                existingGroup.ModifiedAt = DateTime.UtcNow;
                return existingGroup;
            }
        }

        // Create new group
        return CreateLinkGroup(clipList);
    }

    /// <summary>
    /// Unlinks a clip from its group.
    /// </summary>
    public bool UnlinkClip(string clipId)
    {
        if (!_clipToGroup.TryGetValue(clipId, out var groupId))
            return false;

        if (!_groups.TryGetValue(groupId, out var group))
            return false;

        group.ClipIds.Remove(clipId);
        _clipToGroup.Remove(clipId);

        // If only one clip remains, delete the group
        if (group.ClipIds.Count < 2)
        {
            DeleteLinkGroup(groupId);
        }
        else
        {
            group.ModifiedAt = DateTime.UtcNow;

            // Update source clip if necessary
            if (group.SourceClipId == clipId)
            {
                group.SourceClipId = group.ClipIds.FirstOrDefault();
            }
        }

        ClipUnlinked?.Invoke(this, new ClipLinkedEventArgs(clipId, group));

        return true;
    }

    /// <summary>
    /// Gets all clips linked to the specified clip.
    /// </summary>
    public IReadOnlyList<string> GetLinkedClips(string clipId)
    {
        if (!_clipToGroup.TryGetValue(clipId, out var groupId))
            return Array.Empty<string>();

        if (!_groups.TryGetValue(groupId, out var group))
            return Array.Empty<string>();

        return group.ClipIds.Where(id => id != clipId).ToList().AsReadOnly();
    }

    #endregion

    #region Edit Propagation

    /// <summary>
    /// Propagates an edit from a source clip to all linked clips.
    /// </summary>
    public void PropagateEdit(string sourceClipId, LinkedEditType editType, object? editData = null)
    {
        if (!_clipToGroup.TryGetValue(sourceClipId, out var groupId))
            return;

        if (!_groups.TryGetValue(groupId, out var group) || !group.IsEnabled)
            return;

        // Check propagation mask
        if ((PropagationMask & editType) == 0)
            return;

        var targetClipIds = group.ClipIds.Where(id => id != sourceClipId).ToList();

        EditPropagated?.Invoke(this, new LinkedEditEventArgs(sourceClipId, targetClipIds, editType, editData));

        group.ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Propagates note edits to linked clips.
    /// </summary>
    public void PropagateNoteEdit(string sourceClipId, NoteEditData noteData)
    {
        PropagateEdit(sourceClipId, LinkedEditType.Notes, noteData);
    }

    /// <summary>
    /// Propagates automation edits to linked clips.
    /// </summary>
    public void PropagateAutomationEdit(string sourceClipId, AutomationEditData automationData)
    {
        PropagateEdit(sourceClipId, LinkedEditType.Automation, automationData);
    }

    /// <summary>
    /// Propagates effect changes to linked clips.
    /// </summary>
    public void PropagateEffectEdit(string sourceClipId, EffectEditData effectData)
    {
        PropagateEdit(sourceClipId, LinkedEditType.Effects, effectData);
    }

    /// <summary>
    /// Propagates gain changes to linked clips.
    /// </summary>
    public void PropagateGainEdit(string sourceClipId, double gainDb)
    {
        PropagateEdit(sourceClipId, LinkedEditType.Gain, gainDb);
    }

    /// <summary>
    /// Propagates fade changes to linked clips.
    /// </summary>
    public void PropagateFadeEdit(string sourceClipId, double fadeIn, double fadeOut)
    {
        PropagateEdit(sourceClipId, LinkedEditType.Fades, (fadeIn, fadeOut));
    }

    /// <summary>
    /// Propagates color changes to linked clips.
    /// </summary>
    public void PropagateColorEdit(string sourceClipId, string color)
    {
        PropagateEdit(sourceClipId, LinkedEditType.Color, color);
    }

    /// <summary>
    /// Enables or disables edit propagation for a group.
    /// </summary>
    public void SetGroupEnabled(string groupId, bool isEnabled)
    {
        if (_groups.TryGetValue(groupId, out var group))
        {
            group.IsEnabled = isEnabled;
        }
    }

    #endregion

    #region Visual Indicators

    /// <summary>
    /// Gets the link indicator color for a clip.
    /// </summary>
    public string? GetLinkIndicatorColor(string clipId)
    {
        var group = GetClipGroup(clipId);
        return group?.Color;
    }

    /// <summary>
    /// Gets the link group name for a clip.
    /// </summary>
    public string? GetLinkGroupName(string clipId)
    {
        var group = GetClipGroup(clipId);
        return group?.Name;
    }

    /// <summary>
    /// Gets the number of clips in a clip's link group.
    /// </summary>
    public int GetLinkedClipCount(string clipId)
    {
        var group = GetClipGroup(clipId);
        return group?.ClipIds.Count ?? 0;
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Saves link data to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var data = new LinkedClipPersistenceData
        {
            Groups = _groups.Values.ToList(),
            ClipToGroup = new Dictionary<string, string>(_clipToGroup),
            PropagationMask = _propagationMask
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads link data from a file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<LinkedClipPersistenceData>(json, JsonOptions);

        if (data != null)
        {
            _groups.Clear();
            _clipToGroup.Clear();

            foreach (var group in data.Groups)
            {
                _groups[group.Id] = group;
            }

            foreach (var kvp in data.ClipToGroup)
            {
                _clipToGroup[kvp.Key] = kvp.Value;
            }

            _propagationMask = data.PropagationMask;

            RefreshGroupsCollection();
        }
    }

    /// <summary>
    /// Clears all link data.
    /// </summary>
    public void Clear()
    {
        _groups.Clear();
        _clipToGroup.Clear();
        Groups.Clear();
    }

    #endregion

    #region Private Methods

    private void RefreshGroupsCollection()
    {
        Groups.Clear();
        foreach (var group in _groups.Values.OrderBy(g => g.CreatedAt))
        {
            Groups.Add(group);
        }
    }

    #endregion
}

#region Edit Data Classes

/// <summary>
/// Data for note edit propagation.
/// </summary>
public class NoteEditData
{
    public string EditType { get; set; } = string.Empty; // Add, Delete, Move, Resize
    public int NoteNumber { get; set; }
    public double StartBeat { get; set; }
    public double Duration { get; set; }
    public int Velocity { get; set; }
    public double OffsetBeat { get; set; }
    public double OffsetDuration { get; set; }
}

/// <summary>
/// Data for automation edit propagation.
/// </summary>
public class AutomationEditData
{
    public string EditType { get; set; } = string.Empty; // AddPoint, DeletePoint, MovePoint
    public string ParameterId { get; set; } = string.Empty;
    public double Beat { get; set; }
    public double Value { get; set; }
    public int CurveType { get; set; }
}

/// <summary>
/// Data for effect edit propagation.
/// </summary>
public class EffectEditData
{
    public string EditType { get; set; } = string.Empty; // Add, Remove, Modify, Reorder
    public int SlotIndex { get; set; }
    public string EffectType { get; set; } = string.Empty;
    public string EffectName { get; set; } = string.Empty;
    public bool IsBypassed { get; set; }
    public float Mix { get; set; }
    public Dictionary<string, float> Parameters { get; set; } = new();
}

#endregion

#region Interfaces

/// <summary>
/// Interface for linked clip service.
/// </summary>
public interface ILinkedClipService
{
    event EventHandler<ClipLinkGroupEventArgs>? GroupCreated;
    event EventHandler<ClipLinkGroupEventArgs>? GroupDeleted;
    event EventHandler<ClipLinkedEventArgs>? ClipLinked;
    event EventHandler<ClipLinkedEventArgs>? ClipUnlinked;
    event EventHandler<LinkedEditEventArgs>? EditPropagated;

    ObservableCollection<ClipLinkGroup> Groups { get; }
    LinkedEditType PropagationMask { get; set; }

    ClipLinkGroup CreateLinkGroup(IEnumerable<string> clipIds, string? name = null);
    bool DeleteLinkGroup(string groupId);
    ClipLinkGroup? GetGroup(string groupId);
    ClipLinkGroup? GetClipGroup(string clipId);
    bool IsClipLinked(string clipId);

    bool LinkClipToGroup(string clipId, string groupId);
    ClipLinkGroup LinkClips(IEnumerable<string> clipIds);
    bool UnlinkClip(string clipId);
    IReadOnlyList<string> GetLinkedClips(string clipId);

    void PropagateEdit(string sourceClipId, LinkedEditType editType, object? editData = null);
    void SetGroupEnabled(string groupId, bool isEnabled);

    string? GetLinkIndicatorColor(string clipId);
    string? GetLinkGroupName(string clipId);
    int GetLinkedClipCount(string clipId);

    Task SaveToFileAsync(string filePath);
    Task LoadFromFileAsync(string filePath);
    void Clear();
}

#endregion

#region Event Args

public class ClipLinkGroupEventArgs : EventArgs
{
    public ClipLinkGroup Group { get; }

    public ClipLinkGroupEventArgs(ClipLinkGroup group)
    {
        Group = group;
    }
}

public class ClipLinkedEventArgs : EventArgs
{
    public string ClipId { get; }
    public ClipLinkGroup Group { get; }

    public ClipLinkedEventArgs(string clipId, ClipLinkGroup group)
    {
        ClipId = clipId;
        Group = group;
    }
}

public class LinkedEditEventArgs : EventArgs
{
    public string SourceClipId { get; }
    public IReadOnlyList<string> TargetClipIds { get; }
    public LinkedEditType EditType { get; }
    public object? EditData { get; }

    public LinkedEditEventArgs(string sourceClipId, IReadOnlyList<string> targetClipIds, LinkedEditType editType, object? editData)
    {
        SourceClipId = sourceClipId;
        TargetClipIds = targetClipIds;
        EditType = editType;
        EditData = editData;
    }
}

#endregion

#region Persistence Data

internal class LinkedClipPersistenceData
{
    public List<ClipLinkGroup> Groups { get; set; } = new();
    public Dictionary<string, string> ClipToGroup { get; set; } = new();
    public LinkedEditType PropagationMask { get; set; } = LinkedEditType.All;
}

#endregion

// MusicEngineEditor - Folder Track Service
// Copyright (c) 2026 MusicEngine

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a track folder that can contain tracks and nested folders.
/// </summary>
public class TrackFolder : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "New Folder";
    private string _color = "#4A9EFF";
    private bool _isExpanded = true;
    private bool _isMuted;
    private bool _isSoloed;
    private string? _parentFolderId;
    private int _depth;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the folder name.
    /// </summary>
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the folder color.
    /// </summary>
    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether the folder is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether the folder is muted (affects all children).
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether the folder is soloed (affects all children).
    /// </summary>
    public bool IsSoloed
    {
        get => _isSoloed;
        set { _isSoloed = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the parent folder ID (null if root level).
    /// </summary>
    public string? ParentFolderId
    {
        get => _parentFolderId;
        set { _parentFolderId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the nesting depth (0 = root level).
    /// </summary>
    public int Depth
    {
        get => _depth;
        set { _depth = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the list of track IDs in this folder.
    /// </summary>
    public List<string> TrackIds { get; set; } = new();

    /// <summary>
    /// Gets the list of child folder IDs.
    /// </summary>
    public List<string> ChildFolderIds { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Service for managing collapsible track folders with nested support.
/// </summary>
public class FolderTrackService : IFolderTrackService
{
    #region Fields

    private readonly Dictionary<string, TrackFolder> _folders = new();
    private readonly Dictionary<string, string> _trackToFolder = new();
    private const int MaxNestingDepth = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #endregion

    #region Events

    public event EventHandler<FolderChangedEventArgs>? FolderCreated;
    public event EventHandler<FolderChangedEventArgs>? FolderDeleted;
    public event EventHandler<FolderChangedEventArgs>? FolderRenamed;
    public event EventHandler<FolderExpandChangedEventArgs>? FolderExpandChanged;
    public event EventHandler<FolderMuteChangedEventArgs>? FolderMuteChanged;
    public event EventHandler<TrackFolderChangedEventArgs>? TrackAddedToFolder;
    public event EventHandler<TrackFolderChangedEventArgs>? TrackRemovedFromFolder;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets all folders as an observable collection.
    /// </summary>
    public ObservableCollection<TrackFolder> Folders { get; } = new();

    #endregion

    #region Folder Management

    /// <summary>
    /// Creates a new folder.
    /// </summary>
    public TrackFolder CreateFolder(string name, string? parentFolderId = null, string? color = null)
    {
        // Validate nesting depth
        int depth = 0;
        if (parentFolderId != null)
        {
            if (!_folders.TryGetValue(parentFolderId, out var parent))
                throw new ArgumentException("Parent folder not found", nameof(parentFolderId));

            depth = parent.Depth + 1;
            if (depth > MaxNestingDepth)
                throw new InvalidOperationException($"Maximum nesting depth ({MaxNestingDepth}) exceeded");
        }

        var folder = new TrackFolder
        {
            Name = name,
            ParentFolderId = parentFolderId,
            Depth = depth,
            Color = color ?? "#4A9EFF"
        };

        _folders[folder.Id] = folder;

        // Add to parent's children
        if (parentFolderId != null && _folders.TryGetValue(parentFolderId, out var parentFolder))
        {
            parentFolder.ChildFolderIds.Add(folder.Id);
        }

        RefreshFoldersCollection();
        FolderCreated?.Invoke(this, new FolderChangedEventArgs(folder));

        return folder;
    }

    /// <summary>
    /// Deletes a folder and optionally its contents.
    /// </summary>
    public bool DeleteFolder(string folderId, bool deleteContents = false)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return false;

        // Handle children
        if (deleteContents)
        {
            // Delete all child folders recursively
            foreach (var childId in folder.ChildFolderIds.ToList())
            {
                DeleteFolder(childId, true);
            }

            // Remove all track associations
            foreach (var trackId in folder.TrackIds.ToList())
            {
                _trackToFolder.Remove(trackId);
            }
        }
        else
        {
            // Move children to parent
            foreach (var childId in folder.ChildFolderIds)
            {
                if (_folders.TryGetValue(childId, out var child))
                {
                    child.ParentFolderId = folder.ParentFolderId;
                    child.Depth = folder.Depth;
                    UpdateChildDepths(child);

                    if (folder.ParentFolderId != null && _folders.TryGetValue(folder.ParentFolderId, out var parent))
                    {
                        parent.ChildFolderIds.Add(childId);
                    }
                }
            }

            // Move tracks to parent or root
            foreach (var trackId in folder.TrackIds)
            {
                _trackToFolder.Remove(trackId);
            }
        }

        // Remove from parent's children
        if (folder.ParentFolderId != null && _folders.TryGetValue(folder.ParentFolderId, out var parentFolder))
        {
            parentFolder.ChildFolderIds.Remove(folderId);
        }

        _folders.Remove(folderId);
        RefreshFoldersCollection();
        FolderDeleted?.Invoke(this, new FolderChangedEventArgs(folder));

        return true;
    }

    /// <summary>
    /// Renames a folder.
    /// </summary>
    public bool RenameFolder(string folderId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        if (!_folders.TryGetValue(folderId, out var folder))
            return false;

        folder.Name = newName;
        FolderRenamed?.Invoke(this, new FolderChangedEventArgs(folder));

        return true;
    }

    /// <summary>
    /// Gets a folder by ID.
    /// </summary>
    public TrackFolder? GetFolder(string folderId)
    {
        return _folders.TryGetValue(folderId, out var folder) ? folder : null;
    }

    /// <summary>
    /// Gets all root-level folders.
    /// </summary>
    public IReadOnlyList<TrackFolder> GetRootFolders()
    {
        return _folders.Values.Where(f => f.ParentFolderId == null).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets child folders of a folder.
    /// </summary>
    public IReadOnlyList<TrackFolder> GetChildFolders(string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return Array.Empty<TrackFolder>();

        return folder.ChildFolderIds
            .Select(id => _folders.GetValueOrDefault(id))
            .Where(f => f != null)
            .Cast<TrackFolder>()
            .ToList()
            .AsReadOnly();
    }

    #endregion

    #region Expand/Collapse

    /// <summary>
    /// Sets the expanded state of a folder.
    /// </summary>
    public void SetFolderExpanded(string folderId, bool isExpanded)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return;

        folder.IsExpanded = isExpanded;
        FolderExpandChanged?.Invoke(this, new FolderExpandChangedEventArgs(folder, isExpanded));
    }

    /// <summary>
    /// Toggles the expanded state of a folder.
    /// </summary>
    public void ToggleFolderExpanded(string folderId)
    {
        if (_folders.TryGetValue(folderId, out var folder))
        {
            SetFolderExpanded(folderId, !folder.IsExpanded);
        }
    }

    /// <summary>
    /// Expands all folders.
    /// </summary>
    public void ExpandAll()
    {
        foreach (var folder in _folders.Values)
        {
            folder.IsExpanded = true;
        }
    }

    /// <summary>
    /// Collapses all folders.
    /// </summary>
    public void CollapseAll()
    {
        foreach (var folder in _folders.Values)
        {
            folder.IsExpanded = false;
        }
    }

    #endregion

    #region Solo/Mute

    /// <summary>
    /// Sets the mute state of a folder (affects all children).
    /// </summary>
    public void SetFolderMuted(string folderId, bool isMuted)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return;

        folder.IsMuted = isMuted;
        FolderMuteChanged?.Invoke(this, new FolderMuteChangedEventArgs(folder, isMuted, folder.IsSoloed));
    }

    /// <summary>
    /// Sets the solo state of a folder (affects all children).
    /// </summary>
    public void SetFolderSoloed(string folderId, bool isSoloed)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return;

        folder.IsSoloed = isSoloed;
        FolderMuteChanged?.Invoke(this, new FolderMuteChangedEventArgs(folder, folder.IsMuted, isSoloed));
    }

    /// <summary>
    /// Gets the effective mute state for a track considering folder hierarchy.
    /// </summary>
    public bool IsTrackEffectivelyMuted(string trackId)
    {
        if (!_trackToFolder.TryGetValue(trackId, out var folderId))
            return false;

        return IsFolderOrAncestorMuted(folderId);
    }

    /// <summary>
    /// Gets the effective solo state for a track considering folder hierarchy.
    /// </summary>
    public bool IsTrackEffectivelySoloed(string trackId)
    {
        if (!_trackToFolder.TryGetValue(trackId, out var folderId))
            return false;

        return IsFolderOrAncestorSoloed(folderId);
    }

    private bool IsFolderOrAncestorMuted(string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return false;

        if (folder.IsMuted)
            return true;

        if (folder.ParentFolderId != null)
            return IsFolderOrAncestorMuted(folder.ParentFolderId);

        return false;
    }

    private bool IsFolderOrAncestorSoloed(string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return false;

        if (folder.IsSoloed)
            return true;

        if (folder.ParentFolderId != null)
            return IsFolderOrAncestorSoloed(folder.ParentFolderId);

        return false;
    }

    #endregion

    #region Track Management

    /// <summary>
    /// Adds a track to a folder.
    /// </summary>
    public bool AddTrackToFolder(string trackId, string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return false;

        // Remove from current folder if any
        if (_trackToFolder.TryGetValue(trackId, out var currentFolderId))
        {
            RemoveTrackFromFolder(trackId);
        }

        folder.TrackIds.Add(trackId);
        _trackToFolder[trackId] = folderId;

        TrackAddedToFolder?.Invoke(this, new TrackFolderChangedEventArgs(trackId, folder));

        return true;
    }

    /// <summary>
    /// Removes a track from its folder.
    /// </summary>
    public bool RemoveTrackFromFolder(string trackId)
    {
        if (!_trackToFolder.TryGetValue(trackId, out var folderId))
            return false;

        if (!_folders.TryGetValue(folderId, out var folder))
            return false;

        folder.TrackIds.Remove(trackId);
        _trackToFolder.Remove(trackId);

        TrackRemovedFromFolder?.Invoke(this, new TrackFolderChangedEventArgs(trackId, folder));

        return true;
    }

    /// <summary>
    /// Gets the folder containing a track.
    /// </summary>
    public TrackFolder? GetTrackFolder(string trackId)
    {
        if (!_trackToFolder.TryGetValue(trackId, out var folderId))
            return null;

        return _folders.GetValueOrDefault(folderId);
    }

    /// <summary>
    /// Gets all track IDs in a folder (including nested folders).
    /// </summary>
    public IReadOnlyList<string> GetAllTracksInFolder(string folderId, bool includeNested = true)
    {
        if (!_folders.TryGetValue(folderId, out var folder))
            return Array.Empty<string>();

        var tracks = new List<string>(folder.TrackIds);

        if (includeNested)
        {
            foreach (var childId in folder.ChildFolderIds)
            {
                tracks.AddRange(GetAllTracksInFolder(childId, true));
            }
        }

        return tracks.AsReadOnly();
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Saves folder structure to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var data = new FolderPersistenceData
        {
            Folders = _folders.Values.ToList(),
            TrackToFolder = new Dictionary<string, string>(_trackToFolder)
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads folder structure from a file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<FolderPersistenceData>(json, JsonOptions);

        if (data != null)
        {
            _folders.Clear();
            _trackToFolder.Clear();

            foreach (var folder in data.Folders)
            {
                _folders[folder.Id] = folder;
            }

            foreach (var kvp in data.TrackToFolder)
            {
                _trackToFolder[kvp.Key] = kvp.Value;
            }

            RefreshFoldersCollection();
        }
    }

    /// <summary>
    /// Clears all folder data.
    /// </summary>
    public void Clear()
    {
        _folders.Clear();
        _trackToFolder.Clear();
        Folders.Clear();
    }

    #endregion

    #region Private Methods

    private void UpdateChildDepths(TrackFolder folder)
    {
        foreach (var childId in folder.ChildFolderIds)
        {
            if (_folders.TryGetValue(childId, out var child))
            {
                child.Depth = folder.Depth + 1;
                UpdateChildDepths(child);
            }
        }
    }

    private void RefreshFoldersCollection()
    {
        Folders.Clear();
        foreach (var folder in _folders.Values.OrderBy(f => f.Depth).ThenBy(f => f.Name))
        {
            Folders.Add(folder);
        }
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for folder track service.
/// </summary>
public interface IFolderTrackService
{
    event EventHandler<FolderChangedEventArgs>? FolderCreated;
    event EventHandler<FolderChangedEventArgs>? FolderDeleted;
    event EventHandler<FolderChangedEventArgs>? FolderRenamed;
    event EventHandler<FolderExpandChangedEventArgs>? FolderExpandChanged;
    event EventHandler<FolderMuteChangedEventArgs>? FolderMuteChanged;
    event EventHandler<TrackFolderChangedEventArgs>? TrackAddedToFolder;
    event EventHandler<TrackFolderChangedEventArgs>? TrackRemovedFromFolder;

    ObservableCollection<TrackFolder> Folders { get; }

    TrackFolder CreateFolder(string name, string? parentFolderId = null, string? color = null);
    bool DeleteFolder(string folderId, bool deleteContents = false);
    bool RenameFolder(string folderId, string newName);
    TrackFolder? GetFolder(string folderId);
    IReadOnlyList<TrackFolder> GetRootFolders();
    IReadOnlyList<TrackFolder> GetChildFolders(string folderId);

    void SetFolderExpanded(string folderId, bool isExpanded);
    void ToggleFolderExpanded(string folderId);
    void ExpandAll();
    void CollapseAll();

    void SetFolderMuted(string folderId, bool isMuted);
    void SetFolderSoloed(string folderId, bool isSoloed);
    bool IsTrackEffectivelyMuted(string trackId);
    bool IsTrackEffectivelySoloed(string trackId);

    bool AddTrackToFolder(string trackId, string folderId);
    bool RemoveTrackFromFolder(string trackId);
    TrackFolder? GetTrackFolder(string trackId);
    IReadOnlyList<string> GetAllTracksInFolder(string folderId, bool includeNested = true);

    Task SaveToFileAsync(string filePath);
    Task LoadFromFileAsync(string filePath);
    void Clear();
}

#endregion

#region Event Args

public class FolderChangedEventArgs : EventArgs
{
    public TrackFolder Folder { get; }

    public FolderChangedEventArgs(TrackFolder folder)
    {
        Folder = folder;
    }
}

public class FolderExpandChangedEventArgs : EventArgs
{
    public TrackFolder Folder { get; }
    public bool IsExpanded { get; }

    public FolderExpandChangedEventArgs(TrackFolder folder, bool isExpanded)
    {
        Folder = folder;
        IsExpanded = isExpanded;
    }
}

public class FolderMuteChangedEventArgs : EventArgs
{
    public TrackFolder Folder { get; }
    public bool IsMuted { get; }
    public bool IsSoloed { get; }

    public FolderMuteChangedEventArgs(TrackFolder folder, bool isMuted, bool isSoloed)
    {
        Folder = folder;
        IsMuted = isMuted;
        IsSoloed = isSoloed;
    }
}

public class TrackFolderChangedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackFolder Folder { get; }

    public TrackFolderChangedEventArgs(string trackId, TrackFolder folder)
    {
        TrackId = trackId;
        Folder = folder;
    }
}

#endregion

#region Persistence Data

internal class FolderPersistenceData
{
    public List<TrackFolder> Folders { get; set; } = new();
    public Dictionary<string, string> TrackToFolder { get; set; } = new();
}

#endregion

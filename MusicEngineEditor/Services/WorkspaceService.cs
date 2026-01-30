// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents the state of a window within a workspace.
/// </summary>
public class WindowState
{
    /// <summary>
    /// Gets or sets the left position of the window.
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// Gets or sets the top position of the window.
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// Gets or sets the width of the window.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the window.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// Gets or sets whether the window is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the window is docked.
    /// </summary>
    public bool IsDocked { get; set; }

    /// <summary>
    /// Gets or sets the dock position (Left, Right, Top, Bottom, Center).
    /// </summary>
    public string DockPosition { get; set; } = "Center";

    /// <summary>
    /// Gets or sets whether the window is maximized.
    /// </summary>
    public bool IsMaximized { get; set; }

    /// <summary>
    /// Gets or sets whether the window is minimized.
    /// </summary>
    public bool IsMinimized { get; set; }

    /// <summary>
    /// Gets or sets the window's z-order index.
    /// </summary>
    public int ZOrder { get; set; }

    /// <summary>
    /// Creates a default window state.
    /// </summary>
    public static WindowState Default => new()
    {
        Left = 100,
        Top = 100,
        Width = 800,
        Height = 600,
        IsVisible = true,
        IsDocked = false,
        DockPosition = "Center"
    };
}

/// <summary>
/// Represents a workspace configuration with window positions and layouts.
/// </summary>
public class Workspace
{
    /// <summary>
    /// Gets or sets the unique identifier for this workspace.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the workspace.
    /// </summary>
    public string Name { get; set; } = "Untitled Workspace";

    /// <summary>
    /// Gets or sets the description of the workspace.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the dictionary of window states keyed by window name.
    /// </summary>
    public Dictionary<string, WindowState> Windows { get; set; } = new();

    /// <summary>
    /// Gets or sets the currently active view name.
    /// </summary>
    public string ActiveView { get; set; } = "Arrangement";

    /// <summary>
    /// Gets or sets when this workspace was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets when this workspace was last modified.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets whether this is a built-in preset workspace.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets the icon name for this workspace.
    /// </summary>
    public string IconName { get; set; } = "Layout";

    /// <summary>
    /// Gets or sets the keyboard shortcut to activate this workspace (e.g., "Ctrl+1").
    /// </summary>
    public string? Shortcut { get; set; }

    /// <summary>
    /// Creates a deep copy of this workspace.
    /// </summary>
    public Workspace Clone()
    {
        return new Workspace
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Copy)",
            Description = Description,
            Windows = new Dictionary<string, WindowState>(
                Windows.Select(kvp => new KeyValuePair<string, WindowState>(
                    kvp.Key,
                    new WindowState
                    {
                        Left = kvp.Value.Left,
                        Top = kvp.Value.Top,
                        Width = kvp.Value.Width,
                        Height = kvp.Value.Height,
                        IsVisible = kvp.Value.IsVisible,
                        IsDocked = kvp.Value.IsDocked,
                        DockPosition = kvp.Value.DockPosition,
                        IsMaximized = kvp.Value.IsMaximized,
                        IsMinimized = kvp.Value.IsMinimized,
                        ZOrder = kvp.Value.ZOrder
                    }))),
            ActiveView = ActiveView,
            Created = DateTime.Now,
            LastModified = DateTime.Now,
            IsBuiltIn = false,
            IconName = IconName
        };
    }
}

/// <summary>
/// Service for managing workspace layouts with save/load/export functionality.
/// </summary>
public class WorkspaceService
{
    private static readonly string WorkspacesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor", "Workspaces");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<Workspace> _workspaces = [];
    private Workspace? _currentWorkspace;

    /// <summary>
    /// Gets the list of available workspaces.
    /// </summary>
    public IReadOnlyList<Workspace> Workspaces => _workspaces.AsReadOnly();

    /// <summary>
    /// Gets the currently active workspace.
    /// </summary>
    public Workspace? CurrentWorkspace => _currentWorkspace;

    /// <summary>
    /// Fired when a workspace is loaded.
    /// </summary>
    public event EventHandler<Workspace>? WorkspaceLoaded;

    /// <summary>
    /// Fired when a workspace is saved.
    /// </summary>
    public event EventHandler<Workspace>? WorkspaceSaved;

    /// <summary>
    /// Fired when the workspace list changes.
    /// </summary>
    public event EventHandler? WorkspacesChanged;

    /// <summary>
    /// Initializes the workspace service and loads existing workspaces.
    /// </summary>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(WorkspacesFolder);

        // Add built-in presets
        AddBuiltInWorkspaces();

        // Load user workspaces
        await LoadUserWorkspacesAsync();
    }

    /// <summary>
    /// Creates a new workspace with the specified name.
    /// </summary>
    /// <param name="name">The workspace name.</param>
    /// <returns>The created workspace.</returns>
    public Workspace CreateWorkspace(string name)
    {
        var workspace = new Workspace
        {
            Name = name,
            Description = $"Custom workspace created on {DateTime.Now:yyyy-MM-dd}",
            Created = DateTime.Now,
            LastModified = DateTime.Now
        };

        _workspaces.Add(workspace);
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);

        return workspace;
    }

    /// <summary>
    /// Saves the current workspace state.
    /// </summary>
    public async Task SaveCurrentWorkspaceAsync()
    {
        if (_currentWorkspace == null) return;

        _currentWorkspace.LastModified = DateTime.Now;

        if (!_currentWorkspace.IsBuiltIn)
        {
            var filePath = GetWorkspaceFilePath(_currentWorkspace);
            var json = JsonSerializer.Serialize(_currentWorkspace, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        WorkspaceSaved?.Invoke(this, _currentWorkspace);
    }

    /// <summary>
    /// Loads a workspace and applies it.
    /// </summary>
    /// <param name="workspace">The workspace to load.</param>
    public void LoadWorkspace(Workspace workspace)
    {
        _currentWorkspace = workspace;
        WorkspaceLoaded?.Invoke(this, workspace);
    }

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    /// <param name="workspace">The workspace to delete.</param>
    public void DeleteWorkspace(Workspace workspace)
    {
        if (workspace.IsBuiltIn) return;

        _workspaces.Remove(workspace);

        var filePath = GetWorkspaceFilePath(workspace);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        if (_currentWorkspace == workspace)
        {
            _currentWorkspace = null;
        }

        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Duplicates a workspace.
    /// </summary>
    /// <param name="workspace">The workspace to duplicate.</param>
    /// <returns>The duplicated workspace.</returns>
    public Workspace DuplicateWorkspace(Workspace workspace)
    {
        var clone = workspace.Clone();
        _workspaces.Add(clone);
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
        return clone;
    }

    /// <summary>
    /// Renames a workspace.
    /// </summary>
    /// <param name="workspace">The workspace to rename.</param>
    /// <param name="newName">The new name.</param>
    public void RenameWorkspace(Workspace workspace, string newName)
    {
        if (workspace.IsBuiltIn) return;

        var oldPath = GetWorkspaceFilePath(workspace);
        workspace.Name = newName;
        workspace.LastModified = DateTime.Now;

        // Delete old file if exists
        if (File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }

        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Exports a workspace to a file.
    /// </summary>
    /// <param name="workspace">The workspace to export.</param>
    /// <param name="path">The export file path.</param>
    public async Task ExportWorkspaceAsync(Workspace workspace, string path)
    {
        var json = JsonSerializer.Serialize(workspace, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Imports a workspace from a file.
    /// </summary>
    /// <param name="path">The file path to import from.</param>
    /// <returns>The imported workspace.</returns>
    public async Task<Workspace> ImportWorkspaceAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var workspace = JsonSerializer.Deserialize<Workspace>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize workspace file.");

        // Generate new ID and mark as not built-in
        workspace.Id = Guid.NewGuid();
        workspace.IsBuiltIn = false;
        workspace.Name += " (Imported)";

        _workspaces.Add(workspace);
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);

        return workspace;
    }

    /// <summary>
    /// Updates a window state in the current workspace.
    /// </summary>
    /// <param name="windowName">The window name.</param>
    /// <param name="state">The new window state.</param>
    public void UpdateWindowState(string windowName, WindowState state)
    {
        if (_currentWorkspace == null) return;

        _currentWorkspace.Windows[windowName] = state;
        _currentWorkspace.LastModified = DateTime.Now;
    }

    /// <summary>
    /// Gets the window state for a specific window.
    /// </summary>
    /// <param name="windowName">The window name.</param>
    /// <returns>The window state, or null if not found.</returns>
    public WindowState? GetWindowState(string windowName)
    {
        return _currentWorkspace?.Windows.GetValueOrDefault(windowName);
    }

    /// <summary>
    /// Creates the default Mixing workspace preset.
    /// </summary>
    public static Workspace CreateMixingWorkspace()
    {
        return new Workspace
        {
            Name = "Mixing",
            Description = "Optimized layout for mixing with mixer and analysis tools visible.",
            ActiveView = "Mixer",
            IsBuiltIn = true,
            IconName = "Mixer",
            Shortcut = "Ctrl+1",
            Windows = new Dictionary<string, WindowState>
            {
                ["MainWindow"] = new() { Left = 0, Top = 0, Width = 1920, Height = 1080, IsMaximized = true },
                ["MixerView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Bottom", Height = 350 },
                ["ArrangementView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Center" },
                ["PianoRollView"] = new() { IsVisible = false },
                ["SpectrumAnalyzer"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 300 },
                ["Goniometer"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 200 },
                ["LoudnessMeter"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 100 },
                ["VstBrowser"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Left", Width = 250 }
            }
        };
    }

    /// <summary>
    /// Creates the default Editing workspace preset.
    /// </summary>
    public static Workspace CreateEditingWorkspace()
    {
        return new Workspace
        {
            Name = "Editing",
            Description = "Optimized layout for MIDI and audio editing with large piano roll.",
            ActiveView = "PianoRoll",
            IsBuiltIn = true,
            IconName = "Edit",
            Shortcut = "Ctrl+2",
            Windows = new Dictionary<string, WindowState>
            {
                ["MainWindow"] = new() { Left = 0, Top = 0, Width = 1920, Height = 1080, IsMaximized = true },
                ["MixerView"] = new() { IsVisible = false },
                ["ArrangementView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Top", Height = 200 },
                ["PianoRollView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Center" },
                ["SpectrumAnalyzer"] = new() { IsVisible = false },
                ["Goniometer"] = new() { IsVisible = false },
                ["LoudnessMeter"] = new() { IsVisible = false },
                ["VstBrowser"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Left", Width = 200 },
                ["AutomationLanes"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Bottom", Height = 150 }
            }
        };
    }

    /// <summary>
    /// Creates the default Recording workspace preset.
    /// </summary>
    public static Workspace CreateRecordingWorkspace()
    {
        return new Workspace
        {
            Name = "Recording",
            Description = "Optimized layout for recording with input monitoring and large waveform view.",
            ActiveView = "Arrangement",
            IsBuiltIn = true,
            IconName = "Record",
            Shortcut = "Ctrl+3",
            Windows = new Dictionary<string, WindowState>
            {
                ["MainWindow"] = new() { Left = 0, Top = 0, Width = 1920, Height = 1080, IsMaximized = true },
                ["MixerView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Bottom", Height = 200 },
                ["ArrangementView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Center" },
                ["PianoRollView"] = new() { IsVisible = false },
                ["SpectrumAnalyzer"] = new() { IsVisible = false },
                ["Goniometer"] = new() { IsVisible = false },
                ["LoudnessMeter"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 100 },
                ["InputMonitor"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 300 },
                ["Metronome"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Top", Height = 50 },
                ["TransportToolbar"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Top" }
            }
        };
    }

    /// <summary>
    /// Creates the default Mastering workspace preset.
    /// </summary>
    public static Workspace CreateMasteringWorkspace()
    {
        return new Workspace
        {
            Name = "Mastering",
            Description = "Optimized layout for mastering with comprehensive analysis tools.",
            ActiveView = "Mixer",
            IsBuiltIn = true,
            IconName = "Waveform",
            Shortcut = "Ctrl+4",
            Windows = new Dictionary<string, WindowState>
            {
                ["MainWindow"] = new() { Left = 0, Top = 0, Width = 1920, Height = 1080, IsMaximized = true },
                ["MixerView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Left", Width = 400 },
                ["ArrangementView"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Center" },
                ["PianoRollView"] = new() { IsVisible = false },
                ["SpectrumAnalyzer"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 400 },
                ["Goniometer"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 250 },
                ["LoudnessMeter"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Right", Width = 150 },
                ["CorrelationMeter"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Bottom", Height = 100 },
                ["ReferenceTrack"] = new() { IsVisible = true, IsDocked = true, DockPosition = "Bottom", Height = 100 }
            }
        };
    }

    private void AddBuiltInWorkspaces()
    {
        _workspaces.Add(CreateMixingWorkspace());
        _workspaces.Add(CreateEditingWorkspace());
        _workspaces.Add(CreateRecordingWorkspace());
        _workspaces.Add(CreateMasteringWorkspace());
    }

    private async Task LoadUserWorkspacesAsync()
    {
        try
        {
            var files = Directory.GetFiles(WorkspacesFolder, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var workspace = JsonSerializer.Deserialize<Workspace>(json, JsonOptions);
                    if (workspace != null && !_workspaces.Any(w => w.Id == workspace.Id))
                    {
                        workspace.IsBuiltIn = false;
                        _workspaces.Add(workspace);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load workspace {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate workspace files: {ex.Message}");
        }
    }

    private static string GetWorkspaceFilePath(Workspace workspace)
    {
        var safeName = string.Join("_", workspace.Name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(WorkspacesFolder, $"{safeName}_{workspace.Id:N}.json");
    }
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service for managing workspace presets with complete layout persistence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents the complete layout state of a workspace preset.
/// </summary>
public class WorkspacePresetData
{
    /// <summary>
    /// Gets or sets the unique identifier for this preset.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the preset.
    /// </summary>
    public string Name { get; set; } = "Untitled Preset";

    /// <summary>
    /// Gets or sets the description of the preset.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon identifier for the preset.
    /// </summary>
    public string IconName { get; set; } = "Layout";

    /// <summary>
    /// Gets or sets the category of the preset (e.g., "Recording", "Mixing").
    /// </summary>
    public string Category { get; set; } = "Custom";

    /// <summary>
    /// Gets or sets whether this is a built-in (read-only) preset.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets the keyboard shortcut (e.g., "Ctrl+1").
    /// </summary>
    public string? Shortcut { get; set; }

    /// <summary>
    /// Gets or sets the order index for display.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets when this preset was created.
    /// </summary>
    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets when this preset was last modified.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets or sets the main window layout state.
    /// </summary>
    public WindowLayoutState MainWindow { get; set; } = new();

    /// <summary>
    /// Gets or sets the panel visibility states.
    /// </summary>
    public PanelVisibilityState Panels { get; set; } = new();

    /// <summary>
    /// Gets or sets the panel sizes (splitter positions).
    /// </summary>
    public PanelSizeState PanelSizes { get; set; } = new();

    /// <summary>
    /// Gets or sets the zoom levels for various views.
    /// </summary>
    public ZoomState ZoomLevels { get; set; } = new();

    /// <summary>
    /// Gets or sets the active tab selections.
    /// </summary>
    public TabSelectionState TabSelections { get; set; } = new();

    /// <summary>
    /// Gets or sets the thumbnail image data (base64 encoded).
    /// </summary>
    public string? ThumbnailBase64 { get; set; }

    /// <summary>
    /// Creates a deep copy of this preset.
    /// </summary>
    public WorkspacePresetData Clone()
    {
        return new WorkspacePresetData
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Copy)",
            Description = Description,
            IconName = IconName,
            Category = "Custom",
            IsBuiltIn = false,
            Shortcut = null,
            Order = Order + 1,
            Created = DateTime.Now,
            LastModified = DateTime.Now,
            MainWindow = new WindowLayoutState
            {
                Left = MainWindow.Left,
                Top = MainWindow.Top,
                Width = MainWindow.Width,
                Height = MainWindow.Height,
                WindowState = MainWindow.WindowState
            },
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = Panels.ProjectExplorerVisible,
                WorkshopVisible = Panels.WorkshopVisible,
                OutputVisible = Panels.OutputVisible,
                MixerVisible = Panels.MixerVisible,
                PianoRollVisible = Panels.PianoRollVisible,
                ArrangementVisible = Panels.ArrangementVisible,
                SynthEditorVisible = Panels.SynthEditorVisible,
                EffectsEditorVisible = Panels.EffectsEditorVisible,
                TransportVisible = Panels.TransportVisible,
                InputMonitorVisible = Panels.InputMonitorVisible,
                SpectrumAnalyzerVisible = Panels.SpectrumAnalyzerVisible,
                LoudnessMeterVisible = Panels.LoudnessMeterVisible,
                GoniometerVisible = Panels.GoniometerVisible,
                VstBrowserVisible = Panels.VstBrowserVisible,
                SessionViewVisible = Panels.SessionViewVisible,
                DJEffectsVisible = Panels.DJEffectsVisible
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = PanelSizes.LeftPanelWidth,
                RightPanelWidth = PanelSizes.RightPanelWidth,
                BottomPanelHeight = PanelSizes.BottomPanelHeight,
                MixerHeight = PanelSizes.MixerHeight,
                PianoRollHeight = PanelSizes.PianoRollHeight,
                ArrangementHeight = PanelSizes.ArrangementHeight,
                OutputHeight = PanelSizes.OutputHeight
            },
            ZoomLevels = new ZoomState
            {
                ArrangementHorizontalZoom = ZoomLevels.ArrangementHorizontalZoom,
                ArrangementVerticalZoom = ZoomLevels.ArrangementVerticalZoom,
                PianoRollHorizontalZoom = ZoomLevels.PianoRollHorizontalZoom,
                PianoRollVerticalZoom = ZoomLevels.PianoRollVerticalZoom,
                MixerZoom = ZoomLevels.MixerZoom
            },
            TabSelections = new TabSelectionState
            {
                LeftPanelTab = TabSelections.LeftPanelTab,
                RightPanelTab = TabSelections.RightPanelTab,
                BottomPanelTab = TabSelections.BottomPanelTab,
                MainEditorTab = TabSelections.MainEditorTab
            }
        };
    }
}

/// <summary>
/// Represents window layout state (position and size).
/// </summary>
public class WindowLayoutState
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 1920;
    public double Height { get; set; } = 1080;
    public string WindowState { get; set; } = "Maximized";
}

/// <summary>
/// Represents panel visibility states.
/// </summary>
public class PanelVisibilityState
{
    public bool ProjectExplorerVisible { get; set; } = true;
    public bool WorkshopVisible { get; set; }
    public bool OutputVisible { get; set; } = true;
    public bool MixerVisible { get; set; }
    public bool PianoRollVisible { get; set; }
    public bool ArrangementVisible { get; set; } = true;
    public bool SynthEditorVisible { get; set; }
    public bool EffectsEditorVisible { get; set; }
    public bool TransportVisible { get; set; } = true;
    public bool InputMonitorVisible { get; set; }
    public bool SpectrumAnalyzerVisible { get; set; }
    public bool LoudnessMeterVisible { get; set; }
    public bool GoniometerVisible { get; set; }
    public bool VstBrowserVisible { get; set; }
    public bool SessionViewVisible { get; set; }
    public bool DJEffectsVisible { get; set; }
}

/// <summary>
/// Represents panel size states (splitter positions).
/// </summary>
public class PanelSizeState
{
    public double LeftPanelWidth { get; set; } = 240;
    public double RightPanelWidth { get; set; } = 280;
    public double BottomPanelHeight { get; set; } = 200;
    public double MixerHeight { get; set; } = 350;
    public double PianoRollHeight { get; set; } = 400;
    public double ArrangementHeight { get; set; } = 300;
    public double OutputHeight { get; set; } = 150;
}

/// <summary>
/// Represents zoom levels for various views.
/// </summary>
public class ZoomState
{
    public double ArrangementHorizontalZoom { get; set; } = 1.0;
    public double ArrangementVerticalZoom { get; set; } = 1.0;
    public double PianoRollHorizontalZoom { get; set; } = 1.0;
    public double PianoRollVerticalZoom { get; set; } = 1.0;
    public double MixerZoom { get; set; } = 1.0;
}

/// <summary>
/// Represents active tab selections.
/// </summary>
public class TabSelectionState
{
    public string LeftPanelTab { get; set; } = "Files";
    public string RightPanelTab { get; set; } = "Inspector";
    public string BottomPanelTab { get; set; } = "Output";
    public string MainEditorTab { get; set; } = "Code";
}

/// <summary>
/// Container for all workspace presets.
/// </summary>
public class WorkspacePresetsFile
{
    public int Version { get; set; } = 1;
    public string? ActivePresetId { get; set; }
    public List<WorkspacePresetData> Presets { get; set; } = new();
}

/// <summary>
/// Service for managing workspace presets with save/load/export functionality.
/// </summary>
public class WorkspacePresetService
{
    private static readonly string PresetsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor", "WorkspacePresets");

    private static readonly string PresetsFilePath = Path.Combine(PresetsFolder, "presets.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<WorkspacePresetData> _presets = new();
    private WorkspacePresetData? _activePreset;

    /// <summary>
    /// Gets the list of all available presets.
    /// </summary>
    public IReadOnlyList<WorkspacePresetData> Presets => _presets.AsReadOnly();

    /// <summary>
    /// Gets the list of built-in presets.
    /// </summary>
    public IReadOnlyList<WorkspacePresetData> BuiltInPresets =>
        _presets.Where(p => p.IsBuiltIn).OrderBy(p => p.Order).ToList().AsReadOnly();

    /// <summary>
    /// Gets the list of user presets.
    /// </summary>
    public IReadOnlyList<WorkspacePresetData> UserPresets =>
        _presets.Where(p => !p.IsBuiltIn).OrderBy(p => p.Order).ToList().AsReadOnly();

    /// <summary>
    /// Gets the currently active preset.
    /// </summary>
    public WorkspacePresetData? ActivePreset => _activePreset;

    /// <summary>
    /// Fired when the preset list changes.
    /// </summary>
    public event EventHandler? PresetsChanged;

    /// <summary>
    /// Fired when a preset is loaded/activated.
    /// </summary>
    public event EventHandler<WorkspacePresetData>? PresetLoaded;

    /// <summary>
    /// Fired when a preset is saved.
    /// </summary>
    public event EventHandler<WorkspacePresetData>? PresetSaved;

    /// <summary>
    /// Initializes the service and loads presets.
    /// </summary>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(PresetsFolder);

        // Add built-in presets first
        AddBuiltInPresets();

        // Load user presets
        await LoadPresetsAsync();
    }

    /// <summary>
    /// Loads presets from the JSON file.
    /// </summary>
    private async Task LoadPresetsAsync()
    {
        try
        {
            if (File.Exists(PresetsFilePath))
            {
                var json = await File.ReadAllTextAsync(PresetsFilePath);
                var file = JsonSerializer.Deserialize<WorkspacePresetsFile>(json, JsonOptions);

                if (file?.Presets != null)
                {
                    foreach (var preset in file.Presets.Where(p => !p.IsBuiltIn))
                    {
                        if (!_presets.Any(p => p.Id == preset.Id))
                        {
                            _presets.Add(preset);
                        }
                    }

                    // Restore active preset
                    if (!string.IsNullOrEmpty(file.ActivePresetId) &&
                        Guid.TryParse(file.ActivePresetId, out var activeId))
                    {
                        _activePreset = _presets.FirstOrDefault(p => p.Id == activeId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load workspace presets: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves all presets to the JSON file.
    /// </summary>
    public async Task SavePresetsAsync()
    {
        try
        {
            var file = new WorkspacePresetsFile
            {
                Version = 1,
                ActivePresetId = _activePreset?.Id.ToString(),
                Presets = _presets.Where(p => !p.IsBuiltIn).ToList()
            };

            var json = JsonSerializer.Serialize(file, JsonOptions);
            await File.WriteAllTextAsync(PresetsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save workspace presets: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new preset with the given name.
    /// </summary>
    public WorkspacePresetData CreatePreset(string name)
    {
        var preset = new WorkspacePresetData
        {
            Name = name,
            Description = $"Custom workspace created on {DateTime.Now:yyyy-MM-dd}",
            Category = "Custom",
            Order = _presets.Count(p => !p.IsBuiltIn) + 10
        };

        _presets.Add(preset);
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return preset;
    }

    /// <summary>
    /// Saves the current window layout to a preset.
    /// </summary>
    public async Task SaveCurrentLayoutAsync(WorkspacePresetData preset, WorkspaceLayoutCapture capture)
    {
        preset.MainWindow = capture.MainWindow;
        preset.Panels = capture.Panels;
        preset.PanelSizes = capture.PanelSizes;
        preset.ZoomLevels = capture.ZoomLevels;
        preset.TabSelections = capture.TabSelections;
        preset.LastModified = DateTime.Now;

        await SavePresetsAsync();
        PresetSaved?.Invoke(this, preset);
    }

    /// <summary>
    /// Loads a preset and activates it.
    /// </summary>
    public void LoadPreset(WorkspacePresetData preset)
    {
        _activePreset = preset;
        PresetLoaded?.Invoke(this, preset);
    }

    /// <summary>
    /// Deletes a preset.
    /// </summary>
    public async Task DeletePresetAsync(WorkspacePresetData preset)
    {
        if (preset.IsBuiltIn) return;

        _presets.Remove(preset);

        if (_activePreset == preset)
        {
            _activePreset = null;
        }

        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Duplicates a preset.
    /// </summary>
    public WorkspacePresetData DuplicatePreset(WorkspacePresetData preset)
    {
        var clone = preset.Clone();
        _presets.Add(clone);
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return clone;
    }

    /// <summary>
    /// Renames a preset.
    /// </summary>
    public async Task RenamePresetAsync(WorkspacePresetData preset, string newName)
    {
        if (preset.IsBuiltIn) return;

        preset.Name = newName;
        preset.LastModified = DateTime.Now;

        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Exports a preset to a file.
    /// </summary>
    public async Task ExportPresetAsync(WorkspacePresetData preset, string filePath)
    {
        var json = JsonSerializer.Serialize(preset, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Imports a preset from a file.
    /// </summary>
    public async Task<WorkspacePresetData> ImportPresetAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var preset = JsonSerializer.Deserialize<WorkspacePresetData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize preset file.");

        preset.Id = Guid.NewGuid();
        preset.IsBuiltIn = false;
        preset.Name += " (Imported)";
        preset.Created = DateTime.Now;
        preset.LastModified = DateTime.Now;

        _presets.Add(preset);
        await SavePresetsAsync();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return preset;
    }

    /// <summary>
    /// Gets a preset by its shortcut (e.g., "Ctrl+1").
    /// </summary>
    public WorkspacePresetData? GetPresetByShortcut(string shortcut)
    {
        return _presets.FirstOrDefault(p =>
            string.Equals(p.Shortcut, shortcut, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the first N presets for quick access.
    /// </summary>
    public IEnumerable<WorkspacePresetData> GetQuickAccessPresets(int count = 5)
    {
        return _presets
            .OrderBy(p => p.IsBuiltIn ? 0 : 1)
            .ThenBy(p => p.Order)
            .Take(count);
    }

    /// <summary>
    /// Adds all built-in presets.
    /// </summary>
    private void AddBuiltInPresets()
    {
        _presets.AddRange(new[]
        {
            CreateRecordingPreset(),
            CreateMixingPreset(),
            CreateMasteringPreset(),
            CreateEditingPreset(),
            CreatePerformancePreset()
        });
    }

    /// <summary>
    /// Creates the "Recording" built-in preset.
    /// </summary>
    private static WorkspacePresetData CreateRecordingPreset()
    {
        return new WorkspacePresetData
        {
            Id = new Guid("11111111-0000-0000-0000-000000000001"),
            Name = "Recording",
            Description = "Large transport, input meters visible. Optimized for recording sessions.",
            IconName = "Record",
            Category = "Recording",
            IsBuiltIn = true,
            Shortcut = "Ctrl+1",
            Order = 1,
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = true,
                OutputVisible = true,
                MixerVisible = true,
                ArrangementVisible = true,
                TransportVisible = true,
                InputMonitorVisible = true,
                LoudnessMeterVisible = true,
                VstBrowserVisible = false,
                PianoRollVisible = false,
                SpectrumAnalyzerVisible = false,
                GoniometerVisible = false
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = 200,
                RightPanelWidth = 300,
                BottomPanelHeight = 180,
                MixerHeight = 200,
                ArrangementHeight = 400,
                OutputHeight = 120
            },
            ZoomLevels = new ZoomState
            {
                ArrangementHorizontalZoom = 1.5,
                ArrangementVerticalZoom = 1.2
            }
        };
    }

    /// <summary>
    /// Creates the "Mixing" built-in preset.
    /// </summary>
    private static WorkspacePresetData CreateMixingPreset()
    {
        return new WorkspacePresetData
        {
            Id = new Guid("22222222-0000-0000-0000-000000000002"),
            Name = "Mixing",
            Description = "Full mixer, meters prominent. Optimized for mixing sessions.",
            IconName = "Mixer",
            Category = "Mixing",
            IsBuiltIn = true,
            Shortcut = "Ctrl+2",
            Order = 2,
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = false,
                OutputVisible = true,
                MixerVisible = true,
                ArrangementVisible = true,
                TransportVisible = true,
                InputMonitorVisible = false,
                LoudnessMeterVisible = true,
                SpectrumAnalyzerVisible = true,
                GoniometerVisible = true,
                VstBrowserVisible = true,
                PianoRollVisible = false
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = 250,
                RightPanelWidth = 350,
                BottomPanelHeight = 350,
                MixerHeight = 350,
                ArrangementHeight = 250,
                OutputHeight = 100
            },
            ZoomLevels = new ZoomState
            {
                ArrangementHorizontalZoom = 0.8,
                MixerZoom = 1.2
            }
        };
    }

    /// <summary>
    /// Creates the "Mastering" built-in preset.
    /// </summary>
    private static WorkspacePresetData CreateMasteringPreset()
    {
        return new WorkspacePresetData
        {
            Id = new Guid("33333333-0000-0000-0000-000000000003"),
            Name = "Mastering",
            Description = "Loudness meters, spectrum analyzer. Optimized for mastering sessions.",
            IconName = "Waveform",
            Category = "Mastering",
            IsBuiltIn = true,
            Shortcut = "Ctrl+3",
            Order = 3,
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = false,
                OutputVisible = true,
                MixerVisible = true,
                ArrangementVisible = true,
                TransportVisible = true,
                InputMonitorVisible = false,
                LoudnessMeterVisible = true,
                SpectrumAnalyzerVisible = true,
                GoniometerVisible = true,
                VstBrowserVisible = true,
                PianoRollVisible = false,
                EffectsEditorVisible = true
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = 0,
                RightPanelWidth = 450,
                BottomPanelHeight = 200,
                MixerHeight = 400,
                ArrangementHeight = 300,
                OutputHeight = 100
            },
            ZoomLevels = new ZoomState
            {
                ArrangementHorizontalZoom = 0.5,
                MixerZoom = 1.0
            }
        };
    }

    /// <summary>
    /// Creates the "Editing" built-in preset.
    /// </summary>
    private static WorkspacePresetData CreateEditingPreset()
    {
        return new WorkspacePresetData
        {
            Id = new Guid("44444444-0000-0000-0000-000000000004"),
            Name = "Editing",
            Description = "Large piano roll, arrangement view. Optimized for MIDI/audio editing.",
            IconName = "Edit",
            Category = "Editing",
            IsBuiltIn = true,
            Shortcut = "Ctrl+4",
            Order = 4,
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = true,
                OutputVisible = false,
                MixerVisible = false,
                ArrangementVisible = true,
                PianoRollVisible = true,
                TransportVisible = true,
                InputMonitorVisible = false,
                LoudnessMeterVisible = false,
                SpectrumAnalyzerVisible = false,
                GoniometerVisible = false,
                VstBrowserVisible = true
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = 200,
                RightPanelWidth = 250,
                BottomPanelHeight = 0,
                PianoRollHeight = 500,
                ArrangementHeight = 200,
                OutputHeight = 0
            },
            ZoomLevels = new ZoomState
            {
                ArrangementHorizontalZoom = 1.2,
                ArrangementVerticalZoom = 1.0,
                PianoRollHorizontalZoom = 2.0,
                PianoRollVerticalZoom = 1.5
            }
        };
    }

    /// <summary>
    /// Creates the "Performance" built-in preset.
    /// </summary>
    private static WorkspacePresetData CreatePerformancePreset()
    {
        return new WorkspacePresetData
        {
            Id = new Guid("55555555-0000-0000-0000-000000000005"),
            Name = "Performance",
            Description = "Session view, DJ effects. Optimized for live performance.",
            IconName = "Live",
            Category = "Performance",
            IsBuiltIn = true,
            Shortcut = "Ctrl+5",
            Order = 5,
            Panels = new PanelVisibilityState
            {
                ProjectExplorerVisible = false,
                OutputVisible = false,
                MixerVisible = true,
                ArrangementVisible = false,
                PianoRollVisible = false,
                TransportVisible = true,
                InputMonitorVisible = true,
                LoudnessMeterVisible = true,
                SpectrumAnalyzerVisible = false,
                GoniometerVisible = false,
                VstBrowserVisible = false,
                SessionViewVisible = true,
                DJEffectsVisible = true
            },
            PanelSizes = new PanelSizeState
            {
                LeftPanelWidth = 0,
                RightPanelWidth = 400,
                BottomPanelHeight = 250,
                MixerHeight = 300,
                ArrangementHeight = 0,
                OutputHeight = 0
            },
            ZoomLevels = new ZoomState
            {
                MixerZoom = 1.5
            }
        };
    }
}

/// <summary>
/// Represents a capture of the current workspace layout.
/// </summary>
public class WorkspaceLayoutCapture
{
    public WindowLayoutState MainWindow { get; set; } = new();
    public PanelVisibilityState Panels { get; set; } = new();
    public PanelSizeState PanelSizes { get; set; } = new();
    public ZoomState ZoomLevels { get; set; } = new();
    public TabSelectionState TabSelections { get; set; } = new();
}

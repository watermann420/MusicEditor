// MusicEngineEditor - Track Template Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Categories for track templates.
/// </summary>
public enum TrackTemplateCategory
{
    /// <summary>General purpose templates.</summary>
    General,
    /// <summary>Drum and percussion templates.</summary>
    Drums,
    /// <summary>Bass instrument templates.</summary>
    Bass,
    /// <summary>Synthesizer templates.</summary>
    Synth,
    /// <summary>Guitar and string templates.</summary>
    Guitar,
    /// <summary>Vocal and voice templates.</summary>
    Vocals,
    /// <summary>Keys and piano templates.</summary>
    Keys,
    /// <summary>Effect and FX templates.</summary>
    FX,
    /// <summary>Bus and routing templates.</summary>
    Bus,
    /// <summary>User-defined custom templates.</summary>
    Custom
}

/// <summary>
/// Represents an effect configuration in a track template.
/// </summary>
public class TemplateEffectConfig
{
    /// <summary>Effect type name (e.g., "Compressor", "Reverb").</summary>
    public string EffectType { get; set; } = string.Empty;

    /// <summary>Whether the effect is bypassed.</summary>
    public bool Bypassed { get; set; }

    /// <summary>Effect parameter values.</summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>VST plugin path (if VST effect).</summary>
    public string? VstPluginPath { get; set; }

    /// <summary>Preset name within the plugin.</summary>
    public string? PresetName { get; set; }
}

/// <summary>
/// Represents a send configuration in a track template.
/// </summary>
public class TemplateSendConfig
{
    /// <summary>Target bus name.</summary>
    public string TargetBus { get; set; } = string.Empty;

    /// <summary>Send level (0.0 to 1.0).</summary>
    public float Level { get; set; } = 0.5f;

    /// <summary>Whether send is pre-fader.</summary>
    public bool PreFader { get; set; }
}

/// <summary>
/// Represents a complete track configuration that can be saved and loaded.
/// </summary>
public class TrackTemplate
{
    /// <summary>Unique identifier for this template.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Template name.</summary>
    public string Name { get; set; } = "Untitled Template";

    /// <summary>Template description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Template author.</summary>
    public string Author { get; set; } = Environment.UserName;

    /// <summary>Template category.</summary>
    public TrackTemplateCategory Category { get; set; } = TrackTemplateCategory.General;

    /// <summary>Tags for searching.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Whether this is a built-in template.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Track color as hex string.</summary>
    public string Color { get; set; } = "#4B6EAF";

    /// <summary>Track type (Audio, MIDI, Bus, etc.).</summary>
    public string TrackType { get; set; } = "MIDI";

    /// <summary>Default instrument/plugin for MIDI tracks.</summary>
    public string? InstrumentType { get; set; }

    /// <summary>VST instrument path (if VST).</summary>
    public string? VstInstrumentPath { get; set; }

    /// <summary>Instrument preset name.</summary>
    public string? InstrumentPreset { get; set; }

    /// <summary>Instrument parameters.</summary>
    public Dictionary<string, object> InstrumentParameters { get; set; } = [];

    /// <summary>Effect chain configuration.</summary>
    public List<TemplateEffectConfig> Effects { get; set; } = [];

    /// <summary>Send routing configuration.</summary>
    public List<TemplateSendConfig> Sends { get; set; } = [];

    /// <summary>Default volume level (0.0 to 1.0).</summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>Default pan position (-1.0 to 1.0).</summary>
    public float Pan { get; set; } = 0f;

    /// <summary>Whether track is muted by default.</summary>
    public bool Muted { get; set; }

    /// <summary>Whether track is soloed by default.</summary>
    public bool Soloed { get; set; }

    /// <summary>Whether recording is armed by default.</summary>
    public bool RecordArmed { get; set; }

    /// <summary>Input monitoring mode.</summary>
    public string InputMonitoring { get; set; } = "Auto";

    /// <summary>Output routing target.</summary>
    public string OutputRouting { get; set; } = "Master";

    /// <summary>Input source for audio tracks.</summary>
    public string? InputSource { get; set; }

    /// <summary>MIDI input channel (-1 for all).</summary>
    public int MidiChannel { get; set; } = -1;

    /// <summary>Creation date.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Last modified date.</summary>
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Version number for compatibility.</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Creates a deep copy of this template.
    /// </summary>
    public TrackTemplate Clone()
    {
        return new TrackTemplate
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Copy)",
            Description = Description,
            Author = Author,
            Category = Category,
            Tags = new List<string>(Tags),
            IsBuiltIn = false,
            Color = Color,
            TrackType = TrackType,
            InstrumentType = InstrumentType,
            VstInstrumentPath = VstInstrumentPath,
            InstrumentPreset = InstrumentPreset,
            InstrumentParameters = new Dictionary<string, object>(InstrumentParameters),
            Effects = Effects.Select(e => new TemplateEffectConfig
            {
                EffectType = e.EffectType,
                Bypassed = e.Bypassed,
                Parameters = new Dictionary<string, object>(e.Parameters),
                VstPluginPath = e.VstPluginPath,
                PresetName = e.PresetName
            }).ToList(),
            Sends = Sends.Select(s => new TemplateSendConfig
            {
                TargetBus = s.TargetBus,
                Level = s.Level,
                PreFader = s.PreFader
            }).ToList(),
            Volume = Volume,
            Pan = Pan,
            Muted = Muted,
            Soloed = Soloed,
            RecordArmed = RecordArmed,
            InputMonitoring = InputMonitoring,
            OutputRouting = OutputRouting,
            InputSource = InputSource,
            MidiChannel = MidiChannel,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Version = Version
        };
    }
}

/// <summary>
/// Service for managing track templates.
/// Provides saving, loading, and creating tracks from templates.
/// </summary>
public class TrackTemplateService : ITrackTemplateService
{
    private static TrackTemplateService? _instance;
    private static readonly object _lock = new();

    private readonly List<TrackTemplate> _templates = [];
    private readonly string _templatesFolder;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Gets the singleton instance of the TrackTemplateService.
    /// </summary>
    public static TrackTemplateService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new TrackTemplateService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets all loaded templates.
    /// </summary>
    public IReadOnlyList<TrackTemplate> Templates => _templates.AsReadOnly();

    /// <summary>
    /// Gets built-in templates.
    /// </summary>
    public IReadOnlyList<TrackTemplate> BuiltInTemplates =>
        _templates.Where(t => t.IsBuiltIn).ToList().AsReadOnly();

    /// <summary>
    /// Gets user-created templates.
    /// </summary>
    public IReadOnlyList<TrackTemplate> UserTemplates =>
        _templates.Where(t => !t.IsBuiltIn).ToList().AsReadOnly();

    /// <summary>
    /// Event raised when templates are changed.
    /// </summary>
    public event EventHandler? TemplatesChanged;

    private TrackTemplateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _templatesFolder = Path.Combine(appData, "MusicEngineEditor", "TrackTemplates");
    }

    /// <summary>
    /// Initializes the service and loads all templates.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        Directory.CreateDirectory(_templatesFolder);

        LoadBuiltInTemplates();
        await LoadUserTemplatesAsync();

        _isInitialized = true;
    }

    /// <summary>
    /// Reloads all templates.
    /// </summary>
    public async Task ReloadTemplatesAsync()
    {
        _templates.Clear();
        LoadBuiltInTemplates();
        await LoadUserTemplatesAsync();
        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets templates filtered by category.
    /// </summary>
    public IReadOnlyList<TrackTemplate> GetTemplatesByCategory(TrackTemplateCategory category)
    {
        return _templates.Where(t => t.Category == category).ToList().AsReadOnly();
    }

    /// <summary>
    /// Searches templates by name, description, or tags.
    /// </summary>
    public IReadOnlyList<TrackTemplate> SearchTemplates(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return _templates.AsReadOnly();

        var lower = searchText.ToLowerInvariant();
        return _templates
            .Where(t =>
                t.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(lower, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    public TrackTemplate? GetTemplateById(Guid id)
    {
        return _templates.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>
    /// Saves a track configuration as a new template.
    /// </summary>
    public async Task<TrackTemplate> SaveAsTemplateAsync(TrackTemplate template)
    {
        template.ModifiedDate = DateTime.UtcNow;
        template.IsBuiltIn = false;

        await SaveTemplateToFileAsync(template);

        // Add or update in list
        var existing = _templates.FirstOrDefault(t => t.Id == template.Id);
        if (existing != null)
        {
            _templates.Remove(existing);
        }
        _templates.Add(template);

        TemplatesChanged?.Invoke(this, EventArgs.Empty);
        return template;
    }

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    public async Task UpdateTemplateAsync(TrackTemplate template)
    {
        if (template.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify built-in templates.");
        }

        template.ModifiedDate = DateTime.UtcNow;
        await SaveTemplateToFileAsync(template);

        var index = _templates.FindIndex(t => t.Id == template.Id);
        if (index >= 0)
        {
            _templates[index] = template;
        }

        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a user template.
    /// </summary>
    public bool DeleteTemplate(TrackTemplate template)
    {
        if (template.IsBuiltIn)
        {
            return false;
        }

        var fileName = $"{SanitizeFileName(template.Name)}_{template.Id:N}.json";
        var filePath = Path.Combine(_templatesFolder, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        _templates.Remove(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Exports a template to a file.
    /// </summary>
    public async Task ExportTemplateAsync(TrackTemplate template, string filePath)
    {
        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Imports a template from a file.
    /// </summary>
    public async Task<TrackTemplate?> ImportTemplateAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var template = JsonSerializer.Deserialize<TrackTemplate>(json, JsonOptions);

            if (template != null)
            {
                // Assign new ID to avoid conflicts
                template.Id = Guid.NewGuid();
                template.IsBuiltIn = false;
                template.CreatedDate = DateTime.UtcNow;
                template.ModifiedDate = DateTime.UtcNow;

                await SaveAsTemplateAsync(template);
                return template;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to import template: {ex.Message}");
        }

        return null;
    }

    private void LoadBuiltInTemplates()
    {
        // Basic MIDI Track
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Basic MIDI",
            Description = "Simple MIDI track with polyphonic synth.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.General,
            Tags = ["midi", "synth", "basic"],
            IsBuiltIn = true,
            TrackType = "MIDI",
            InstrumentType = "PolySynth",
            Color = "#4B6EAF",
            Effects =
            [
                new() { EffectType = "Reverb", Parameters = new() { ["Mix"] = 0.2 } }
            ]
        });

        // Drum Track
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Name = "Drum Kit",
            Description = "MIDI track configured for drums with compression.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Drums,
            Tags = ["drums", "percussion", "beat"],
            IsBuiltIn = true,
            TrackType = "MIDI",
            InstrumentType = "SimpleSynth",
            Color = "#FF5722",
            Effects =
            [
                new() { EffectType = "Compressor", Parameters = new() { ["Ratio"] = 4.0, ["Threshold"] = -10.0 } },
                new() { EffectType = "ParametricEQ", Parameters = new() { ["HighGain"] = 2.0 } }
            ]
        });

        // Bass Track
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Name = "Bass Synth",
            Description = "Bass synth with compression and saturation.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Bass,
            Tags = ["bass", "synth", "low"],
            IsBuiltIn = true,
            TrackType = "MIDI",
            InstrumentType = "SimpleSynth",
            Color = "#3F51B5",
            Effects =
            [
                new() { EffectType = "Compressor", Parameters = new() { ["Ratio"] = 3.0, ["Attack"] = 20.0 } },
                new() { EffectType = "Distortion", Parameters = new() { ["Drive"] = 0.2, ["Type"] = "Tube" } }
            ]
        });

        // Lead Synth
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Name = "Supersaw Lead",
            Description = "Supersaw synth with delay and reverb.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Synth,
            Tags = ["lead", "supersaw", "synth", "trance"],
            IsBuiltIn = true,
            TrackType = "MIDI",
            InstrumentType = "SupersawSynth",
            Color = "#9C27B0",
            Effects =
            [
                new() { EffectType = "Delay", Parameters = new() { ["Time"] = 0.375, ["Feedback"] = 0.3 } },
                new() { EffectType = "Reverb", Parameters = new() { ["Mix"] = 0.25, ["Size"] = 0.7 } }
            ]
        });

        // Pad Track
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            Name = "Ambient Pad",
            Description = "Polyphonic pad with chorus and shimmer reverb.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Synth,
            Tags = ["pad", "ambient", "atmosphere"],
            IsBuiltIn = true,
            TrackType = "MIDI",
            InstrumentType = "PolySynth",
            Color = "#00BCD4",
            Effects =
            [
                new() { EffectType = "Chorus", Parameters = new() { ["Rate"] = 0.5, ["Depth"] = 0.4 } },
                new() { EffectType = "ShimmerReverb", Parameters = new() { ["Mix"] = 0.4, ["Shift"] = 12 } }
            ]
        });

        // Audio Recording
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000006"),
            Name = "Audio Recording",
            Description = "Audio track with input monitoring and basic processing.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.General,
            Tags = ["audio", "recording", "input"],
            IsBuiltIn = true,
            TrackType = "Audio",
            Color = "#8BC34A",
            InputMonitoring = "Auto",
            RecordArmed = true,
            Effects =
            [
                new() { EffectType = "Gate", Parameters = new() { ["Threshold"] = -40.0 } },
                new() { EffectType = "Compressor", Parameters = new() { ["Ratio"] = 2.0 } }
            ]
        });

        // Vocal Track
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000007"),
            Name = "Vocal Recording",
            Description = "Vocal track with de-esser, compression, and reverb.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Vocals,
            Tags = ["vocals", "voice", "singing"],
            IsBuiltIn = true,
            TrackType = "Audio",
            Color = "#E91E63",
            InputMonitoring = "Auto",
            RecordArmed = true,
            Effects =
            [
                new() { EffectType = "Gate", Parameters = new() { ["Threshold"] = -45.0 } },
                new() { EffectType = "DeEsser", Parameters = new() { ["Frequency"] = 6000, ["Reduction"] = -6.0 } },
                new() { EffectType = "Compressor", Parameters = new() { ["Ratio"] = 3.0, ["Threshold"] = -12.0 } },
                new() { EffectType = "ParametricEQ", Parameters = new() { ["HighGain"] = 2.0, ["LowCut"] = 80 } },
                new() { EffectType = "Reverb", Parameters = new() { ["Mix"] = 0.15 } }
            ],
            Sends = [new() { TargetBus = "Reverb Return", Level = 0.3f }]
        });

        // Reverb Return Bus
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000008"),
            Name = "Reverb Return",
            Description = "Return bus with reverb for send effects.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Bus,
            Tags = ["bus", "return", "reverb", "send"],
            IsBuiltIn = true,
            TrackType = "Return",
            Color = "#607D8B",
            Effects =
            [
                new() { EffectType = "Reverb", Parameters = new() { ["Mix"] = 1.0, ["Size"] = 0.8, ["Decay"] = 2.5 } }
            ]
        });

        // Delay Return Bus
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000009"),
            Name = "Delay Return",
            Description = "Return bus with delay for send effects.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.Bus,
            Tags = ["bus", "return", "delay", "send"],
            IsBuiltIn = true,
            TrackType = "Return",
            Color = "#607D8B",
            Effects =
            [
                new() { EffectType = "Delay", Parameters = new() { ["Mix"] = 1.0, ["Time"] = 0.25, ["Feedback"] = 0.4 } },
                new() { EffectType = "Filter", Parameters = new() { ["Frequency"] = 3000, ["Type"] = "LowPass" } }
            ]
        });

        // FX Track
        _templates.Add(new TrackTemplate
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000010"),
            Name = "FX / Riser",
            Description = "MIDI track for FX and risers with granular synth.",
            Author = "MusicEngine",
            Category = TrackTemplateCategory.FX,
            Tags = ["fx", "effects", "riser", "noise"],
            IsBuiltIn = true,
            TrackType = "MIDI",
            InstrumentType = "GranularSynth",
            Color = "#4CAF50",
            Effects =
            [
                new() { EffectType = "Filter", Parameters = new() { ["Frequency"] = 8000 } },
                new() { EffectType = "Delay", Parameters = new() { ["Time"] = 0.125, ["Feedback"] = 0.5 } },
                new() { EffectType = "Reverb", Parameters = new() { ["Mix"] = 0.5 } }
            ]
        });
    }

    private async Task LoadUserTemplatesAsync()
    {
        if (!Directory.Exists(_templatesFolder))
        {
            Directory.CreateDirectory(_templatesFolder);
            return;
        }

        var files = Directory.GetFiles(_templatesFolder, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var template = JsonSerializer.Deserialize<TrackTemplate>(json, JsonOptions);

                if (template != null)
                {
                    template.IsBuiltIn = false;
                    _templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load template from {file}: {ex.Message}");
            }
        }
    }

    private async Task SaveTemplateToFileAsync(TrackTemplate template)
    {
        var fileName = $"{SanitizeFileName(template.Name)}_{template.Id:N}.json";
        var filePath = Path.Combine(_templatesFolder, fileName);

        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder();

        foreach (char c in name)
        {
            if (!invalid.Contains(c))
            {
                result.Append(c);
            }
            else
            {
                result.Append('_');
            }
        }

        return result.ToString().Trim();
    }
}

/// <summary>
/// Interface for track template service.
/// </summary>
public interface ITrackTemplateService
{
    /// <summary>Gets all loaded templates.</summary>
    IReadOnlyList<TrackTemplate> Templates { get; }

    /// <summary>Gets built-in templates.</summary>
    IReadOnlyList<TrackTemplate> BuiltInTemplates { get; }

    /// <summary>Gets user templates.</summary>
    IReadOnlyList<TrackTemplate> UserTemplates { get; }

    /// <summary>Event raised when templates change.</summary>
    event EventHandler? TemplatesChanged;

    /// <summary>Initializes the service.</summary>
    Task InitializeAsync();

    /// <summary>Reloads all templates.</summary>
    Task ReloadTemplatesAsync();

    /// <summary>Gets templates by category.</summary>
    IReadOnlyList<TrackTemplate> GetTemplatesByCategory(TrackTemplateCategory category);

    /// <summary>Searches templates.</summary>
    IReadOnlyList<TrackTemplate> SearchTemplates(string searchText);

    /// <summary>Gets a template by ID.</summary>
    TrackTemplate? GetTemplateById(Guid id);

    /// <summary>Saves a track as a template.</summary>
    Task<TrackTemplate> SaveAsTemplateAsync(TrackTemplate template);

    /// <summary>Updates a template.</summary>
    Task UpdateTemplateAsync(TrackTemplate template);

    /// <summary>Deletes a template.</summary>
    bool DeleteTemplate(TrackTemplate template);

    /// <summary>Exports a template to file.</summary>
    Task ExportTemplateAsync(TrackTemplate template, string filePath);

    /// <summary>Imports a template from file.</summary>
    Task<TrackTemplate?> ImportTemplateAsync(string filePath);
}

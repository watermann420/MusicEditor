// MusicEngineEditor - Project Template Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing project templates.
/// Provides loading, saving, and creation of projects from templates.
/// </summary>
public class ProjectTemplateService : IProjectTemplateService
{
    private static ProjectTemplateService? _instance;
    private static readonly object _lock = new();

    private readonly List<ProjectTemplate> _templates = new();
    private readonly string _templatesFolder;
    private readonly string _builtInTemplatesFolder;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Gets the singleton instance of the ProjectTemplateService.
    /// </summary>
    public static ProjectTemplateService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ProjectTemplateService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets all loaded templates.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> Templates => _templates.AsReadOnly();

    /// <summary>
    /// Gets the built-in templates.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> BuiltInTemplates =>
        _templates.Where(t => t.IsBuiltIn).ToList().AsReadOnly();

    /// <summary>
    /// Gets user-created templates.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> UserTemplates =>
        _templates.Where(t => !t.IsBuiltIn).ToList().AsReadOnly();

    /// <summary>
    /// Event raised when templates are reloaded.
    /// </summary>
    public event EventHandler? TemplatesChanged;

    /// <summary>
    /// Creates a new ProjectTemplateService.
    /// </summary>
    private ProjectTemplateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _templatesFolder = Path.Combine(appData, "MusicEngineEditor", "Templates");
        _builtInTemplatesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
    }

    /// <summary>
    /// Initializes the service and loads all templates.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Ensure directories exist
        Directory.CreateDirectory(_templatesFolder);

        // Load built-in templates first
        LoadBuiltInTemplates();

        // Load user templates from disk
        await LoadUserTemplatesAsync();

        _isInitialized = true;
    }

    /// <summary>
    /// Loads all templates from the templates folder.
    /// </summary>
    public async Task LoadTemplatesAsync()
    {
        _templates.Clear();

        // Load built-in templates
        LoadBuiltInTemplates();

        // Load user templates
        await LoadUserTemplatesAsync();

        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets templates filtered by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Templates matching the category.</returns>
    public IReadOnlyList<ProjectTemplate> GetTemplatesByCategory(TemplateCategory category)
    {
        return _templates.Where(t => t.Category == category).ToList().AsReadOnly();
    }

    /// <summary>
    /// Searches templates by name or description.
    /// </summary>
    /// <param name="searchText">Text to search for.</param>
    /// <returns>Matching templates.</returns>
    public IReadOnlyList<ProjectTemplate> SearchTemplates(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return _templates.AsReadOnly();

        var lower = searchText.ToLowerInvariant();
        return _templates
            .Where(t =>
                t.TemplateName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(lower, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <returns>The template, or null if not found.</returns>
    public ProjectTemplate? GetTemplateById(Guid id)
    {
        return _templates.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>
    /// Creates a new MusicProject from a template.
    /// </summary>
    /// <param name="template">The template to use.</param>
    /// <param name="projectName">Name for the new project.</param>
    /// <param name="projectPath">Path where the project will be created.</param>
    /// <returns>The created project.</returns>
    public MusicProject CreateProjectFromTemplate(ProjectTemplate template, string projectName, string projectPath)
    {
        var project = new MusicProject
        {
            Name = projectName,
            Guid = Guid.NewGuid(),
            Namespace = SanitizeNamespace(projectName),
            FilePath = Path.Combine(projectPath, projectName, $"{projectName}.meproj"),
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Settings = new ProjectSettings
            {
                SampleRate = template.SampleRate,
                BufferSize = template.BufferSize,
                DefaultBpm = template.DefaultBpm
            }
        };

        // Create project directory structure
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "Scripts"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Audio"));
        Directory.CreateDirectory(Path.Combine(projectDir, "bin"));
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));

        // Create default Main.me script with template track setup code
        var mainScript = CreateTemplateScript(project, template);
        project.Scripts.Add(mainScript);

        return project;
    }

    /// <summary>
    /// Saves a project as a new template.
    /// </summary>
    /// <param name="project">The project to save as template.</param>
    /// <param name="templateName">Name for the template.</param>
    /// <param name="category">Category for the template.</param>
    /// <param name="description">Description for the template.</param>
    /// <returns>The created template.</returns>
    public async Task<ProjectTemplate> SaveAsTemplateAsync(
        MusicProject project,
        string templateName,
        TemplateCategory category,
        string description = "")
    {
        var template = new ProjectTemplate
        {
            Id = Guid.NewGuid(),
            TemplateName = templateName,
            Description = description,
            Author = Environment.UserName,
            Category = category,
            DefaultBpm = project.Settings.DefaultBpm,
            SampleRate = project.Settings.SampleRate,
            BufferSize = project.Settings.BufferSize,
            IsBuiltIn = false,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        // Save template to disk
        await SaveTemplateAsync(template);

        _templates.Add(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return template;
    }

    /// <summary>
    /// Saves a template to disk.
    /// </summary>
    /// <param name="template">The template to save.</param>
    public async Task SaveTemplateAsync(ProjectTemplate template)
    {
        var fileName = $"{SanitizeFileName(template.TemplateName)}.json";
        var filePath = Path.Combine(_templatesFolder, fileName);

        template.ModifiedDate = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Deletes a user template.
    /// </summary>
    /// <param name="template">The template to delete.</param>
    /// <returns>True if deleted successfully.</returns>
    public bool DeleteTemplate(ProjectTemplate template)
    {
        if (template.IsBuiltIn)
        {
            return false; // Cannot delete built-in templates
        }

        var fileName = $"{SanitizeFileName(template.TemplateName)}.json";
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
    /// Loads built-in templates.
    /// </summary>
    private void LoadBuiltInTemplates()
    {
        // Empty Project template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            TemplateName = "Empty Project",
            Description = "A blank canvas with just a master track. Perfect for starting from scratch.",
            Author = "MusicEngine",
            Category = TemplateCategory.Empty,
            IsBuiltIn = true,
            DefaultBpm = 120,
            Tags = new List<string> { "empty", "blank", "minimal" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Basic Beat template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            TemplateName = "Basic Beat",
            Description = "A simple setup with drums, bass, and synth tracks. Great for quick ideas.",
            Author = "MusicEngine",
            Category = TemplateCategory.Electronic,
            IsBuiltIn = true,
            DefaultBpm = 128,
            Tags = new List<string> { "beat", "drums", "bass", "synth", "electronic" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Drums", Type = TemplateTrackType.Midi, Color = "#FF5722", DefaultInstrument = "SimpleSynth" },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth" },
                new() { Name = "Synth", Type = TemplateTrackType.Midi, Color = "#9C27B0", DefaultInstrument = "PolySynth" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Full Band template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            TemplateName = "Full Band",
            Description = "Complete band setup with drums, bass, guitar, keys, and vocals tracks.",
            Author = "MusicEngine",
            Category = TemplateCategory.Band,
            IsBuiltIn = true,
            DefaultBpm = 120,
            Tags = new List<string> { "band", "rock", "live", "acoustic", "vocals" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Drums", Type = TemplateTrackType.Midi, Color = "#FF5722", DefaultInstrument = "SimpleSynth", Group = "Rhythm" },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth", Group = "Rhythm" },
                new() { Name = "Guitar", Type = TemplateTrackType.Audio, Color = "#795548", Group = "Instruments" },
                new() { Name = "Keys", Type = TemplateTrackType.Midi, Color = "#009688", DefaultInstrument = "PolySynth", Group = "Instruments" },
                new() { Name = "Vocals", Type = TemplateTrackType.Audio, Color = "#E91E63" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Electronic template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            TemplateName = "Electronic",
            Description = "Professional electronic music setup with drums, bass, lead, pad, and FX tracks.",
            Author = "MusicEngine",
            Category = TemplateCategory.Electronic,
            IsBuiltIn = true,
            DefaultBpm = 130,
            Tags = new List<string> { "electronic", "edm", "synth", "dance", "house", "techno" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Kick", Type = TemplateTrackType.Midi, Color = "#F44336", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Snare", Type = TemplateTrackType.Midi, Color = "#FF9800", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Hi-Hats", Type = TemplateTrackType.Midi, Color = "#FFC107", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth" },
                new() { Name = "Lead", Type = TemplateTrackType.Midi, Color = "#9C27B0", DefaultInstrument = "SupersawSynth" },
                new() { Name = "Pad", Type = TemplateTrackType.Midi, Color = "#00BCD4", DefaultInstrument = "PolySynth" },
                new() { Name = "FX", Type = TemplateTrackType.Midi, Color = "#4CAF50", DefaultInstrument = "GranularSynth" },
                new() { Name = "Reverb Return", Type = TemplateTrackType.Return, Color = "#607D8B" },
                new() { Name = "Delay Return", Type = TemplateTrackType.Return, Color = "#607D8B" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A", DefaultEffects = new List<string> { "Limiter" } }
            }
        });

        // Orchestral Sketch template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
            TemplateName = "Orchestral Sketch",
            Description = "Cinematic orchestral template with strings, brass, woodwinds, and percussion sections.",
            Author = "MusicEngine",
            Category = TemplateCategory.Orchestral,
            IsBuiltIn = true,
            DefaultBpm = 90,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "orchestral", "cinematic", "film", "classical", "epic" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Violins I", Type = TemplateTrackType.Midi, Color = "#8B4513", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Violins II", Type = TemplateTrackType.Midi, Color = "#A0522D", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Violas", Type = TemplateTrackType.Midi, Color = "#CD853F", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Cellos", Type = TemplateTrackType.Midi, Color = "#D2691E", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Basses", Type = TemplateTrackType.Midi, Color = "#8B0000", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "French Horns", Type = TemplateTrackType.Midi, Color = "#DAA520", DefaultInstrument = "PolySynth", Group = "Brass" },
                new() { Name = "Trumpets", Type = TemplateTrackType.Midi, Color = "#FFD700", DefaultInstrument = "PolySynth", Group = "Brass" },
                new() { Name = "Trombones", Type = TemplateTrackType.Midi, Color = "#B8860B", DefaultInstrument = "PolySynth", Group = "Brass" },
                new() { Name = "Flutes", Type = TemplateTrackType.Midi, Color = "#87CEEB", DefaultInstrument = "PolySynth", Group = "Woodwinds" },
                new() { Name = "Clarinets", Type = TemplateTrackType.Midi, Color = "#4682B4", DefaultInstrument = "PolySynth", Group = "Woodwinds" },
                new() { Name = "Timpani", Type = TemplateTrackType.Midi, Color = "#8B4513", DefaultInstrument = "SimpleSynth", Group = "Percussion" },
                new() { Name = "Percussion", Type = TemplateTrackType.Midi, Color = "#A52A2A", DefaultInstrument = "SimpleSynth", Group = "Percussion" },
                new() { Name = "Reverb Hall", Type = TemplateTrackType.Return, Color = "#607D8B", DefaultEffects = new List<string> { "ConvolutionReverb" } },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });
    }

    /// <summary>
    /// Loads user templates from the templates folder.
    /// </summary>
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
                var template = JsonSerializer.Deserialize<ProjectTemplate>(json, JsonOptions);

                if (template != null)
                {
                    template.IsBuiltIn = false; // Ensure user templates are marked correctly
                    _templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue loading other templates
                System.Diagnostics.Debug.WriteLine($"Failed to load template from {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Creates a default script for a template-based project.
    /// </summary>
    private MusicScript CreateTemplateScript(MusicProject project, ProjectTemplate template)
    {
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        var scriptPath = Path.Combine(projectDir, "Scripts", "Main.me");
        var ns = $"{project.Namespace}.Scripts";

        var trackSetup = new System.Text.StringBuilder();
        foreach (var track in template.Tracks)
        {
            if (track.Type == TemplateTrackType.Master)
                continue;

            trackSetup.AppendLine($"            // {track.Name} Track");
            if (track.Type == TemplateTrackType.Midi && !string.IsNullOrEmpty(track.DefaultInstrument))
            {
                trackSetup.AppendLine($"            // var {ToVariableName(track.Name)} = new {track.DefaultInstrument}();");
            }
            trackSetup.AppendLine();
        }

        var content = $@"// ============================================
// MusicEngine Script
// Project: {project.Name}
// Template: {template.TemplateName}
// Namespace: {ns}
// File: Main.me
// Created: {DateTime.Now:yyyy-MM-dd}
// ============================================

#project {project.Name}

namespace {ns}
{{
    public class Main : MusicScript
    {{
        public override void Setup()
        {{
            // Template: {template.TemplateName}
            // {template.Description}

            Bpm = {template.DefaultBpm};

            // Track Setup:
{trackSetup}
        }}

        public override void Play()
        {{
            // Start your patterns here
            Pattern.Note(""C4 E4 G4"")
                .Every(1.0)
                .Gain(0.8f)
                .Start();
        }}

        public override void Stop()
        {{
            // Cleanup when stopped
        }}
    }}
}}
";

        return new MusicScript
        {
            FilePath = scriptPath,
            Namespace = ns,
            IsEntryPoint = true,
            Content = content,
            Project = project,
            IsDirty = true
        };
    }

    private static string SanitizeNamespace(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
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
        return result.ToString();
    }

    private static string ToVariableName(string name)
    {
        var result = new System.Text.StringBuilder();
        var nextUpper = false;

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (result.Length == 0)
                {
                    result.Append(char.ToLowerInvariant(c));
                }
                else if (nextUpper)
                {
                    result.Append(char.ToUpperInvariant(c));
                    nextUpper = false;
                }
                else
                {
                    result.Append(c);
                }
            }
            else
            {
                nextUpper = true;
            }
        }

        return result.Length > 0 ? result.ToString() : "track";
    }
}

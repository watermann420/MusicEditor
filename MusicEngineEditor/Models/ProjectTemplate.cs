// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace MusicEngineEditor.Models;

/// <summary>
/// Categories for project templates.
/// </summary>
public enum TemplateCategory
{
    /// <summary>Empty/blank project.</summary>
    Empty,
    /// <summary>Electronic music templates.</summary>
    Electronic,
    /// <summary>Acoustic/live instrument templates.</summary>
    Acoustic,
    /// <summary>Orchestral/classical templates.</summary>
    Orchestral,
    /// <summary>Rock/Band templates.</summary>
    Band,
    /// <summary>Hip-Hop/Urban templates.</summary>
    HipHop,
    /// <summary>Experimental/Sound design templates.</summary>
    Experimental,
    /// <summary>Podcast/Voice recording templates.</summary>
    Podcast,
    /// <summary>Film scoring/Media composition templates.</summary>
    FilmScoring,
    /// <summary>Jazz templates.</summary>
    Jazz,
    /// <summary>User-created custom templates.</summary>
    Custom
}

/// <summary>
/// Type of track in a template.
/// </summary>
public enum TemplateTrackType
{
    /// <summary>Audio track for recorded audio.</summary>
    Audio,
    /// <summary>MIDI track for virtual instruments.</summary>
    Midi,
    /// <summary>Bus/Group track for routing.</summary>
    Bus,
    /// <summary>Master output track.</summary>
    Master,
    /// <summary>Return/Send track for effects.</summary>
    Return
}

/// <summary>
/// Represents a track definition in a project template.
/// </summary>
public class TemplateTrack
{
    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the track type.
    /// </summary>
    public TemplateTrackType Type { get; set; } = TemplateTrackType.Midi;

    /// <summary>
    /// Gets or sets the track color as a hex string (e.g., "#FF5722").
    /// </summary>
    public string Color { get; set; } = "#4B6EAF";

    /// <summary>
    /// Gets or sets the default instrument/plugin for MIDI tracks.
    /// </summary>
    public string? DefaultInstrument { get; set; }

    /// <summary>
    /// Gets or sets the list of default effect plugins.
    /// </summary>
    public List<string> DefaultEffects { get; set; } = new();

    /// <summary>
    /// Gets or sets the default volume level (0.0 to 1.0).
    /// </summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets the default pan position (-1.0 to 1.0).
    /// </summary>
    public float Pan { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets whether the track is initially muted.
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>
    /// Gets or sets whether the track is initially soloed.
    /// </summary>
    public bool Soloed { get; set; }

    /// <summary>
    /// Gets or sets the output routing (e.g., "Master", "Bus 1").
    /// </summary>
    public string OutputRouting { get; set; } = "Master";

    /// <summary>
    /// Gets or sets the group/folder this track belongs to.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets the Color as a WPF Color object.
    /// </summary>
    public Color GetColor()
    {
        try
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(Color);
        }
        catch
        {
            return Colors.DodgerBlue;
        }
    }
}

/// <summary>
/// Represents a project template that defines a starting point for new projects.
/// </summary>
public class ProjectTemplate
{
    /// <summary>
    /// Gets or sets the unique identifier for this template.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string TemplateName { get; set; } = "Untitled Template";

    /// <summary>
    /// Gets or sets the template description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template author.
    /// </summary>
    public string Author { get; set; } = "MusicEngine";

    /// <summary>
    /// Gets or sets the template category.
    /// </summary>
    public TemplateCategory Category { get; set; } = TemplateCategory.Empty;

    /// <summary>
    /// Gets or sets the path to the template thumbnail image.
    /// </summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// Gets or sets the list of tracks in this template.
    /// </summary>
    public List<TemplateTrack> Tracks { get; set; } = new();

    /// <summary>
    /// Gets or sets the default BPM for projects created from this template.
    /// </summary>
    public int DefaultBpm { get; set; } = 120;

    /// <summary>
    /// Gets or sets the time signature numerator (beats per measure).
    /// </summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>
    /// Gets or sets the time signature denominator (note value that gets one beat).
    /// </summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>
    /// Gets the time signature as a formatted string (e.g., "4/4").
    /// </summary>
    public string TimeSignature => $"{TimeSignatureNumerator}/{TimeSignatureDenominator}";

    /// <summary>
    /// Gets or sets the default sample rate in Hz.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the default buffer size.
    /// </summary>
    public int BufferSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the global effects applied to the master track.
    /// </summary>
    public List<string> MasterEffects { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is a built-in template.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets the date this template was created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date this template was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets tags for searching templates.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets the number of tracks in this template.
    /// </summary>
    public int TrackCount => Tracks.Count;

    /// <summary>
    /// Gets a summary of the template for display.
    /// </summary>
    public string Summary =>
        $"{TrackCount} track{(TrackCount != 1 ? "s" : "")} | {DefaultBpm} BPM | {TimeSignature}";

    /// <summary>
    /// Gets the category display name.
    /// </summary>
    public string CategoryName => Category switch
    {
        TemplateCategory.Empty => "Empty",
        TemplateCategory.Electronic => "Electronic",
        TemplateCategory.Acoustic => "Acoustic",
        TemplateCategory.Orchestral => "Orchestral",
        TemplateCategory.Band => "Band/Rock",
        TemplateCategory.HipHop => "Hip-Hop/Urban",
        TemplateCategory.Experimental => "Experimental",
        TemplateCategory.Podcast => "Podcast/Voice",
        TemplateCategory.FilmScoring => "Film Scoring",
        TemplateCategory.Jazz => "Jazz",
        TemplateCategory.Custom => "Custom",
        _ => "Other"
    };

    /// <summary>
    /// Gets the icon for the category.
    /// </summary>
    public string CategoryIcon => Category switch
    {
        TemplateCategory.Empty => "\u2395",       // Empty rectangle
        TemplateCategory.Electronic => "\u2B4F",   // Wave
        TemplateCategory.Acoustic => "\u266B",     // Music notes
        TemplateCategory.Orchestral => "\u1D11E",  // Treble clef
        TemplateCategory.Band => "\u266A",         // Note
        TemplateCategory.HipHop => "\u2667",       // Club
        TemplateCategory.Experimental => "\u269B", // Atom
        TemplateCategory.Podcast => "\u2399",      // Microphone
        TemplateCategory.FilmScoring => "\u2302",  // Film
        TemplateCategory.Jazz => "\u266C",         // Double note
        TemplateCategory.Custom => "\u2605",       // Star
        _ => "\u2022"
    };

    /// <summary>
    /// Creates a deep copy of this template.
    /// </summary>
    public ProjectTemplate Clone()
    {
        return new ProjectTemplate
        {
            Id = Guid.NewGuid(), // New ID for the clone
            TemplateName = TemplateName,
            Description = Description,
            Author = Author,
            Category = Category,
            ThumbnailPath = ThumbnailPath,
            Tracks = Tracks.Select(t => new TemplateTrack
            {
                Name = t.Name,
                Type = t.Type,
                Color = t.Color,
                DefaultInstrument = t.DefaultInstrument,
                DefaultEffects = new List<string>(t.DefaultEffects),
                Volume = t.Volume,
                Pan = t.Pan,
                Muted = t.Muted,
                Soloed = t.Soloed,
                OutputRouting = t.OutputRouting,
                Group = t.Group
            }).ToList(),
            DefaultBpm = DefaultBpm,
            TimeSignatureNumerator = TimeSignatureNumerator,
            TimeSignatureDenominator = TimeSignatureDenominator,
            SampleRate = SampleRate,
            BufferSize = BufferSize,
            MasterEffects = new List<string>(MasterEffects),
            IsBuiltIn = false, // Clones are never built-in
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Tags = new List<string>(Tags)
        };
    }
}

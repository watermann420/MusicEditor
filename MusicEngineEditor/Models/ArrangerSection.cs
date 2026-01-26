// MusicEngineEditor - Arranger Section Model
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Predefined section types for arrangement.
/// </summary>
public enum SectionType
{
    Intro,
    Verse,
    PreChorus,
    Chorus,
    Bridge,
    Solo,
    Breakdown,
    Buildup,
    Drop,
    Outro,
    Custom
}

/// <summary>
/// Represents a section in the arranger track (e.g., Intro, Verse, Chorus).
/// </summary>
public partial class ArrangerSection : ObservableObject
{
    /// <summary>
    /// Unique identifier for the section.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Display name of the section.
    /// </summary>
    [ObservableProperty]
    private string _name = "Section";

    /// <summary>
    /// Start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _startBeat;

    /// <summary>
    /// Length of the section in beats.
    /// </summary>
    [ObservableProperty]
    private double _lengthBeats = 16;

    /// <summary>
    /// Section type.
    /// </summary>
    [ObservableProperty]
    private SectionType _sectionType = SectionType.Custom;

    /// <summary>
    /// Display color as hex string.
    /// </summary>
    [ObservableProperty]
    private string _color = "#4B6EAF";

    /// <summary>
    /// Indicates whether the section is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Indicates whether the section is currently being dragged.
    /// </summary>
    [ObservableProperty]
    private bool _isDragging;

    /// <summary>
    /// Indicates whether the section is locked.
    /// </summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>
    /// Optional notes/description for the section.
    /// </summary>
    [ObservableProperty]
    private string? _notes;

    /// <summary>
    /// Gets the end beat position.
    /// </summary>
    public double EndBeat => StartBeat + LengthBeats;

    /// <summary>
    /// Creates a new section with default values.
    /// </summary>
    public ArrangerSection()
    {
    }

    /// <summary>
    /// Creates a new section with specified values.
    /// </summary>
    /// <param name="name">Section name.</param>
    /// <param name="startBeat">Start position in beats.</param>
    /// <param name="lengthBeats">Length in beats.</param>
    /// <param name="sectionType">Section type.</param>
    public ArrangerSection(string name, double startBeat, double lengthBeats, SectionType sectionType = SectionType.Custom)
    {
        Name = name;
        StartBeat = startBeat;
        LengthBeats = lengthBeats;
        SectionType = sectionType;
        Color = GetDefaultColorForType(sectionType);
    }

    /// <summary>
    /// Gets the default color for a section type.
    /// </summary>
    /// <param name="type">Section type.</param>
    /// <returns>Hex color string.</returns>
    public static string GetDefaultColorForType(SectionType type)
    {
        return type switch
        {
            SectionType.Intro => "#6B8E23",      // Olive
            SectionType.Verse => "#4682B4",      // Steel Blue
            SectionType.PreChorus => "#9370DB",  // Medium Purple
            SectionType.Chorus => "#DC143C",     // Crimson
            SectionType.Bridge => "#FF8C00",     // Dark Orange
            SectionType.Solo => "#FFD700",       // Gold
            SectionType.Breakdown => "#8B4513",  // Saddle Brown
            SectionType.Buildup => "#20B2AA",    // Light Sea Green
            SectionType.Drop => "#FF1493",       // Deep Pink
            SectionType.Outro => "#708090",      // Slate Gray
            _ => "#4B6EAF"                       // Default Accent
        };
    }

    /// <summary>
    /// Creates a deep copy of this section.
    /// </summary>
    /// <returns>A new ArrangerSection with the same values but a new Id.</returns>
    public ArrangerSection Clone()
    {
        return new ArrangerSection
        {
            Id = Guid.NewGuid(),
            Name = Name,
            StartBeat = StartBeat,
            LengthBeats = LengthBeats,
            SectionType = SectionType,
            Color = Color,
            IsSelected = false,
            IsDragging = false,
            IsLocked = IsLocked,
            Notes = Notes
        };
    }

    /// <summary>
    /// Checks if this section overlaps with another.
    /// </summary>
    /// <param name="other">The other section.</param>
    /// <returns>True if sections overlap.</returns>
    public bool OverlapsWith(ArrangerSection other)
    {
        if (other == null || other.Id == Id) return false;
        return StartBeat < other.EndBeat && EndBeat > other.StartBeat;
    }

    /// <summary>
    /// Checks if a beat position is within this section.
    /// </summary>
    /// <param name="beat">Beat position to check.</param>
    /// <returns>True if the beat is within the section.</returns>
    public bool ContainsBeat(double beat)
    {
        return beat >= StartBeat && beat < EndBeat;
    }

    partial void OnSectionTypeChanged(SectionType value)
    {
        // Update name and color when type changes if using default name
        if (string.IsNullOrEmpty(Name) || Name == "Section" || Enum.TryParse<SectionType>(Name, out _))
        {
            Name = value.ToString();
            Color = GetDefaultColorForType(value);
        }
    }
}

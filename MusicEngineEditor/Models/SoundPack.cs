// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Sound pack container model.

using System.Collections.Generic;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a collection of audio samples bundled together
/// </summary>
public class SoundPack
{
    /// <summary>
    /// Unique identifier for the sound pack
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the sound pack
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Author or creator of the sound pack
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// License type (e.g., "Royalty-Free", "CC0")
    /// </summary>
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// Version of the sound pack
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Collection of audio samples in this pack
    /// </summary>
    public List<AudioSample> Samples { get; set; } = new();
}

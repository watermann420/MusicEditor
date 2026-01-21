using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing sound packs and audio samples
/// </summary>
public interface ISoundPackService
{
    /// <summary>
    /// Gets all installed sound packs
    /// </summary>
    /// <returns>Read-only list of installed sound packs</returns>
    IReadOnlyList<SoundPack> GetInstalledPacks();

    /// <summary>
    /// Searches samples across all installed packs
    /// </summary>
    /// <param name="query">Text search query (searches name and tags)</param>
    /// <param name="category">Filter by category (Drums, Bass, Synths, FX, etc.)</param>
    /// <returns>List of matching audio samples</returns>
    IReadOnlyList<AudioSample> SearchSamples(string? query = null, string? category = null);

    /// <summary>
    /// Gets a stream to read the sample audio data
    /// </summary>
    /// <param name="sample">The audio sample to get</param>
    /// <returns>Stream containing the audio data</returns>
    Task<Stream> GetSampleStreamAsync(AudioSample sample);

    /// <summary>
    /// Plays a preview of the specified sample
    /// </summary>
    /// <param name="sample">The sample to preview</param>
    Task PreviewSampleAsync(AudioSample sample);

    /// <summary>
    /// Stops any currently playing preview
    /// </summary>
    void StopPreview();

    /// <summary>
    /// Gets whether a preview is currently playing
    /// </summary>
    bool IsPreviewPlaying { get; }

    /// <summary>
    /// Gets the currently previewing sample, if any
    /// </summary>
    AudioSample? CurrentPreviewSample { get; }
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Sound pack loading service.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using MusicEngineEditor.Models;
using NAudio.Wave;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing sound packs and audio sample previews
/// </summary>
public class SoundPackService : ISoundPackService, IDisposable
{
    private readonly List<SoundPack> _installedPacks = new();
    private readonly List<AudioSample> _allSamples = new();
    private readonly object _previewLock = new();

    private WaveOutEvent? _waveOut;
    private WaveStream? _audioStream;
    private bool _disposed;

    public bool IsPreviewPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public AudioSample? CurrentPreviewSample { get; private set; }

    public SoundPackService()
    {
        LoadBuiltInPacks();
    }

    private void LoadBuiltInPacks()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = "MusicEngineEditor.Resources.SoundPacks.";

            // Find all manifest.json resources
            var manifestResources = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(resourcePrefix) && name.EndsWith("manifest.json"))
                .ToList();

            foreach (var manifestResource in manifestResources)
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(manifestResource);
                    if (stream == null) continue;

                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var pack = JsonSerializer.Deserialize<SoundPack>(json, options);

                    if (pack != null)
                    {
                        // Set the sound pack ID on all samples
                        foreach (var sample in pack.Samples)
                        {
                            sample.SoundPackId = pack.Id;
                        }

                        _installedPacks.Add(pack);
                        _allSamples.AddRange(pack.Samples);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load sound pack from {manifestResource}: {ex.Message}");
                }
            }

            // If no embedded packs found, create a default built-in pack
            if (_installedPacks.Count == 0)
            {
                CreateDefaultBuiltInPack();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load built-in sound packs: {ex.Message}");
            CreateDefaultBuiltInPack();
        }
    }

    private void CreateDefaultBuiltInPack()
    {
        // Create a default pack with placeholder samples
        var defaultPack = new SoundPack
        {
            Id = "builtin",
            Name = "Built-in Essentials",
            Author = "MusicEngine",
            License = "Royalty-Free",
            Version = "1.0.0",
            Samples = new List<AudioSample>
            {
                new()
                {
                    Id = "kick_01",
                    Name = "Acoustic Kick",
                    Category = "Drums",
                    FilePath = "Drums/kick_acoustic.wav",
                    Tags = new List<string> { "kick", "acoustic", "punchy" },
                    Duration = TimeSpan.FromMilliseconds(250),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "snare_01",
                    Name = "Tight Snare",
                    Category = "Drums",
                    FilePath = "Drums/snare_tight.wav",
                    Tags = new List<string> { "snare", "tight", "crisp" },
                    Duration = TimeSpan.FromMilliseconds(200),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "hihat_01",
                    Name = "Closed Hi-Hat",
                    Category = "Drums",
                    FilePath = "Drums/hihat_closed.wav",
                    Tags = new List<string> { "hihat", "closed", "tight" },
                    Duration = TimeSpan.FromMilliseconds(100),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "hihat_02",
                    Name = "Open Hi-Hat",
                    Category = "Drums",
                    FilePath = "Drums/hihat_open.wav",
                    Tags = new List<string> { "hihat", "open", "sustain" },
                    Duration = TimeSpan.FromMilliseconds(500),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "bass_01",
                    Name = "Sub Bass Hit",
                    Category = "Bass",
                    FilePath = "Bass/sub_hit.wav",
                    Tags = new List<string> { "bass", "sub", "808" },
                    Key = "C",
                    Duration = TimeSpan.FromMilliseconds(800),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "bass_02",
                    Name = "Synth Bass Stab",
                    Category = "Bass",
                    FilePath = "Bass/synth_stab.wav",
                    Tags = new List<string> { "bass", "synth", "stab" },
                    Key = "F",
                    Duration = TimeSpan.FromMilliseconds(300),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "synth_01",
                    Name = "Pad Atmosphere",
                    Category = "Synths",
                    FilePath = "Synths/pad_atmosphere.wav",
                    Tags = new List<string> { "pad", "atmosphere", "ambient" },
                    Key = "Am",
                    Duration = TimeSpan.FromSeconds(4),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "synth_02",
                    Name = "Lead Pluck",
                    Category = "Synths",
                    FilePath = "Synths/lead_pluck.wav",
                    Tags = new List<string> { "lead", "pluck", "melodic" },
                    Key = "C",
                    Duration = TimeSpan.FromMilliseconds(600),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "fx_01",
                    Name = "Riser Impact",
                    Category = "FX",
                    FilePath = "FX/riser_impact.wav",
                    Tags = new List<string> { "riser", "impact", "transition" },
                    Duration = TimeSpan.FromSeconds(2),
                    SoundPackId = "builtin"
                },
                new()
                {
                    Id = "fx_02",
                    Name = "Sweep Down",
                    Category = "FX",
                    FilePath = "FX/sweep_down.wav",
                    Tags = new List<string> { "sweep", "down", "transition" },
                    Duration = TimeSpan.FromSeconds(1.5),
                    SoundPackId = "builtin"
                }
            }
        };

        _installedPacks.Add(defaultPack);
        _allSamples.AddRange(defaultPack.Samples);
    }

    public IReadOnlyList<SoundPack> GetInstalledPacks()
    {
        return _installedPacks.AsReadOnly();
    }

    public IReadOnlyList<AudioSample> SearchSamples(string? query = null, string? category = null)
    {
        var results = _allSamples.AsEnumerable();

        // Filter by category
        if (!string.IsNullOrWhiteSpace(category) && category != "All")
        {
            results = results.Where(s =>
                s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by search query (name and tags)
        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            results = results.Where(s =>
                searchTerms.All(term =>
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    s.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (s.Key?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        return results.ToList().AsReadOnly();
    }

    public async Task<Stream> GetSampleStreamAsync(AudioSample sample)
    {
        if (sample == null)
            throw new ArgumentNullException(nameof(sample));

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"MusicEngineEditor.Resources.SoundPacks.{sample.SoundPackId}.{sample.FilePath.Replace('/', '.')}";

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            return await Task.FromResult(stream);
        }

        // Return empty stream if sample not found
        return new MemoryStream();
    }

    public async Task PreviewSampleAsync(AudioSample sample)
    {
        if (sample == null)
            throw new ArgumentNullException(nameof(sample));

        // Stop any current preview
        StopPreview();

        try
        {
            var stream = await GetSampleStreamAsync(sample);
            if (stream.Length == 0)
            {
                // Sample not found, just set the state
                CurrentPreviewSample = sample;
                return;
            }

            lock (_previewLock)
            {
                // Create audio reader based on file extension
                var extension = Path.GetExtension(sample.FilePath).ToLowerInvariant();

                WaveStream waveStream;
                if (extension == ".mp3")
                {
                    waveStream = new Mp3FileReader(stream);
                }
                else
                {
                    waveStream = new WaveFileReader(stream);
                }

                _audioStream = waveStream;
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioStream);
                _waveOut.PlaybackStopped += OnPlaybackStopped;

                CurrentPreviewSample = sample;
                _waveOut.Play();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to preview sample: {ex.Message}");
            CurrentPreviewSample = sample;
        }
    }

    public void StopPreview()
    {
        lock (_previewLock)
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioStream != null)
                {
                    _audioStream.Dispose();
                    _audioStream = null;
                }

                CurrentPreviewSample = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping preview: {ex.Message}");
            }
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_previewLock)
        {
            CurrentPreviewSample = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopPreview();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
